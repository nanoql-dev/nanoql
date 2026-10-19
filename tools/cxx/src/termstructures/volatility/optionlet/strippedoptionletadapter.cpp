// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/optionlet/strippedoptionletadapter.hpp: the members of StrippedOptionletAdapter the
// wheel doesn't bind, on an adapter built inside each call on an OptionletStripper1 of a stripper spec of
// stripperspec.hpp, with extrapolation enabled when the call says so:
//
//     StrippedOptionletAdapter::smileSection      (stripper, enableExtrapolation, time, [strike, ...]):
//                                                 {"exerciseTime", "minStrike", "maxStrike", "volatilityType",
//                                                 "shift", "volatilities" (one per strike, or {"throws": true})}
//     StrippedOptionletAdapter::volatility(Period) (stripper, enableExtrapolation, [length, units], strike,
//                                                 extrapolate)
//
// A section's atmLevel() isn't read: the adapter builds its sections on Null<Real>(), whose SimpleQuote refuses the
// read, "invalid SimpleQuote" (tests/NanoQL.Tests/Divergences/NullRealSentinelTests.cs).

#include "convert.hpp"
#include "termstructures/volatility/capfloor/capfloorvolspec.hpp"
#include "termstructures/volatility/optionlet/stripperspec.hpp"

#include <ql/errors.hpp>
#include <ql/termstructures/volatility/optionlet/strippedoptionletadapter.hpp>
#include <ql/termstructures/volatility/smilesection.hpp>
#include <ql/time/period.hpp>

#include <cstdint>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<StrippedOptionletAdapter> adapter(const strippers::StripperSpec& s, bool extrapolate) {
            auto a = ext::make_shared<StrippedOptionletAdapter>(strippers::makeStripper(s));
            if (extrapolate)
                a->enableExtrapolation();
            return a;
        }

        const Register unit({
            {"StrippedOptionletAdapter::smileSection",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 strippers::StripperSpec s = strippers::stripper(a[0]);
                 bool extrapolate = boolean(a[1]);
                 Time t = real(a[2]);
                 std::vector<Rate> strikes = reals(a[3]);
                 return [=] {
                     ext::shared_ptr<SmileSection> section = adapter(s, extrapolate)->smileSection(t);
                     json::array vols;
                     for (Rate k : strikes) {
                         try {
                             vols.push_back(json::value(section->volatility(k)));
                         } catch (const Error&) {
                             vols.push_back(json::object{{"throws", true}});
                         }
                     }
                     return json::value(json::object{
                         {"exerciseTime", section->exerciseTime()},
                         {"minStrike", section->minStrike()},
                         {"maxStrike", section->maxStrike()},
                         {"volatilityType", static_cast<std::int64_t>(section->volatilityType())},
                         {"shift", section->shift()},
                         {"volatilities", vols},
                     });
                 };
             }},
            {"StrippedOptionletAdapter::volatility(Period)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 strippers::StripperSpec s = strippers::stripper(a[0]);
                 bool enable = boolean(a[1]);
                 std::pair<Integer, TimeUnit> p = capfloorvols::period(a[2]);
                 Rate k = real(a[3]);
                 bool extrapolate = boolean(a[4]);
                 return [=] {
                     return json::value(adapter(s, enable)->volatility(Period(p.first, p.second), k, extrapolate));
                 };
             }},
        });

    }

}
