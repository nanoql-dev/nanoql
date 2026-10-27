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
//
// "Burley2020SobolBrownianGenerator.paths" takes (factors, steps, ordering, seed, directionIntegers, scrambleSeed,
// paths): the generator's and its factory's (Burley2020SobolBrownianGeneratorFactory(ordering, seed,
// directionIntegers, scrambleSeed).create(factors, steps)) "numberOfFactors", "numberOfSteps" and draws, under
// "generator" and "factory": each path {"weight", "steps"}, each step {"weight", "variates"}.

#include "convert.hpp"

#include <ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp>

#include <cstdint>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        SobolBrownianGenerator generator(Integer factors, Integer steps, SobolBrownianGenerator::Ordering ordering) {
            if (factors < 1 || steps < 1)
                throw InvalidParams("at least one factor and one step");
            return SobolBrownianGenerator(static_cast<Size>(factors), static_cast<Size>(steps), ordering, 1);
        }

        unsigned long seedOf(const json::value& v) {
            long long seed = int64(v);
            if (seed < 1 || seed > 4294967295LL)
                throw InvalidParams("a seed must be in [1, 4294967295]");
            return static_cast<unsigned long>(seed);
        }

        json::value draws(BrownianGenerator& g, Integer paths) {
            json::object out;
            out["numberOfFactors"] = static_cast<std::int64_t>(g.numberOfFactors());
            out["numberOfSteps"] = static_cast<std::int64_t>(g.numberOfSteps());
            json::array ps;
            std::vector<Real> variates(g.numberOfFactors());
            for (Integer p = 0; p < paths; ++p) {
                json::object path;
                path["weight"] = g.nextPath();
                json::array ss;
                for (Size s = 0; s < g.numberOfSteps(); ++s) {
                    json::object step;
                    step["weight"] = g.nextStep(variates);
                    json::array vs;
                    for (Real x : variates)
                        vs.emplace_back(x);
                    step["variates"] = vs;
                    ss.emplace_back(step);
                }
                path["steps"] = ss;
                ps.emplace_back(path);
            }
            out["paths"] = ps;
            return json::value(out);
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
            {"Burley2020SobolBrownianGenerator.paths",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Integer factors = integer(a[0]), steps = integer(a[1]);
                 auto ordering = enumeration<SobolBrownianGenerator::Ordering>(a[2]);
                 unsigned long seed = seedOf(a[3]);
                 auto integers = enumeration<SobolRsg::DirectionIntegers>(a[4]);
                 unsigned long scrambleSeed = seedOf(a[5]);
                 Integer paths = integer(a[6]);
                 if (factors < 1 || steps < 1 || paths < 0)
                     throw InvalidParams("at least one factor and one step, and no negative paths");
                 return [=] {
                     Burley2020SobolBrownianGenerator g(static_cast<Size>(factors), static_cast<Size>(steps), ordering,
                                                        seed, integers, scrambleSeed);
                     Burley2020SobolBrownianGeneratorFactory f(ordering, seed, integers, scrambleSeed);
                     ext::shared_ptr<BrownianGenerator> h = f.create(static_cast<Size>(factors), static_cast<Size>(steps));
                     json::object out;
                     out["generator"] = draws(g, paths);
                     out["factory"] = draws(*h, paths);
                     return json::value(out);
                 };
             }},
        });

    }

}
