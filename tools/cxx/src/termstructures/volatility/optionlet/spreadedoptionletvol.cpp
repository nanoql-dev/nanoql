// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/optionlet/spreadedoptionletvol.hpp: SpreadedOptionletVolatility, which the wheel doesn't
// bind, built inside each call over a base of optionletvolspec.hpp, or over the adapter of a stripper of
// stripperspec.hpp, with a spread value (its quote's):
//
//     SpreadedOptionletVolatility::inspectors (base, spread): {"referenceDate", "maxDate", "maxTime", "minStrike",
//                                             "maxStrike", "volatilityType", "displacement", "settlementDays" (or
//                                             {"throws": true}), "businessDayConvention", "allowsExtrapolation"}
//     SpreadedOptionletVolatility::volatility(Time) (base, spread, time, strike, extrapolate)
//     SpreadedOptionletVolatility::volatility(Date) (base, spread, date, strike, extrapolate)
//     SpreadedOptionletVolatility::smileSection     (base, spread, time, [strike, ...]): the section's volatilities
//
// A base is {"kind": "structure", "structure": optionletvolspec's spec} or {"kind": "adapter", "stripper": stripper
// spec, "enableExtrapolation"}.

#include "convert.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"
#include "termstructures/volatility/optionlet/stripperspec.hpp"

#include <ql/errors.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/optionlet/spreadedoptionletvol.hpp>
#include <ql/termstructures/volatility/smilesection.hpp>
#include <ql/termstructures/volatility/optionlet/strippedoptionletadapter.hpp>

#include <cstdint>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct BaseSpec {
            bool adapter = false;
            termstructures::OptionletVolSpec structure;
            strippers::StripperSpec stripper;
            bool enableExtrapolation = false;
        };

        BaseSpec base(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a spreaded volatility's base, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const json::value* kind = o.if_contains("kind");
            if (kind == nullptr)
                throw InvalidParams("a spreaded volatility's base needs kind");
            BaseSpec b;
            std::string k = text(*kind);
            if (k == "structure") {
                const json::value* s = o.if_contains("structure");
                if (s == nullptr || o.size() != 2)
                    throw InvalidParams("a structure base is {kind, structure}");
                b.structure = termstructures::optionletVolatility(*s);
            } else if (k == "adapter") {
                const json::value* s = o.if_contains("stripper");
                const json::value* e = o.if_contains("enableExtrapolation");
                if (s == nullptr || e == nullptr || o.size() != 3)
                    throw InvalidParams("an adapter base is {kind, stripper, enableExtrapolation}");
                b.adapter = true;
                b.stripper = strippers::stripper(*s);
                b.enableExtrapolation = boolean(*e);
            } else {
                throw InvalidParams("unknown base kind " + k);
            }
            return b;
        }

        ext::shared_ptr<SpreadedOptionletVolatility> make(const BaseSpec& b, Real spread) {
            ext::shared_ptr<OptionletVolatilityStructure> structure;
            if (b.adapter) {
                structure = ext::make_shared<StrippedOptionletAdapter>(strippers::makeStripper(b.stripper));
                if (b.enableExtrapolation)
                    structure->enableExtrapolation();
            } else {
                structure = termstructures::makeOptionletVolatility(b.structure);
            }
            return ext::make_shared<SpreadedOptionletVolatility>(
                Handle<OptionletVolatilityStructure>(structure), Handle<Quote>(ext::make_shared<SimpleQuote>(spread)));
        }

        json::value attempt(json::value (*f)(const SpreadedOptionletVolatility&), const SpreadedOptionletVolatility& v) {
            try {
                return f(v);
            } catch (const Error&) {
                return json::object{{"throws", true}};
            }
        }

        const Register unit({
            {"SpreadedOptionletVolatility::inspectors",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 BaseSpec b = base(a[0]);
                 Real spread = real(a[1]);
                 return [=] {
                     ext::shared_ptr<SpreadedOptionletVolatility> v = make(b, spread);
                     return json::value(json::object{
                         {"referenceDate", result(v->referenceDate())},
                         {"maxDate", result(v->maxDate())},
                         {"maxTime", v->maxTime()},
                         {"minStrike", v->minStrike()},
                         {"maxStrike", v->maxStrike()},
                         {"volatilityType", static_cast<std::int64_t>(v->volatilityType())},
                         {"displacement", v->displacement()},
                         {"settlementDays", attempt([](const SpreadedOptionletVolatility& x) {
                              return json::value(static_cast<std::int64_t>(x.settlementDays()));
                          }, *v)},
                         {"businessDayConvention", static_cast<std::int64_t>(v->businessDayConvention())},
                         {"allowsExtrapolation", v->allowsExtrapolation()},
                     });
                 };
             }},
            {"SpreadedOptionletVolatility::volatility(Time)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 BaseSpec b = base(a[0]);
                 Real spread = real(a[1]);
                 Time t = real(a[2]);
                 Rate k = real(a[3]);
                 bool e = boolean(a[4]);
                 return [=] { return json::value(make(b, spread)->volatility(t, k, e)); };
             }},
            {"SpreadedOptionletVolatility::volatility(Date)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 BaseSpec b = base(a[0]);
                 Real spread = real(a[1]);
                 DateSpec d = date(a[2]);
                 Rate k = real(a[3]);
                 bool e = boolean(a[4]);
                 return [=] { return json::value(make(b, spread)->volatility(makeDate(d), k, e)); };
             }},
            {"SpreadedOptionletVolatility::smileSection",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 BaseSpec b = base(a[0]);
                 Real spread = real(a[1]);
                 Time t = real(a[2]);
                 std::vector<Rate> strikes = reals(a[3]);
                 return [=] {
                     ext::shared_ptr<SmileSection> section = make(b, spread)->smileSection(t, true);
                     json::array vols;
                     for (Rate k : strikes) {
                         try {
                             vols.push_back(json::value(section->volatility(k)));
                         } catch (const Error&) {
                             vols.push_back(json::object{{"throws", true}});
                         }
                     }
                     return json::value(vols);
                 };
             }},
        });

    }

}
