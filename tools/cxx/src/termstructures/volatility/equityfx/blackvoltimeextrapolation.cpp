// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/equityfx/blackvoltimeextrapolation.hpp: BlackVolTimeExtrapolation's two
// extrapolatedVariance overloads, on the harness's own variances, as tests/NanoQL.Tests/Vectors/Cxx/TermStructures/
// BlackVolTimeExtrapolationCxxVectorTests.cs defines them: a surface w(t, k) = t s(k)^2 + c t^2, with s(k) = sigma0 +
// slope (k - 100) / 100, and a curve w(t) = t sigma0^2 + c t^2. "BlackVolTimeExtrapolation::extrapolatedVariance"
// takes (type, t, strike, times, [sigma0, slope, c]); "BlackVolTimeExtrapolation::extrapolatedVarianceCurve" takes
// (type, t, times, [sigma0, c]). No QuantLib class calls the surface overload; the curve overload is the one
// BlackVarianceCurve calls.

#include "convert.hpp"

#include <ql/termstructures/volatility/equityfx/blackvoltimeextrapolation.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            // (type, t, strike, times, [sigma0, slope, c])
            {"BlackVolTimeExtrapolation::extrapolatedVariance",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 auto type = enumeration<BlackVolTimeExtrapolation::Type>(a[0]);
                 Real t = real(a[1]);
                 Real strike = real(a[2]);
                 std::vector<Real> times = reals(a[3]);
                 std::vector<Real> p = reals(a[4]);
                 if (p.size() != 3)
                     throw InvalidParams("the surface takes [sigma0, slope, c]");
                 return [=] {
                     auto w = [p](Time u, Real k) {
                         Real s = p[0] + p[1] * (k - 100.0) / 100.0;
                         return u * s * s + p[2] * u * u;
                     };
                     return json::value(BlackVolTimeExtrapolation::extrapolatedVariance(type, t, strike, times, w));
                 };
             }},
            // (type, t, times, [sigma0, c])
            {"BlackVolTimeExtrapolation::extrapolatedVarianceCurve",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 auto type = enumeration<BlackVolTimeExtrapolation::Type>(a[0]);
                 Real t = real(a[1]);
                 std::vector<Real> times = reals(a[2]);
                 std::vector<Real> p = reals(a[3]);
                 if (p.size() != 2)
                     throw InvalidParams("the curve takes [sigma0, c]");
                 return [=] {
                     auto w = [p](Time u) { return u * p[0] * p[0] + p[1] * u * u; };
                     return json::value(BlackVolTimeExtrapolation::extrapolatedVariance(type, t, times, w));
                 };
             }},
        });

    }

}
