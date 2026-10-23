// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/atmsmilesection.hpp, .cpp and atmadjustedsmilesection.hpp, .cpp: AtmSmileSection and
// AtmAdjustedSmileSection, which the wheel doesn't expose, over a source section spec (smilesectionspec.hpp), both
// built inside the call. A section is {"source": <section spec>, "atm": <real or null>}, null for Null<Real>(), and the
// adjusted section adds "recenterSmile". Every member QuantLib forwards to the source, the ATM level, the volatility
// and the variance; and the option and digital prices, the vega and the density, which AtmSmileSection takes from
// SmileSection's formulas and AtmAdjustedSmileSection from the source at the adjusted strike.
//
//     <Class>::minStrike, maxStrike, atmLevel, exerciseDate, exerciseTime, dayCounter, referenceDate,
//              volatilityType, shift                                   (spec)
//     <Class>::volatility, variance                                    (spec, strike)
//     <Class>::optionPrice                                             (spec, strike, option type, discount)
//     <Class>::digitalOptionPrice                                      (spec, strike, option type, discount, gap)
//     <Class>::vega                                                    (spec, strike, discount)
//     <Class>::density                                                 (spec, strike, discount, gap)

#include "convert.hpp"
#include "termstructures/volatility/smilesectionspec.hpp"

#include <ql/option.hpp>
#include <ql/termstructures/volatility/atmadjustedsmilesection.hpp>
#include <ql/termstructures/volatility/atmsmilesection.hpp>

