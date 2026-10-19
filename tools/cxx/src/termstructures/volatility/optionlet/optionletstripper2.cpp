// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/optionlet/optionletstripper2.hpp: OptionletStripper2, which the wheel doesn't bind, on an
// OptionletStripper1 of a stripper spec of stripperspec.hpp and an at-the-money CapFloorTermVolCurve of a curve spec of
// capfloor/capfloorvolspec.hpp, both built inside each call:
//
//     OptionletStripper2::strip (stripper, curve): {"atmCapFloorStrikes", "atmCapFloorPrices", "spreadsVol",
//                               "optionletStrikes" and "optionletVolatilities" (a row per date, the at-the-money
//                               strikes inserted), "optionletFixingDates", "optionletFixingTimes", "atmOptionletRates"};
//                               QuantLib's refusal, at the first query, where it refuses the curve

#include "convert.hpp"
#include "termstructures/volatility/capfloor/capfloorvolspec.hpp"
#include "termstructures/volatility/optionlet/stripperspec.hpp"

#include <ql/termstructures/volatility/optionlet/optionletstripper2.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value numbers(const std::vector<Real>& xs) {
            json::array out;
            for (Real x : xs)
                out.push_back(json::value(x));
            return out;
        }

        const Register unit({
            {"OptionletStripper2::strip",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 strippers::StripperSpec s = strippers::stripper(a[0]);
                 capfloorvols::SurfaceSpec c = capfloorvols::curve(a[1]);
                 return [=] {
                     auto stripper2 = ext::make_shared<OptionletStripper2>(
                         strippers::makeStripper(s), Handle<CapFloorTermVolCurve>(capfloorvols::makeCurve(c)));
                     json::array strikes, vols, dates;
                     for (Size i = 0; i < stripper2->optionletMaturities(); ++i) {
                         strikes.push_back(numbers(stripper2->optionletStrikes(i)));
                         vols.push_back(numbers(stripper2->optionletVolatilities(i)));
                     }
                     for (const Date& d : stripper2->optionletFixingDates())
                         dates.push_back(result(d));
                     return json::value(json::object{
                         {"atmCapFloorStrikes", numbers(stripper2->atmCapFloorStrikes())},
                         {"atmCapFloorPrices", numbers(stripper2->atmCapFloorPrices())},
                         {"spreadsVol", numbers(stripper2->spreadsVol())},
                         {"optionletStrikes", strikes},
                         {"optionletVolatilities", vols},
                         {"optionletFixingDates", dates},
                         {"optionletFixingTimes", numbers(stripper2->optionletFixingTimes())},
                         {"atmOptionletRates", numbers(stripper2->atmOptionletRates())},
                     });
                 };
             }},
        });

    }

}
