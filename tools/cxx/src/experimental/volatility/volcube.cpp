// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/volatility/volcube.hpp, .cpp, which the wheel doesn't expose: what VolatilityCube checks at
// construction. "VolatilityCube::build" takes (surfaceSettlementDays, curveSettlementDays): a cube of SABR surfaces
// on Euribor 6M forecast on a flat 3% (continuous, Actual/365 (Fixed)) from the evaluation date, each over an abcd
// at-the-money curve on TARGET at 1M, 3M, 6M, 1Y and 2Y (0.20, 0.24, 0.26, 0.255, 0.24) with the given settlement days,
// and abcd curves with the given settlement days, one surface or curve per entry; it returns {"surfaces", "curves"},
// the counts of the cube's surfaces() and curves(), where QuantLib builds it.

#include "convert.hpp"

#include <ql/experimental/volatility/abcdatmvolcurve.hpp>
#include <ql/experimental/volatility/sabrvolsurface.hpp>
#include <ql/experimental/volatility/volcube.hpp>
#include <ql/indexes/ibor/euribor.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/target.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        ext::shared_ptr<AbcdAtmVolCurve> curve(Natural settlementDays, const std::vector<Period>& tenors) {
            std::vector<Handle<Quote>> quotes;
            for (Real v : {0.20, 0.24, 0.26, 0.255, 0.24})
                quotes.emplace_back(ext::make_shared<SimpleQuote>(v));
            return ext::make_shared<AbcdAtmVolCurve>(settlementDays, TARGET(), tenors, quotes);
        }

        const Register unit({
            {"VolatilityCube::build",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 const std::vector<Integer> surfaceDays = integers(a[0]), curveDays = integers(a[1]);
                 for (Integer d : surfaceDays)
                     if (d < 0)
                         throw InvalidParams("settlement days are a Natural");
                 for (Integer d : curveDays)
                     if (d < 0)
                         throw InvalidParams("settlement days are a Natural");
                 return [=] {
                     const std::vector<Period> tenors = {1 * Months, 3 * Months, 6 * Months, 1 * Years, 2 * Years};
                     Handle<YieldTermStructure> rates(
                         ext::make_shared<FlatForward>(Settings::instance().evaluationDate(), 0.03, Actual365Fixed()));
                     auto index = ext::make_shared<Euribor>(6 * Months, rates);
                     std::vector<Handle<InterestRateVolSurface>> surfaces;
                     for (Integer d : surfaceDays) {
                         std::vector<std::vector<Handle<Quote>>> spreads;
                         for (Size i = 0; i < tenors.size(); ++i) {
                             std::vector<Handle<Quote>> row;
                             for (Real v : {0.01, 0.0, -0.005})
                                 row.emplace_back(ext::make_shared<SimpleQuote>(v));
                             spreads.push_back(row);
                         }
                         surfaces.emplace_back(ext::make_shared<SabrVolSurface>(
                             index, Handle<BlackAtmVolCurve>(curve(static_cast<Natural>(d), tenors)), tenors,
                             std::vector<Spread>{-0.01, 0.0, 0.01}, spreads));
                     }
                     std::vector<Handle<AbcdAtmVolCurve>> curves;
                     for (Integer d : curveDays)
                         curves.emplace_back(curve(static_cast<Natural>(d), tenors));
                     VolatilityCube cube(surfaces, curves);
                     json::object out;
                     out["surfaces"] = json::value(static_cast<std::int64_t>(cube.surfaces().size()));
                     out["curves"] = json::value(static_cast<std::int64_t>(cube.curves().size()));
                     return json::value(out);
                 };
             }},
        });

    }

}
