// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/quadratic.hpp, .cpp, which the wheel doesn't expose. "quadratic" takes (a, b, c, x) and returns
// {"turningPoint", "valueAtTurningPoint", "value" (at x), "discriminant", "rootsFound", "x", "y"}, roots' outputs
// "x" and "y".

#include "convert.hpp"

#include <ql/math/quadratic.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"quadratic",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 const Real qa = real(a[0]), qb = real(a[1]), qc = real(a[2]), x = real(a[3]);
                 return [=] {
                     quadratic q(qa, qb, qc);
                     Real r1 = 0.0, r2 = 0.0;
                     const bool found = q.roots(r1, r2);
                     json::object out;
                     out["turningPoint"] = q.turningPoint();
                     out["valueAtTurningPoint"] = q.valueAtTurningPoint();
                     out["value"] = q(x);
                     out["discriminant"] = q.discriminant();
                     out["rootsFound"] = found;
                     out["x"] = r1;
                     out["y"] = r2;
                     return json::value(out);
                 };
             }},
        });

    }

}
