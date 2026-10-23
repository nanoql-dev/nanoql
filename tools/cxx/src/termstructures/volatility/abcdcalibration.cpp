// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/abcdcalibration.hpp, .cpp: AbcdCalibration, which the wheel doesn't bind, built inside
// each call from an abcd fit spec of abcdspec.hpp, its guesses the constructor's.
//
//     AbcdCalibration::guess     (fit, points)    {"value", "error", "maxError", "errors", "k"}: the members at the
//                                                 guesses, before compute()
//     AbcdCalibration::compute   (fit, points)    {"a", "b", "c", "d", "error", "maxError", "errors", "endCriteria",
//                                                 "k", "value"}: the members after compute()
//
// "value" is value(x) at each point, "k" k(t, blackVols) on the fit's own times and volatilities, "endCriteria"
// EndCriteria::Type's integer.

#include "convert.hpp"
#include "termstructures/volatility/abcdspec.hpp"

#include <ql/termstructures/volatility/abcdcalibration.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        AbcdCalibration makeCalibration(const abcd::FitSpec& s) {
            return AbcdCalibration(s.t, s.blackVols, s.guess[0], s.guess[1], s.guess[2], s.guess[3], s.fixed[0],
                                   s.fixed[1], s.fixed[2], s.fixed[3], s.vegaWeighted, abcd::makeEndCriteria(s),
                                   abcd::makeMethod(s));
        }

        json::value values(const AbcdCalibration& c, const std::vector<Real>& points) {
            std::vector<Real> out;
            for (Real x : points)
                out.push_back(c.value(x));
            return abcd::realArray(out);
        }

        json::value errors(const AbcdCalibration& c) {
            Array e = c.errors();
            return abcd::realArray(std::vector<Real>(e.begin(), e.end()));
        }

        const Register unit({
            {"AbcdCalibration::guess",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 abcd::FitSpec s = abcd::fit(a[0]);
                 std::vector<Real> points = reals(a[1]);
                 return [=] {
                     AbcdCalibration c = makeCalibration(s);
                     return json::value(json::object{{"value", values(c, points)},
                                                     {"error", c.error()},
                                                     {"maxError", c.maxError()},
                                                     {"errors", errors(c)},
                                                     {"k", abcd::realArray(c.k(s.t, s.blackVols))}});
                 };
             }},
            {"AbcdCalibration::compute",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 abcd::FitSpec s = abcd::fit(a[0]);
                 std::vector<Real> points = reals(a[1]);
                 return [=] {
                     AbcdCalibration c = makeCalibration(s);
                     c.compute();
                     return json::value(json::object{{"a", c.a()},
                                                     {"b", c.b()},
                                                     {"c", c.c()},
                                                     {"d", c.d()},
                                                     {"error", c.error()},
                                                     {"maxError", c.maxError()},
                                                     {"errors", errors(c)},
                                                     {"endCriteria", static_cast<int>(c.endCriteria())},
                                                     {"k", abcd::realArray(c.k(s.t, s.blackVols))},
                                                     {"value", values(c, points)}});
                 };
             }},
        });

    }

}
