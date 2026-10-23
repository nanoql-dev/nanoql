// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/optionlet/capletvariancecurve.hpp: CapletVarianceCurve, which the wheel doesn't bind,
// built inside each call from a spec {"referenceDate" (a serial), "dates" (serials), "vols", "dayCounter"
// (convert.hpp's), "volatilityType" (QuantLib's integer), "displacement"}.
//
//     CapletVarianceCurve::inspectors              (spec)    {"referenceDate", "maxDate", "maxTime", "minStrike",
//                                                            "maxStrike", "volatilityType", "displacement"}
//     CapletVarianceCurve::volatility(Date)        (spec, date, strike, extrapolate)    a real
//     CapletVarianceCurve::volatility(Time)        (spec, time, strike, extrapolate)
//     CapletVarianceCurve::blackVariance(Date)     (spec, date, strike, extrapolate)
//     CapletVarianceCurve::blackVariance(Time)     (spec, time, strike, extrapolate)
//     CapletVarianceCurve::smileSection(Date)      (spec, date, strike)    {"exerciseTime", "volatility",
//                                                                           "variance"}, the section's at the strike
//     CapletVarianceCurve::smileSection(Time)      (spec, time, strike)
//
// The members are OptionletVolatilityStructure's; smileSection takes extrapolate true, the section's volatility
// type and shift left out (QuantLib's flat section drops the curve's, the S-g1d plan's G1R6 (xiii)).

#include "convert.hpp"

#include <ql/termstructures/volatility/optionlet/capletvariancecurve.hpp>
#include <ql/termstructures/volatility/smilesection.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a caplet variance curve spec needs ") + name);
            return *v;
        }

        struct CurveSpec {
            DateSpec referenceDate;
            std::vector<DateSpec> dates;
            std::vector<Real> vols;
            DayCounterSpec dayCounter;
            VolatilityType type = ShiftedLognormal;
            Real displacement = 0.0;
        };

        CurveSpec curve(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a caplet variance curve spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                const std::string k(kv.key());
                if (k != "referenceDate" && k != "dates" && k != "vols" && k != "dayCounter" &&
                    k != "volatilityType" && k != "displacement")
                    throw InvalidParams("a caplet variance curve spec takes no key " + k);
            }
            CurveSpec s;
            s.referenceDate = date(key(o, "referenceDate"));
            if (!key(o, "dates").is_array())
                throw InvalidParams("a caplet variance curve's dates are an array");
            for (const auto& d : key(o, "dates").get_array())
                s.dates.push_back(date(d));
            s.vols = reals(key(o, "vols"));
            s.dayCounter = dayCounter(key(o, "dayCounter"));
            s.type = enumeration<VolatilityType>(key(o, "volatilityType"));
            s.displacement = real(key(o, "displacement"));
            return s;
        }

        ext::shared_ptr<CapletVarianceCurve> makeCurve(const CurveSpec& s) {
            std::vector<Date> dates;
            for (const auto& d : s.dates)
                dates.push_back(makeDate(d));
            return ext::make_shared<CapletVarianceCurve>(makeDate(s.referenceDate), dates, s.vols,
                                                         makeDayCounter(s.dayCounter), s.type, s.displacement);
        }

        json::value section(const ext::shared_ptr<SmileSection>& s, Real strike) {
            return json::value(json::object{{"exerciseTime", s->exerciseTime()},
                                            {"volatility", s->volatility(strike)},
                                            {"variance", s->variance(strike)}});
        }

        // A member at (spec, date or time, strike, extrapolate).
        template <class F>
        Call atPoint(const Args& a, bool byDate, F f) {
            arity(a, 4);
            CurveSpec s = curve(a[0]);
            DateSpec d = byDate ? date(a[1]) : DateSpec{true, 0};
            Real t = byDate ? 0.0 : real(a[1]);
            Real strike = real(a[2]);
            bool extrapolate = boolean(a[3]);
            return [=] {
                auto c = makeCurve(s);
                return json::value(byDate ? f(*c, makeDate(d), strike, extrapolate) : f(*c, t, strike, extrapolate));
            };
        }

        const Register unit({
            {"CapletVarianceCurve::inspectors",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 CurveSpec s = curve(a[0]);
                 return [=] {
                     auto c = makeCurve(s);
                     return json::value(json::object{{"referenceDate", result(c->referenceDate())},
                                                     {"maxDate", result(c->maxDate())},
                                                     {"maxTime", c->maxTime()},
                                                     {"minStrike", c->minStrike()},
                                                     {"maxStrike", c->maxStrike()},
                                                     {"volatilityType", static_cast<int>(c->volatilityType())},
                                                     {"displacement", c->displacement()}});
                 };
             }},
            {"CapletVarianceCurve::volatility(Date)",
             [](const Args& a) {
                 return atPoint(a, true, [](const auto& c, auto x, Real k, bool e) { return c.volatility(x, k, e); });
             }},
            {"CapletVarianceCurve::volatility(Time)",
             [](const Args& a) {
                 return atPoint(a, false, [](const auto& c, auto x, Real k, bool e) { return c.volatility(x, k, e); });
             }},
            {"CapletVarianceCurve::blackVariance(Date)",
             [](const Args& a) {
                 return atPoint(a, true,
                                [](const auto& c, auto x, Real k, bool e) { return c.blackVariance(x, k, e); });
             }},
            {"CapletVarianceCurve::blackVariance(Time)",
             [](const Args& a) {
                 return atPoint(a, false,
                                [](const auto& c, auto x, Real k, bool e) { return c.blackVariance(x, k, e); });
             }},
            {"CapletVarianceCurve::smileSection(Date)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 CurveSpec s = curve(a[0]);
                 DateSpec d = date(a[1]);
                 Real strike = real(a[2]);
                 return [=] { return section(makeCurve(s)->smileSection(makeDate(d), true), strike); };
             }},
            {"CapletVarianceCurve::smileSection(Time)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 CurveSpec s = curve(a[0]);
                 Real t = real(a[1]);
                 Real strike = real(a[2]);
                 return [=] { return section(makeCurve(s)->smileSection(t, true), strike); };
             }},
        });

    }

}
