// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/meshers/exponentialjump1dmesher.hpp, .cpp: the jump size's densities and
// distributions, which the wheel's mesher doesn't expose.
//
//     ExponentialJump1dMesher::jumpSize  (steps, beta, jumpIntensity, eta, eps, [x, ...], t):
//                                        {"density": [...], "densityAt": [...], "distribution": [...],
//                                         "distributionAt": [...]}, the stationary ones and those at time t

#include "convert.hpp"

#include <ql/methods/finitedifferences/meshers/exponentialjump1dmesher.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"ExponentialJump1dMesher::jumpSize",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Integer steps = integer(a[0]);
                 if (steps < 0)
                     throw InvalidParams("a number of steps is non-negative");
                 Real beta = real(a[1]), intensity = real(a[2]), eta = real(a[3]), eps = real(a[4]);
                 std::vector<Real> xs = reals(a[5]);
                 Real t = real(a[6]);
                 return [=] {
                     ExponentialJump1dMesher m(static_cast<Size>(steps), beta, intensity, eta, eps);
                     json::array density, densityAt, distribution, distributionAt;
                     for (Real x : xs) {
                         density.emplace_back(m.jumpSizeDensity(x));
                         densityAt.emplace_back(m.jumpSizeDensity(x, t));
                         distribution.emplace_back(m.jumpSizeDistribution(x));
                         distributionAt.emplace_back(m.jumpSizeDistribution(x, t));
                     }
                     json::object out;
                     out["density"] = std::move(density);
                     out["densityAt"] = std::move(densityAt);
                     out["distribution"] = std::move(distribution);
                     out["distributionAt"] = std::move(distributionAt);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
