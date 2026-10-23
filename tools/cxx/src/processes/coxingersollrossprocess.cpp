// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/processes/coxingersollrossprocess.hpp, .cpp: CoxIngersollRossProcess, which the wheel doesn't expose.
// "CoxIngersollRossProcess" takes (speed, vol, x0, level, member, t, x, dt, dw) and returns the member's value: "x0",
// "speed", "volatility" and "level" read no point; "drift" and "diffusion" read (t, x); "expectation", "stdDeviation"
// and "variance" (t, x, dt); "evolve" (t, x, dt, dw), its quadratic-exponential step. x0 or level null takes the
// constructor's default; the constructor refuses a negative volatility inside the call.

#include "convert.hpp"

#include <ql/processes/coxingersollrossprocess.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"CoxIngersollRossProcess",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 Real speed = real(a[0]), vol = real(a[1]), x0 = real(a[2]), level = real(a[3]);
                 std::string m = text(a[4]);
                 Real t = real(a[5]), x = real(a[6]), dt = real(a[7]), dw = real(a[8]);
                 if (m != "x0" && m != "speed" && m != "volatility" && m != "level" && m != "drift" &&
                     m != "diffusion" && m != "expectation" && m != "stdDeviation" && m != "variance" &&
                     m != "evolve")
                     throw InvalidParams("unknown member " + m);
                 return [=] {
                     CoxIngersollRossProcess p =
                         x0 == Null<Real>()      ? CoxIngersollRossProcess(speed, vol)
                         : level == Null<Real>() ? CoxIngersollRossProcess(speed, vol, x0)
                                                 : CoxIngersollRossProcess(speed, vol, x0, level);
                     if (m == "x0")
                         return json::value(p.x0());
                     if (m == "speed")
                         return json::value(p.speed());
                     if (m == "volatility")
                         return json::value(p.volatility());
                     if (m == "level")
                         return json::value(p.level());
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
