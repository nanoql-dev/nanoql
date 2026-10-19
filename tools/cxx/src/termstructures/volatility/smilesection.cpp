// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/smilesection.hpp: SmileSection's members on a section spec (smilesectionspec.hpp),
// the section built inside the call so that a constructor's refusal is QuantLib's. The harness's own section keeps the
// base's defaults, which no QuantLib section in the wheel leaves as they are: the variance, the option and digital
// prices, the vega, the density and the converted volatilities over a smile that varies with the strike, and the
// comparison of the converted volatility's type with the type the section was built with. FlatSmileSection's time
// form takes QuantLib's empty day counter here, which the wheel can't build: its dayCounter() is returned as its name,
// or null for an empty one.

#include "convert.hpp"
#include "termstructures/volatility/smilesectionspec.hpp"

#include <ql/option.hpp>
#include <ql/termstructures/volatility/smilesection.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::makeSmileSection;
        using termstructures::nullableReal;
        using termstructures::smileSection;
        using termstructures::SmileSectionSpec;

        // A member of the section alone: (spec).
        Call whole(const Args& a, json::value (*f)(const SmileSection&)) {
            arity(a, 1);
            SmileSectionSpec s = smileSection(a[0]);
            return [=] { return f(*makeSmileSection(s)); };
        }

        const Register unit({
            {"SmileSection::minStrike",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return json::value(s.minStrike()); }); }},
            {"SmileSection::maxStrike",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return json::value(s.maxStrike()); }); }},
            {"SmileSection::atmLevel",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return nullableReal(s.atmLevel()); }); }},
            {"SmileSection::exerciseDate",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return result(s.exerciseDate()); }); }},
            {"SmileSection::referenceDate",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return result(s.referenceDate()); }); }},
            {"SmileSection::exerciseTime",
             [](const Args& a) {
                 return whole(a, [](const SmileSection& s) { return json::value(s.exerciseTime()); });
             }},
            {"SmileSection::volatilityType",
             [](const Args& a) {
                 return whole(a, [](const SmileSection& s) {
                     return json::value(static_cast<int>(s.volatilityType()));
                 });
             }},
            {"SmileSection::shift",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return json::value(s.shift()); }); }},
            // The day counter's name, or null for QuantLib's empty one.
            {"SmileSection::dayCounter",
             [](const Args& a) {
                 return whole(a, [](const SmileSection& s) {
                     return s.dayCounter().empty() ? json::value(nullptr) : json::value(s.dayCounter().name());
                 });
             }},
            {"SmileSection::variance",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 SmileSectionSpec s = smileSection(a[0]);
                 Real k = real(a[1]);
                 return [=] { return json::value(makeSmileSection(s)->variance(k)); };
             }},
            {"SmileSection::volatility(Rate)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 SmileSectionSpec s = smileSection(a[0]);
                 Real k = real(a[1]);
                 return [=] { return json::value(makeSmileSection(s)->volatility(k)); };
             }},
            // (spec, strike, type, shift)
            {"SmileSection::volatility(Rate,VolatilityType,Real)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SmileSectionSpec s = smileSection(a[0]);
                 Real k = real(a[1]);
                 auto type = enumeration<VolatilityType>(a[2]);
                 Real shift = real(a[3]);
                 return [=] { return json::value(makeSmileSection(s)->volatility(k, type, shift)); };
             }},
            // (spec, strike, option type, discount)
            {"SmileSection::optionPrice",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SmileSectionSpec s = smileSection(a[0]);
                 Real k = real(a[1]);
                 auto type = enumeration<Option::Type>(a[2]);
                 Real discount = real(a[3]);
                 return [=] { return json::value(makeSmileSection(s)->optionPrice(k, type, discount)); };
             }},
            // (spec, strike, option type, discount, gap)
            {"SmileSection::digitalOptionPrice",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 SmileSectionSpec s = smileSection(a[0]);
                 Real k = real(a[1]);
                 auto type = enumeration<Option::Type>(a[2]);
                 Real discount = real(a[3]);
                 Real gap = real(a[4]);
                 return [=] { return json::value(makeSmileSection(s)->digitalOptionPrice(k, type, discount, gap)); };
             }},
            // (spec, strike, discount)
            {"SmileSection::vega",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 SmileSectionSpec s = smileSection(a[0]);
                 Real k = real(a[1]);
                 Real discount = real(a[2]);
                 return [=] { return json::value(makeSmileSection(s)->vega(k, discount)); };
             }},
            // (spec, strike, discount, gap)
            {"SmileSection::density",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SmileSectionSpec s = smileSection(a[0]);
                 Real k = real(a[1]);
                 Real discount = real(a[2]);
                 Real gap = real(a[3]);
                 return [=] { return json::value(makeSmileSection(s)->density(k, discount, gap)); };
             }},
        });

    }

}
