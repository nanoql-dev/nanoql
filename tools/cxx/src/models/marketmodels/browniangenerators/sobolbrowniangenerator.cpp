// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp, .cpp: the Sobol generator's ordering table
// and its transform of given variates, which the wheel doesn't expose.
//
// "SobolBrownianGenerator::orderedIndices" takes (factors, steps, ordering): QuantLib's Ordering (0 factors, 1 steps,
// 2 diagonal). It returns the table, a row per factor, the variate index each step reads.
//
// "SobolBrownianGenerator::transform" takes (factors, steps, ordering, variates): factors × steps arrays, each holding
// one variate's values over the paths. It returns transform(variates): a row per factor, each path's steps in turn,
// built by the Brownian bridge over the ordering's variates. The generator is built with seed 1 and Jaeckel's
// direction integers, which the transform doesn't read.

#include "convert.hpp"

#include <ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        SobolBrownianGenerator generator(Integer factors, Integer steps, SobolBrownianGenerator::Ordering ordering) {
            if (factors < 1 || steps < 1)
                throw InvalidParams("at least one factor and one step");
            return SobolBrownianGenerator(static_cast<Size>(factors), static_cast<Size>(steps), ordering, 1);
        }

        const Register unit({
            {"SobolBrownianGenerator::orderedIndices",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Integer factors = integer(a[0]), steps = integer(a[1]);
                 auto ordering = enumeration<SobolBrownianGenerator::Ordering>(a[2]);
                 return [=] {
                     SobolBrownianGenerator g = generator(factors, steps, ordering);
                     json::array rows;
                     for (const auto& row : g.orderedIndices()) {
                         json::array r;
                         for (Size k : row)
                             r.emplace_back(static_cast<double>(k));
                         rows.emplace_back(r);
                     }
                     return json::value(rows);
                 };
             }},
            {"SobolBrownianGenerator::transform",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Integer factors = integer(a[0]), steps = integer(a[1]);
                 auto ordering = enumeration<SobolBrownianGenerator::Ordering>(a[2]);
                 if (!a[3].is_array())
                     throw InvalidParams("expected an array of variates, got " + json::serialize(a[3]));
                 std::vector<std::vector<Real>> variates;
                 for (const json::value& v : a[3].get_array())
                     variates.push_back(reals(v));
                 return [=] {
                     SobolBrownianGenerator g = generator(factors, steps, ordering);
                     json::array rows;
                     for (const auto& row : g.transform(variates)) {
                         json::array r;
                         for (Real x : row)
                             r.emplace_back(x);
                         rows.emplace_back(r);
                     }
                     return json::value(rows);
                 };
             }},
        });

    }

}