#include <functional>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::makeSmileSection;
        using termstructures::nullableReal;
        using termstructures::SmileSectionSpec;

        struct AtmSpec {
            SmileSectionSpec source;
            Real atm = Null<Real>();
            bool adjusted = false;
            bool recenterSmile = false;
        };

        AtmSpec atmSpec(const json::value& v, bool adjusted) {
            if (!v.is_object())
                throw InvalidParams("expected an ATM smile section spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const json::value* source = o.if_contains("source");
            const json::value* atm = o.if_contains("atm");
            const json::value* recenter = o.if_contains("recenterSmile");
            if (source == nullptr || atm == nullptr || (adjusted && recenter == nullptr) ||
                o.size() != (adjusted ? 3U : 2U))
                throw InvalidParams(std::string("an ATM smile section spec is {source, atm") +
                                    (adjusted ? ", recenterSmile}: " : "}: ") + json::serialize(v));
            AtmSpec s;
            s.source = termstructures::smileSection(*source);
            s.atm = real(*atm);
            s.adjusted = adjusted;
            s.recenterSmile = adjusted && boolean(*recenter);
            return s;
        }

        ext::shared_ptr<SmileSection> make(const AtmSpec& s) {
            ext::shared_ptr<SmileSection> source = makeSmileSection(s.source);
            if (s.adjusted)
                return ext::make_shared<AtmAdjustedSmileSection>(source, s.atm, s.recenterSmile);
            return ext::make_shared<AtmSmileSection>(source, s.atm);
        }

        using Whole = std::function<json::value(const SmileSection&)>;
        using AtStrike = std::function<Real(const SmileSection&, Real)>;

        Method whole(bool adjusted, Whole f) {
            return [adjusted, f](const Args& a) -> Call {
                arity(a, 1);
                AtmSpec s = atmSpec(a[0], adjusted);
                return [=] { return f(*make(s)); };
            };
        }

        Method atStrike(bool adjusted, AtStrike f) {
            return [adjusted, f](const Args& a) -> Call {
                arity(a, 2);
                AtmSpec s = atmSpec(a[0], adjusted);
                Real k = real(a[1]);
                return [=] { return json::value(f(*make(s), k)); };
            };
        }

        // The member by its name, on either class.
        Method member(bool adjusted, const std::string& name) {
            if (name == "minStrike")
                return whole(adjusted, [](const SmileSection& s) { return json::value(s.minStrike()); });
            if (name == "maxStrike")
                return whole(adjusted, [](const SmileSection& s) { return json::value(s.maxStrike()); });
            if (name == "atmLevel")
                return whole(adjusted, [](const SmileSection& s) { return nullableReal(s.atmLevel()); });
            if (name == "exerciseDate")
                return whole(adjusted, [](const SmileSection& s) { return result(s.exerciseDate()); });
            if (name == "exerciseTime")
                return whole(adjusted, [](const SmileSection& s) { return json::value(s.exerciseTime()); });
            // The day counter's name, or null for QuantLib's empty one.
            if (name == "dayCounter")
                return whole(adjusted, [](const SmileSection& s) {
                    return s.dayCounter().empty() ? json::value(nullptr) : json::value(s.dayCounter().name());
                });
            if (name == "referenceDate")
                return whole(adjusted, [](const SmileSection& s) { return result(s.referenceDate()); });
            if (name == "volatilityType")
                return whole(adjusted,
                             [](const SmileSection& s) { return json::value(static_cast<int>(s.volatilityType())); });
            if (name == "shift")
                return whole(adjusted, [](const SmileSection& s) { return json::value(s.shift()); });
            if (name == "volatility")
                return atStrike(adjusted, [](const SmileSection& s, Real k) { return s.volatility(k); });
            if (name == "variance")
                return atStrike(adjusted, [](const SmileSection& s, Real k) { return s.variance(k); });
            // (spec, strike, option type, discount)
            if (name == "optionPrice")
                return [adjusted](const Args& a) -> Call {
                    arity(a, 4);
                    AtmSpec s = atmSpec(a[0], adjusted);
                    Real k = real(a[1]);
                    auto type = enumeration<Option::Type>(a[2]);
                    Real discount = real(a[3]);
                    return [=] { return json::value(make(s)->optionPrice(k, type, discount)); };
                };
            // (spec, strike, option type, discount, gap)
            if (name == "digitalOptionPrice")
                return [adjusted](const Args& a) -> Call {
                    arity(a, 5);
                    AtmSpec s = atmSpec(a[0], adjusted);
                    Real k = real(a[1]);
                    auto type = enumeration<Option::Type>(a[2]);
                    Real discount = real(a[3]);
                    Real gap = real(a[4]);
                    return [=] { return json::value(make(s)->digitalOptionPrice(k, type, discount, gap)); };
                };
            // (spec, strike, discount)
            if (name == "vega")
                return [adjusted](const Args& a) -> Call {
                    arity(a, 3);
                    AtmSpec s = atmSpec(a[0], adjusted);
                    Real k = real(a[1]);
                    Real discount = real(a[2]);
                    return [=] { return json::value(make(s)->vega(k, discount)); };
                };
            // (spec, strike, discount, gap): density
            return [adjusted](const Args& a) -> Call {
                arity(a, 4);
                AtmSpec s = atmSpec(a[0], adjusted);
                Real k = real(a[1]);
                Real discount = real(a[2]);
                Real gap = real(a[3]);
                return [=] { return json::value(make(s)->density(k, discount, gap)); };
            };
        }

        const Register unit({
            {"AtmSmileSection::minStrike", member(false, "minStrike")},
            {"AtmSmileSection::maxStrike", member(false, "maxStrike")},
            {"AtmSmileSection::atmLevel", member(false, "atmLevel")},
            {"AtmSmileSection::exerciseDate", member(false, "exerciseDate")},
            {"AtmSmileSection::exerciseTime", member(false, "exerciseTime")},
            {"AtmSmileSection::dayCounter", member(false, "dayCounter")},
            {"AtmSmileSection::referenceDate", member(false, "referenceDate")},
            {"AtmSmileSection::volatilityType", member(false, "volatilityType")},
            {"AtmSmileSection::shift", member(false, "shift")},
            {"AtmSmileSection::volatility", member(false, "volatility")},
            {"AtmSmileSection::variance", member(false, "variance")},
            {"AtmSmileSection::optionPrice", member(false, "optionPrice")},
            {"AtmSmileSection::digitalOptionPrice", member(false, "digitalOptionPrice")},
            {"AtmSmileSection::vega", member(false, "vega")},
            {"AtmSmileSection::density", member(false, "density")},
            {"AtmAdjustedSmileSection::minStrike", member(true, "minStrike")},
            {"AtmAdjustedSmileSection::maxStrike", member(true, "maxStrike")},
            {"AtmAdjustedSmileSection::atmLevel", member(true, "atmLevel")},
            {"AtmAdjustedSmileSection::exerciseDate", member(true, "exerciseDate")},
            {"AtmAdjustedSmileSection::exerciseTime", member(true, "exerciseTime")},
            {"AtmAdjustedSmileSection::dayCounter", member(true, "dayCounter")},
            {"AtmAdjustedSmileSection::referenceDate", member(true, "referenceDate")},
            {"AtmAdjustedSmileSection::volatilityType", member(true, "volatilityType")},
            {"AtmAdjustedSmileSection::shift", member(true, "shift")},
            {"AtmAdjustedSmileSection::volatility", member(true, "volatility")},
            {"AtmAdjustedSmileSection::variance", member(true, "variance")},
            {"AtmAdjustedSmileSection::optionPrice", member(true, "optionPrice")},
            {"AtmAdjustedSmileSection::digitalOptionPrice", member(true, "digitalOptionPrice")},
            {"AtmAdjustedSmileSection::vega", member(true, "vega")},
            {"AtmAdjustedSmileSection::density", member(true, "density")},
        });

    }

}
