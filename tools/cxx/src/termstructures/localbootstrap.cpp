// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/localbootstrap.hpp, which the wheel doesn't expose: a piecewise convex-monotone curve on the local
// bootstrap, built inside the call from specs of termstructures/yield/ratehelperspec.hpp under the settings already
// set. "LocalBootstrap::curve" takes (curve, helpers, bootstrap, discount dates): the curve {"traits",
// "quadraticity", "monotonicity", "forcePositive", "referenceDate", "dayCounter"}, the traits "ForwardRate" or
// "ZeroYield", the interpolator ConvexMonotone(quadraticity, monotonicity, forcePositive), a fixed reference date and
// a day counter of convert.hpp; the bootstrap {"localisation", "forcePositive", "accuracy"}, the accuracy null for
// Null<Real>(). It returns {"dates", "data", "maxDate", "quoteErrors", "discounts"}: the curve's nodes, its maximum
// date, each helper's quote error, and the discounts at the dates, extrapolating. A refusal of QuantLib's, the
// bootstrap's or the curve's, is the call's.

#include "convert.hpp"
#include "termstructures/yield/ratehelperspec.hpp"

#include <ql/math/interpolations/convexmonotoneinterpolation.hpp>
#include <ql/termstructures/localbootstrap.hpp>
#include <ql/termstructures/yield/bootstraptraits.hpp>
#include <ql/termstructures/yield/piecewiseyieldcurve.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::RateHelperSpec;

        struct CurveSpec {
            std::string traits;
            Real quadraticity = 0.3, monotonicity = 0.7;
            bool forcePositive = true;
            DateSpec referenceDate;
            DayCounterSpec dayCounter;
        };

        struct BootstrapSpec {
            Integer localisation = 2;
            bool forcePositive = true;
            Real accuracy = Null<Real>();
        };

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " spec needs " + name);
            return *v;
        }

        CurveSpec curve(const json::value& v) {
            if (!v.is_object() || v.get_object().size() != 6)
                throw InvalidParams("expected a curve spec of six keys, got " + json::serialize(v));
            const json::object& o = v.get_object();
            CurveSpec s;
            s.traits = text(key(o, "traits", "a curve"));
            if (s.traits != "ForwardRate" && s.traits != "ZeroYield")
                throw InvalidParams("no local curve on " + s.traits);
            s.quadraticity = real(key(o, "quadraticity", "a curve"));
            s.monotonicity = real(key(o, "monotonicity", "a curve"));
            s.forcePositive = boolean(key(o, "forcePositive", "a curve"));
            s.referenceDate = date(key(o, "referenceDate", "a curve"));
            s.dayCounter = dayCounter(key(o, "dayCounter", "a curve"));
            return s;
        }

        BootstrapSpec bootstrap(const json::value& v) {
            if (!v.is_object() || v.get_object().size() != 3)
                throw InvalidParams("expected a bootstrap spec of three keys, got " + json::serialize(v));
            const json::object& o = v.get_object();
            BootstrapSpec s;
            s.localisation = integer(key(o, "localisation", "a bootstrap"));
            if (s.localisation == Null<Integer>() || s.localisation < 0)
                throw InvalidParams("a localisation is a Size");
            s.forcePositive = boolean(key(o, "forcePositive", "a bootstrap"));
            s.accuracy = real(key(o, "accuracy", "a bootstrap"));
            return s;
        }

        template <class Traits>
        json::value run(const CurveSpec& c, const std::vector<RateHelperSpec>& h, const BootstrapSpec& b,
                        const std::vector<DateSpec>& discountDates) {
            typedef PiecewiseYieldCurve<Traits, ConvexMonotone, LocalBootstrap> Curve;
            std::vector<ext::shared_ptr<RateHelper>> instruments;
            for (const RateHelperSpec& s : h)
                instruments.push_back(termstructures::makeRateHelper(s));
            typename Curve::bootstrap_type localBootstrap(static_cast<Size>(b.localisation), b.forcePositive,
                                                          b.accuracy);
            auto curve = ext::make_shared<Curve>(makeDate(c.referenceDate), instruments, makeDayCounter(c.dayCounter),
                                                 ConvexMonotone(c.quadraticity, c.monotonicity, c.forcePositive),
                                                 localBootstrap);
            json::array nodeDates, nodeData, errors, discounts;
            for (const auto& n : curve->nodes()) {
                nodeDates.push_back(result(n.first));
                nodeData.push_back(json::value(n.second));
            }
            for (const auto& i : instruments)
                errors.push_back(json::value(i->quoteError()));
            for (const DateSpec& d : discountDates)
                discounts.push_back(json::value(curve->discount(makeDate(d), true)));
            json::object out;
            out["dates"] = nodeDates;
            out["data"] = nodeData;
            out["maxDate"] = result(curve->maxDate());
            out["quoteErrors"] = errors;
            out["discounts"] = discounts;
            return out;
        }

        const Register unit({
            {"LocalBootstrap::curve",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 CurveSpec c = curve(a[0]);
                 if (!a[1].is_array())
                     throw InvalidParams("expected an array of rate helpers, got " + json::serialize(a[1]));
                 std::vector<RateHelperSpec> h;
                 for (const json::value& v : a[1].get_array())
                     h.push_back(termstructures::rateHelper(v));
                 BootstrapSpec b = bootstrap(a[2]);
                 if (!a[3].is_array())
                     throw InvalidParams("expected an array of dates, got " + json::serialize(a[3]));
                 std::vector<DateSpec> dates;
                 for (const json::value& v : a[3].get_array())
                     dates.push_back(date(v));
                 return [=] {
                     return c.traits == "ForwardRate" ? run<ForwardRate>(c, h, b, dates) : run<ZeroYield>(c, h, b, dates);
                 };
             }},
        });

    }

}
