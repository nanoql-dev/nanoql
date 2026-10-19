// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/swaption/discretizedswaption.hpp, .cpp: DiscretizedSwaption, which no wheel class exposes, built
// inside each call from a swaption spec (instruments/swaptionspec.hpp, its arguments set up by the swaption itself),
// a reference date and a day counter.
//
//     DiscretizedSwaption::mandatoryTimes   (swaption, referenceDate, dayCounter)
//         the times, in QuantLib's order;
//     DiscretizedSwaption::rollback         (swaption, referenceDate, dayCounter, model, gridTimes, gridSteps, from,
//                                            to)
//         the swaption initialized at from on the model's tree on TimeGrid(gridTimes, gridSteps), rolled back to to:
//         {"values", "presentValue"}.
//
// A model is {"type": "HullWhite", "curve", "a", "sigma"}, its curve a curve spec of indexes/indexspec.hpp, or
// {"type": "Vasicek", "r0", "a", "b", "sigma", "lambda"}.

#include "indexes/indexspec.hpp"
#include "instruments/swaptionspec.hpp"
#include "math/optimization/specs.hpp"

#include <ql/models/shortrate/onefactormodels/hullwhite.hpp>
#include <ql/models/shortrate/onefactormodels/vasicek.hpp>
#include <ql/pricingengines/swaption/discretizedswaption.hpp>
#include <ql/timegrid.hpp>

#include <set>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using swaptions::SwaptionSpec;

        struct ModelSpec {
            bool hullWhite = true;
            indexes::CurveSpec curve;
            Real r0 = 0.0, a = 0.0, b = 0.0, sigma = 0.0, lambda = 0.0;
        };

        ModelSpec model(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a model spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            auto key = [&](const char* name) -> const json::value& {
                const json::value* x = o.if_contains(name);
                if (x == nullptr)
                    throw InvalidParams(std::string("a model spec needs ") + name);
                return *x;
            };
            std::string type = text(key("type"));
            std::set<std::string> keys;
            ModelSpec m;
            if (type == "HullWhite") {
                keys = {"type", "curve", "a", "sigma"};
                m.curve = indexes::curve(key("curve"));
            } else if (type == "Vasicek") {
                keys = {"type", "r0", "a", "b", "sigma", "lambda"};
                m.hullWhite = false;
                m.r0 = real(key("r0"));
                m.b = real(key("b"));
                m.lambda = real(key("lambda"));
            } else {
                throw InvalidParams("unknown model " + type);
            }
            for (const auto& kv : o) {
                if (keys.count(std::string(kv.key())) == 0)
                    throw InvalidParams(type + " takes no key " + std::string(kv.key()));
            }
            m.a = real(key("a"));
            m.sigma = real(key("sigma"));
            return m;
        }

        ext::shared_ptr<ShortRateModel> makeModel(const ModelSpec& m) {
            if (m.hullWhite)
                return ext::make_shared<HullWhite>(indexes::makeCurve(m.curve), m.a, m.sigma);
            return ext::make_shared<Vasicek>(m.r0, m.a, m.b, m.sigma, m.lambda);
        }

        Swaption::arguments arguments(const SwaptionSpec& s) {
            Swaption::arguments args;
            swaptions::makeSwaption(s)->setupArguments(&args);
            return args;
        }

        const Register unit({
            {"DiscretizedSwaption::mandatoryTimes",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 SwaptionSpec s = swaptions::swaption(a[0]);
                 DateSpec reference = date(a[1]);
                 DayCounterSpec dc = dayCounter(a[2]);
                 return [=] {
                     DiscretizedSwaption asset(arguments(s), makeDate(reference), makeDayCounter(dc));
                     std::vector<Time> times = asset.mandatoryTimes();
                     return optimization::arrayResult(Array(times.begin(), times.end()));
                 };
             }},
            {"DiscretizedSwaption::rollback",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 SwaptionSpec s = swaptions::swaption(a[0]);
                 DateSpec reference = date(a[1]);
                 DayCounterSpec dc = dayCounter(a[2]);
                 ModelSpec m = model(a[3]);
                 std::vector<Real> gridTimes = reals(a[4]);
                 Integer gridSteps = integer(a[5]);
                 if (gridSteps < 0)
                     throw InvalidParams("expected a non-negative number of steps");
                 Real from = real(a[6]), to = real(a[7]);
                 return [=] {
                     DiscretizedSwaption asset(arguments(s), makeDate(reference), makeDayCounter(dc));
                     ext::shared_ptr<Lattice> lattice =
                         makeModel(m)->tree(TimeGrid(gridTimes.begin(), gridTimes.end(), gridSteps));
                     asset.initialize(lattice, from);
                     asset.rollback(to);
                     return json::value(json::object{{"values", optimization::arrayResult(asset.values())},
                                                     {"presentValue", asset.presentValue()}});
                 };
             }},
        });

    }

}
