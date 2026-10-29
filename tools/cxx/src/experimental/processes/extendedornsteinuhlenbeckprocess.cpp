// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/processes/extendedornsteinuhlenbeckprocess.hpp, .cpp: the rules the wheel doesn't bind (it builds the
// mid-point rule alone). "ExtendedOrnsteinUhlenbeckProcess::values" takes (process, steps, draws): the process (a spec
// of extouspec.hpp) and steps [t0, x0, dt]; it returns {"expectation", "stdDeviation", "evolve"}: the expectation and
// deviation over each step, and the evolution on each step at each draw.

#include "experimental/processes/extouspec.hpp"

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"ExtendedOrnsteinUhlenbeckProcess::values",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 const json::value spec = a[0];
                 processes::extendedOU(spec);
                 if (!a[1].is_array())
                     throw InvalidParams("expected steps [t0, x0, dt], got " + json::serialize(a[1]));
                 std::vector<std::vector<Real>> steps;
                 for (const json::value& s : a[1].get_array()) {
                     steps.push_back(reals(s));
                     if (steps.back().size() != 3)
                         throw InvalidParams("a step is [t0, x0, dt]");
                 }
                 const std::vector<Real> draws = reals(a[2]);
                 return [=] {
                     const auto p = processes::extendedOU(spec);
                     json::array expectation, deviation, evolve;
                     for (const auto& s : steps) {
                         expectation.emplace_back(p->expectation(s[0], s[1], s[2]));
                         deviation.emplace_back(p->stdDeviation(s[0], s[1], s[2]));
                         for (Real w : draws)
                             evolve.emplace_back(p->evolve(s[0], s[1], s[2], w));
                     }
                     json::object out;
                     out["expectation"] = expectation;
                     out["stdDeviation"] = deviation;
                     out["evolve"] = evolve;
                     return json::value(out);
                 };
             }},
        });

    }

}
