// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/shortrate/generalizedhullwhite.hpp, .cpp and generalizedornsteinuhlenbeckprocess.hpp, .cpp: the
// generalized Hull-White model and its process, which the wheel doesn't bind.
//
// "GeneralizedHullWhite::values" takes (curve, model, queries): the curve a models::CurveSpec (models/modelspec.hpp);
// the model {"type": "constant", "a", "sigma"}, the (curve, a, sigma) constructor, or {"type": "linearFlat",
// "speedDates", "volDates", "speeds", "vols"}, the LinearFlat one, or the same keys under "linear", the template
// constructor on Linear interpolations; the queries {"bondOptions": [[type, strike, maturity, bondMaturity], ...],
// "discountBonds": [[now, maturity, rate], ...], "phi": [t, ...], "tree": [end, steps]}. It returns {"a", "sigma",
// "fixedReversion", "bondOptions", "discountBonds", "phi", "treeSizes", "tree", "dynamics"}: a() and sigma(),
// fixedReversion(), discountBondOption's prices, discountBond's (A and B through A·exp(−B·r)), the analytical fitting
// parameter through HWdynamics()'s shortRate(t, 0), and the tree(TimeGrid(end, steps)): "treeSizes" each column's
// size, "tree" the columns' node discounts in turn; "dynamics" whether dynamics() throws; on the LinearFlat form also
// {"global", "requiredPoints"}, LinearFlat's traits.
//
// "GeneralizedOrnsteinUhlenbeckProcess::values" takes (speed, vol, x0, level, points), the points [[t, x, dt], ...],
// and returns each point's drift, diffusion, expectation, stdDeviation and variance in turn, on a constant speed and
// volatility. "GeneralizedOrnsteinUhlenbeckProcess::members" takes (speed, vol, x0, level, times) and returns
// {"speed", "volatility", "level"}: speed(t) and volatility(t) at each time, and level().

#include "convert.hpp"
#include "models/modelspec.hpp"

#include <ql/experimental/shortrate/generalizedhullwhite.hpp>
#include <ql/experimental/shortrate/generalizedornsteinuhlenbeckprocess.hpp>
#include <ql/math/interpolations/linearinterpolation.hpp>
#include <ql/timegrid.hpp>

