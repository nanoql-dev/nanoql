// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/optimization/projection.hpp, .cpp: Projection, which no wheel class exposes, built inside each call from
// the parameters' values and the fixed-parameter flags (an empty list for QuantLib's default, all free).
//
//     Projection::project   (parameterValues, fixParameters, parameters)            the free parameters
//     Projection::include   (parameterValues, fixParameters, projectedParameters)   all the parameters
//
// each an Array as a list of reals, or QuantLib's refusal of the projection or of the call's argument.

#include "math/optimization/specs.hpp"

#include <ql/math/optimization/projection.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using namespace optimization;

        std::vector<bool> flags(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of booleans, got " + json::serialize(v));
            std::vector<bool> out;
            for (const json::value& x : v.get_array())
                out.push_back(boolean(x));
            return out;
        }

        // A function of a projection and an Array.
        Call withProjection(const Args& a, Array (*f)(const Projection&, const Array&)) {
            arity(a, 3);
            std::vector<Real> values = reals(a[0]);
            std::vector<bool> fix = flags(a[1]);
            std::vector<Real> x = reals(a[2]);
            return [=] { return arrayResult(f(Projection(makeArray(values), fix), makeArray(x))); };
        }

        const Register unit({
            {"Projection::project",
             [](const Args& a) {
                 return withProjection(a, [](const Projection& p, const Array& x) { return p.project(x); });
             }},
            {"Projection::include",
             [](const Args& a) {
                 return withProjection(a, [](const Projection& p, const Array& x) { return p.include(x); });
             }},
        });

    }

}
