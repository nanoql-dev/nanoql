// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/spherecylinder.hpp, .cpp, which the wheel doesn't expose. "SphereCylinderOptimizer" takes (r,
// s, alpha, z1, z2, z3, zweight, maxIterations, tolerance) and returns {"isIntersectionNonEmpty", "closest" (y1, y2,
// y3: findClosest's), "projectionFound" and "projection" (findByProjection's)}, the solves only where the intersection
// isn't empty; "sphereCylinderOptimizerClosest" takes the same and returns the function's three values.

#include "convert.hpp"

#include <ql/math/optimization/spherecylinder.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Inputs {
            Real r, s, alpha, z1, z2, z3, zweight;
            Size maxIterations;
            Real tolerance;
        };

        Inputs inputs(const Args& a) {
            arity(a, 9);
            return {real(a[0]), real(a[1]), real(a[2]), real(a[3]), real(a[4]), real(a[5]), real(a[6]),
                    static_cast<Size>(integer(a[7])), real(a[8])};
        }

        json::value triple(Real y1, Real y2, Real y3) {
            json::array out;
            out.emplace_back(y1);
            out.emplace_back(y2);
            out.emplace_back(y3);
            return out;
        }

        const Register unit({
            {"SphereCylinderOptimizer",
             [](const Args& a) -> Call {
                 const Inputs in = inputs(a);
                 return [=] {
                     SphereCylinderOptimizer optimizer(in.r, in.s, in.alpha, in.z1, in.z2, in.z3, in.zweight);
                     json::object out;
                     out["isIntersectionNonEmpty"] = optimizer.isIntersectionNonEmpty();
                     if (optimizer.isIntersectionNonEmpty()) {
                         Real y1 = 0.0, y2 = 0.0, y3 = 0.0;
                         optimizer.findClosest(in.maxIterations, in.tolerance, y1, y2, y3);
                         out["closest"] = triple(y1, y2, y3);
                         const bool found = optimizer.findByProjection(y1, y2, y3);
                         out["projectionFound"] = found;
                         out["projection"] = triple(y1, y2, y3);
                     }
                     return json::value(out);
                 };
             }},
            {"sphereCylinderOptimizerClosest",
             [](const Args& a) -> Call {
                 const Inputs in = inputs(a);
                 return [=] {
                     const std::vector<Real> y = sphereCylinderOptimizerClosest(
                         in.r, in.s, in.alpha, in.z1, in.z2, in.z3, static_cast<Natural>(in.maxIterations),
                         in.tolerance, in.zweight);
                     return triple(y[0], y[1], y[2]);
                 };
             }},
        });

    }

}
