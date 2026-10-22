// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/processes/squarerootprocess.hpp, .cpp: SquareRootProcess, which the wheel doesn't expose, on its default Euler
// discretization. "SquareRootProcess" takes (b, a, sigma, x0, member, t, x, dt, dw) and returns the member's value:
// "x0", "a", "b" and "sigma" read no point; "drift" and "diffusion" read (t, x); "expectation", "stdDeviation" and
// "variance" (t, x, dt); "evolve" (t, x, dt, dw). x0 null takes the constructor's default.

#include "convert.hpp"

#include <ql/processes/squarerootprocess.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"SquareRootProcess",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 Real b = real(a[0]), speed = real(a[1]), sigma = real(a[2]), x0 = real(a[3]);
                 std::string m = text(a[4]);
                 Real t = real(a[5]), x = real(a[6]), dt = real(a[7]), dw = real(a[8]);
                 if (m != "x0" && m != "a" && m != "b" && m != "sigma" && m != "drift" && m != "diffusion" &&
                     m != "expectation" && m != "stdDeviation" && m != "variance" && m != "evolve")
                     throw InvalidParams("unknown member " + m);
                 return [=] {
                     SquareRootProcess p = x0 == Null<Real>() ? SquareRootProcess(b, speed, sigma)
                                                              : SquareRootProcess(b, speed, sigma, x0);
                     if (m == "x0")
                         return json::value(p.x0());
                     if (m == "a")
                         return json::value(p.a());
                     if (m == "b")
                         return json::value(p.b());
                     if (m == "sigma")
                         return json::value(p.sigma());
                     if (m == "drift")
                         return json::value(p.drift(t, x));
                     if (m == "diffusion")
                         return json::value(p.diffusion(t, x));
                     if (m == "expectation")
                         return json::value(p.expectation(t, x, dt));
                     if (m == "stdDeviation")
                         return json::value(p.stdDeviation(t, x, dt));
                     if (m == "variance")
                         return json::value(p.variance(t, x, dt));
                     return json::value(p.evolve(t, x, dt, dw));
                 };
             }},
        });

    }

}
