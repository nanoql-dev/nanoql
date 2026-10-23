// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/interpolations/abcdinterpolation.hpp and ql/termstructures/volatility/abcd.hpp: AbcdInterpolation and
// AbcdSquared, which the wheel doesn't bind, built inside each call. An interpolation is built from an abcd fit spec of
// termstructures/volatility/abcdspec.hpp on its times and volatilities, a null guess QuantLib's Null<Real>() (its
// default guess, the parameter left free).
//
//     AbcdInterpolation          (fit, points, kPoints)    {"a", "b", "c", "d", "k", "rmsError", "maxError",
//                                                          "endCriteria", "value", "kAt"}: the members after the fit,
//                                                          "value" the interpolation at each point, extrapolating,
//                                                          and "kAt" k(t, xBegin, xEnd) on the fit's times at each
//                                                          of kPoints
//     AbcdInterpolation::value   (fit, x)         a real: the interpolation at x, extrapolating
//     AbcdSquared                (a, b, c, d, T, S, ts)    [AbcdSquared(a, b, c, d, T, S)(t) for each t]
//     Abcd::traits               ()               {"global", "requiredPoints"}: the interpolator factory's traits

#include "convert.hpp"
#include "termstructures/volatility/abcdspec.hpp"

#include <ql/math/interpolations/abcdinterpolation.hpp>
#include <ql/termstructures/volatility/abcd.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        AbcdInterpolation makeInterpolation(const abcd::FitSpec& s) {
            return AbcdInterpolation(s.t.begin(), s.t.end(), s.blackVols.begin(), s.guess[0], s.guess[1], s.guess[2],
                                     s.guess[3], s.fixed[0], s.fixed[1], s.fixed[2], s.fixed[3], s.vegaWeighted,
                                     abcd::makeEndCriteria(s), abcd::makeMethod(s));
        }

        const Register unit({
            {"AbcdInterpolation",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 abcd::FitSpec s = abcd::fit(a[0]);
                 std::vector<Real> points = reals(a[1]);
                 std::vector<Real> kPoints = reals(a[2]);
                 return [=] {
                     // the interpolation reads the spec's vectors, which outlive it here
                     AbcdInterpolation f = makeInterpolation(s);
                     std::vector<Real> values, ks;
                     for (Real x : points)
                         values.push_back(f(x, true));
                     for (Real x : kPoints)
                         ks.push_back(f.k(x, s.t.begin(), s.t.end()));
                     return json::value(json::object{{"a", f.a()},
                                                     {"b", f.b()},
                                                     {"c", f.c()},
                                                     {"d", f.d()},
                                                     {"k", abcd::realArray(f.k())},
                                                     {"rmsError", f.rmsError()},
                                                     {"maxError", f.maxError()},
                                                     {"endCriteria", static_cast<int>(f.endCriteria())},
                                                     {"value", abcd::realArray(values)},
                                                     {"kAt", abcd::realArray(ks)}});
                 };
             }},
            {"AbcdInterpolation::value",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 abcd::FitSpec s = abcd::fit(a[0]);
                 Real x = real(a[1]);
                 return [=] { return json::value(makeInterpolation(s)(x, true)); };
             }},
            {"Abcd::traits",
             [](const Args& a) -> Call {
                 arity(a, 0);
                 return [] {
                     return json::value(json::object{{"global", Abcd::global},
                                                     {"requiredPoints", static_cast<std::int64_t>(Abcd::requiredPoints)}});
                 };
             }},
            {"AbcdSquared",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Real pa = real(a[0]), pb = real(a[1]), pc = real(a[2]), pd = real(a[3]), T = real(a[4]),
                      S = real(a[5]);
                 std::vector<Real> ts = reals(a[6]);
                 return [=] {
                     AbcdSquared f(pa, pb, pc, pd, T, S);
                     std::vector<Real> out;
                     for (Real t : ts)
                         out.push_back(f(t));
                     return abcd::realArray(out);
                 };
             }},
        });

    }

}
