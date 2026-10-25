// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/meshers/fdmblackscholesmultistrikemesher.hpp, .cpp: the log-spot mesher concentrated at
// several strikes, which the wheel doesn't expose. "FdmBlackScholesMultiStrikeMesher::outputs" takes (size, spot,
// riskFreeRate, dividendYield, volatility, referenceDate, dayCounter, maturity, strikes, eps, scaleFactor, cPoint): a
// generalized Black-Scholes process on flat forwards and a constant volatility from the reference date, the mesher on
// it, cPoint [point, density] or null for QuantLib's (Null<Real>(), Null<Real>()). It returns {"locations", "dplus",
// "dminus"}, QuantLib's null distances at the ends as null.

#include "convert.hpp"

#include <ql/methods/finitedifferences/meshers/fdmblackscholesmultistrikemesher.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>

#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        json::value nullable(Real x) {
            return x == Null<Real>() ? json::value(nullptr) : json::value(x);
        }

        const Register unit({
            {"FdmBlackScholesMultiStrikeMesher::outputs",
             [](const Args& a) -> Call {
                 arity(a, 12);
                 Integer size = integer(a[0]);
                 if (size < 0)
                     throw InvalidParams("a size must be non-negative");
                 Real spot = real(a[1]), r = real(a[2]), q = real(a[3]), vol = real(a[4]);
                 DateSpec reference = date(a[5]);
                 DayCounterSpec dc = dayCounter(a[6]);
                 Real maturity = real(a[7]);
                 std::vector<Real> strikes = reals(a[8]);
                 Real eps = real(a[9]), scaleFactor = real(a[10]);
                 std::pair<Real, Real> cPoint{Null<Real>(), Null<Real>()};
                 if (!a[11].is_null()) {
                     std::vector<Real> c = reals(a[11]);
                     if (c.size() != 2)
                         throw InvalidParams("a cPoint is [point, density]");
                     cPoint = std::make_pair(c[0], c[1]);
                 }
                 return [=] {
                     Date d = makeDate(reference);
                     DayCounter c = makeDayCounter(dc);
                     auto process = ext::make_shared<GeneralizedBlackScholesProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, q, c)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(d, r, c)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(d, NullCalendar(), vol, c)));
                     FdmBlackScholesMultiStrikeMesher mesher(static_cast<Size>(size), process, maturity, strikes, eps,
                                                             scaleFactor, cPoint);
                     json::array locations, dplus, dminus;
                     for (Size i = 0; i < mesher.size(); ++i) {
                         locations.push_back(json::value(mesher.location(i)));
                         dplus.push_back(nullable(mesher.dplus(i)));
                         dminus.push_back(nullable(mesher.dminus(i)));
                     }
                     json::object out;
                     out["locations"] = locations;
                     out["dplus"] = dplus;
                     out["dminus"] = dminus;
                     return json::value(out);
                 };
             }},
        });

    }

}