#include <cstdint>
#include <initializer_list>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const json::object& object(const json::value& v, const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + " spec, got " + json::serialize(v));
            return v.get_object();
        }

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        struct GhwSpec {
            std::string type;
            Real a = 0.0, sigma = 0.0;
            std::vector<DateSpec> speedDates, volDates;
            std::vector<Real> speeds, vols;
        };

        std::vector<DateSpec> dates(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a list of dates, got " + json::serialize(v));
            std::vector<DateSpec> out;
            for (const json::value& d : v.get_array())
                out.push_back(date(d));
            return out;
        }

        GhwSpec ghw(const json::value& v) {
            const json::object& o = object(v, "a generalized Hull-White model");
            GhwSpec s;
            s.type = text(key(o, "type", "a generalized Hull-White model"));
            if (s.type == "constant") {
                onlyKeys(o, {"type", "a", "sigma"}, s.type);
                s.a = real(key(o, "a", s.type));
                s.sigma = real(key(o, "sigma", s.type));
            } else if (s.type == "linearFlat" || s.type == "linear") {
                onlyKeys(o, {"type", "speedDates", "volDates", "speeds", "vols"}, s.type);
                s.speedDates = dates(key(o, "speedDates", s.type));
                s.volDates = dates(key(o, "volDates", s.type));
                s.speeds = reals(key(o, "speeds", s.type));
                s.vols = reals(key(o, "vols", s.type));
            } else {
                throw InvalidParams("unknown generalized Hull-White model " + s.type);
            }
            return s;
        }

        ext::shared_ptr<GeneralizedHullWhite> makeGhw(const models::CurveSpec& c, const GhwSpec& s) {
            Handle<YieldTermStructure> curve(models::makeCurve(c));
            if (s.type == "constant")
                return ext::make_shared<GeneralizedHullWhite>(curve, s.a, s.sigma);
            std::vector<Date> speedDates, volDates;
            for (const DateSpec& d : s.speedDates)
                speedDates.push_back(makeDate(d));
            for (const DateSpec& d : s.volDates)
                volDates.push_back(makeDate(d));
            if (s.type == "linear")
                return ext::make_shared<GeneralizedHullWhite>(curve, speedDates, volDates, s.speeds, s.vols, Linear(),
                                                              Linear());
            return ext::make_shared<GeneralizedHullWhite>(curve, speedDates, volDates, s.speeds, s.vols);
        }

        std::vector<std::vector<Real>> rows(const json::value& v, std::size_t width, const std::string& what) {
            if (!v.is_array())
                throw InvalidParams("expected a list of " + what + ", got " + json::serialize(v));
            std::vector<std::vector<Real>> out;
            for (const json::value& r : v.get_array()) {
                std::vector<Real> row = reals(r);
                if (row.size() != width)
                    throw InvalidParams(what + " take " + std::to_string(width) + " numbers each");
                out.push_back(row);
            }
            return out;
        }

        const Register unit({
            {"GeneralizedHullWhite::values",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 models::CurveSpec c = models::curve(a[0]);
                 GhwSpec s = ghw(a[1]);
                 const json::object& q = object(a[2], "the queries");
                 onlyKeys(q, {"bondOptions", "discountBonds", "phi", "tree"}, "the queries");
                 auto options = rows(key(q, "bondOptions", "the queries"), 4, "bond options");
                 auto bonds = rows(key(q, "discountBonds", "the queries"), 3, "discount bonds");
                 std::vector<Real> phiTimes = reals(key(q, "phi", "the queries"));
                 std::vector<Real> tree = reals(key(q, "tree", "the queries"));
                 if (tree.size() != 2)
                     throw InvalidParams("a tree is [end, steps]");
                 return [=] {
                     auto m = makeGhw(c, s);
                     json::object out;
                     out["a"] = m->a();
                     out["sigma"] = m->sigma();
                     json::array fixed;
                     for (bool f : m->fixedReversion())
                         fixed.emplace_back(f);
                     out["fixedReversion"] = fixed;
                     json::array prices;
                     for (const auto& o : options)
                         prices.emplace_back(m->discountBondOption(o[0] > 0 ? Option::Call : Option::Put, o[1], o[2], o[3]));
                     out["bondOptions"] = prices;
                     json::array discounts;
                     for (const auto& b : bonds)
                         discounts.emplace_back(m->discountBond(b[0], b[1], b[2]));
                     out["discountBonds"] = discounts;
                     json::array phi;
                     auto dynamics = m->HWdynamics();
                     for (Real t : phiTimes)
                         phi.emplace_back(dynamics->shortRate(t, 0.0));
                     out["phi"] = phi;
                     auto lattice = ext::dynamic_pointer_cast<OneFactorModel::ShortRateTree>(
                         m->tree(TimeGrid(tree[0], static_cast<Size>(tree[1]))));
                     json::array sizes, nodes;
                     for (Size i = 0; i + 1 < lattice->timeGrid().size(); ++i) {
                         sizes.emplace_back(static_cast<std::int64_t>(lattice->size(i)));
                         for (Size j = 0; j < lattice->size(i); ++j)
                             nodes.emplace_back(lattice->discount(i, j));
                     }
                     out["treeSizes"] = sizes;
                     out["tree"] = nodes;
                     try {
                         m->dynamics();
                         out["dynamics"] = "returned";
                     } catch (const Error&) {
                         out["dynamics"] = json::object{{"throws", true}};
                     }
                     if (s.type == "linearFlat") {
                         out["global"] = LinearFlat::global;
                         out["requiredPoints"] = static_cast<std::int64_t>(LinearFlat::requiredPoints);
                     }
                     return json::value(out);
                 };
             }},
            {"GeneralizedOrnsteinUhlenbeckProcess::values",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Real speed = real(a[0]), vol = real(a[1]), x0 = real(a[2]), level = real(a[3]);
                 auto points = rows(a[4], 3, "points");
                 return [=] {
                     GeneralizedOrnsteinUhlenbeckProcess p([speed](Real) { return speed; }, [vol](Real) { return vol; },
                                                           x0, level);
                     json::array out;
                     for (const auto& r : points) {
                         out.emplace_back(p.drift(r[0], r[1]));
                         out.emplace_back(p.diffusion(r[0], r[1]));
                         out.emplace_back(p.expectation(r[0], r[1], r[2]));
                         out.emplace_back(p.stdDeviation(r[0], r[1], r[2]));
                         out.emplace_back(p.variance(r[0], r[1], r[2]));
                     }
                     return json::value(out);
                 };
             }},
            {"GeneralizedOrnsteinUhlenbeckProcess::members",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Real speed = real(a[0]), vol = real(a[1]), x0 = real(a[2]), level = real(a[3]);
                 std::vector<Real> times = reals(a[4]);
                 return [=] {
                     GeneralizedOrnsteinUhlenbeckProcess p([speed](Real) { return speed; }, [vol](Real) { return vol; },
                                                           x0, level);
                     json::array speeds, vols;
                     for (Real t : times) {
                         speeds.emplace_back(p.speed(t));
                         vols.emplace_back(p.volatility(t));
                     }
                     json::object out;
                     out["speed"] = speeds;
                     out["volatility"] = vols;
                     out["level"] = p.level();
                     return json::value(out);
                 };
             }},
        });

    }

}
