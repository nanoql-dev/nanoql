// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/volatility/sabrvolsurface.hpp, .cpp, which the wheel doesn't expose. "SabrVolSurface::values" takes
// (indexMonths, rate, tenors, atmVols, atmRateSpreads, volSpreads, spreadDates, queryTimes, strikeOffsets): the surface
// on a Euribor of indexMonths months forecast on a flat rate (continuous, Actual/365 (Fixed)) from the evaluation date,
// its at-the-money curve an AbcdAtmVolCurve on TARGET from the evaluation date at the option tenors (each [length,
// TimeUnit]), the strike spreads and a quote per volatility spread, a row per option tenor; it returns {"maxDate",
// "minStrike", "maxStrike", "optionDates", "volatilitySpreadsAtTenor", "volatilitySpreads", "atmLevel", "exerciseDate",
// "volatility", "atmVol"}: the surface's strike range, the index option dates of its tenors and its volatility spreads
// at each (the period overload), its volatility spreads at each spread date (a serial), and at each query time its smile
// section's at-the-money level, exercise date and volatilities at the level plus each offset, and the surface's
// at-the-money volatility.

#include "convert.hpp"

#include <ql/experimental/volatility/abcdatmvolcurve.hpp>
#include <ql/experimental/volatility/sabrvolsurface.hpp>
#include <ql/indexes/ibor/euribor.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/target.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        std::vector<Period> tenorsOf(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected a list of [length, unit], got " + json::serialize(v));
            std::vector<Period> out;
            for (const json::value& p : v.get_array()) {
                if (!p.is_array() || p.get_array().size() != 2)
                    throw InvalidParams("expected [length, unit], got " + json::serialize(p));
                out.emplace_back(integer(p.get_array()[0]), enumeration<TimeUnit>(p.get_array()[1]));
            }
            return out;
        }

        json::array array(const std::vector<Real>& x) {
            json::array out;
            for (Real xi : x)
                out.emplace_back(xi);
            return out;
        }

        const Register unit({
            {"SabrVolSurface::values",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 const Integer indexMonths = integer(a[0]);
                 const Real rate = real(a[1]);
                 const std::vector<Period> tenors = tenorsOf(a[2]);
                 const std::vector<Real> atmVols = reals(a[3]);
                 const std::vector<Real> spreads = reals(a[4]);
                 if (!a[5].is_array())
                     throw InvalidParams("expected the volatility spreads' rows, got " + json::serialize(a[5]));
                 std::vector<std::vector<Real>> volSpreads;
                 for (const json::value& row : a[5].get_array())
                     volSpreads.push_back(reals(row));
                 // QuantLib reads a row per option tenor, unchecked.
                 if (volSpreads.size() < tenors.size())
                     throw InvalidParams("the volatility spreads need a row per option tenor");
                 const std::vector<Integer> spreadDates = integers(a[6]);
                 const std::vector<Real> queryTimes = reals(a[7]);
                 const std::vector<Real> offsets = reals(a[8]);
                 return [=] {
                     const Date today = Settings::instance().evaluationDate();
                     Handle<YieldTermStructure> curve(
                         ext::make_shared<FlatForward>(today, rate, Actual365Fixed()));
                     auto index = ext::make_shared<Euribor>(Period(indexMonths, Months), curve);
                     std::vector<Handle<Quote>> atmQuotes;
                     for (Real v : atmVols)
                         atmQuotes.emplace_back(ext::make_shared<SimpleQuote>(v));
                     Handle<BlackAtmVolCurve> atm(ext::make_shared<AbcdAtmVolCurve>(0, TARGET(), tenors, atmQuotes));
                     std::vector<std::vector<Handle<Quote>>> volQuotes;
                     for (const auto& row : volSpreads) {
                         std::vector<Handle<Quote>> q;
                         for (Real v : row)
                             q.emplace_back(ext::make_shared<SimpleQuote>(v));
                         volQuotes.push_back(q);
                     }
                     SabrVolSurface surface(index, atm, tenors, spreads, volQuotes);

                     json::object out;
                     out["maxDate"] = result(surface.maxDate());
                     out["minStrike"] = json::value(surface.minStrike());
                     out["maxStrike"] = json::value(surface.maxStrike());
                     json::array optionDates, spreadsAtTenor, spreadsAt, atmLevel, exerciseDate, volatility, atmVol;
                     for (const Period& p : tenors) {
                         optionDates.push_back(result(surface.optionDateFromTenor(p)));
                         spreadsAtTenor.push_back(json::value(array(surface.volatilitySpreads(p))));
                     }
                     for (Integer s : spreadDates)
                         spreadsAt.push_back(json::value(array(surface.volatilitySpreads(Date(Date::serial_type(s))))));
                     for (Time t : queryTimes) {
                         ext::shared_ptr<SmileSection> section = surface.smileSection(t, true);
                         Real level = section->atmLevel();
                         atmLevel.push_back(json::value(level));
                         exerciseDate.push_back(result(section->exerciseDate()));
                         json::array row;
                         for (Real k : offsets)
                             row.push_back(json::value(section->volatility(level + k)));
                         volatility.push_back(row);
                         atmVol.push_back(json::value(surface.atmVol(t, true)));
                     }
                     out["optionDates"] = optionDates;
                     out["volatilitySpreadsAtTenor"] = spreadsAtTenor;
                     out["volatilitySpreads"] = spreadsAt;
                     out["atmLevel"] = atmLevel;
                     out["exerciseDate"] = exerciseDate;
                     out["volatility"] = volatility;
                     out["atmVol"] = atmVol;
                     return json::value(out);
                 };
             }},
        });

    }

}
