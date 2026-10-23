// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/yield/nonlinearfittingmethods.hpp, .cpp's NaturalCubicFitting, which the wheel doesn't expose: its
// discount function, through a FittedBondDiscountCurve that evaluates it on given parameters. "NaturalCubicFitting::
// discount" takes (knotTimes, parameters, minCutoffTime, maxCutoffTime, referenceDate, maxDate, times), the cut-offs
// null for QuantLib's defaults (0 and QL_MAX_REAL), the curve on Actual/365 (Fixed), and returns {"size", "discount":
// the curve's discount at each time}; a construction or a parameter count QuantLib refuses throws.

#include "convert.hpp"

#include <ql/math/array.hpp>
#include <ql/termstructures/yield/fittedbonddiscountcurve.hpp>
#include <ql/termstructures/yield/nonlinearfittingmethods.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"NaturalCubicFitting::discount",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::vector<Real> knots = reals(a[0]), params = reals(a[1]), times = reals(a[6]);
                 Real lo = a[2].is_null() ? 0.0 : real(a[2]);
                 Real hi = a[3].is_null() ? QL_MAX_REAL : real(a[3]);
                 DateSpec reference = date(a[4]), maxDate = date(a[5]);
                 return [=] {
                     NaturalCubicFitting method(knots, Array(), {}, Array(), lo, hi);
                     FittedBondDiscountCurve curve(makeDate(reference), method,
                                                   Array(params.begin(), params.end()), makeDate(maxDate),
                                                   Actual365Fixed());
                     json::array discount;
                     for (Real t : times)
                         discount.push_back(json::value(curve.discount(t)));
                     json::object out;
                     out["size"] = json::value(static_cast<std::int64_t>(curve.fitResults().size()));
                     out["discount"] = discount;
                     return json::value(out);
                 };
             }},
        });

    }

}
