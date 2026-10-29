// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/math/isotropicrandomwalk.hpp, levyflightdistribution.hpp, zigguratrng.hpp, .cpp,
// claytoncopularng.hpp, farliegumbelmorgensterncopularng.hpp and frankcopularng.hpp: the walks and the generators,
// which the wheel doesn't bind.
//
// "IsotropicRandomWalk::steps" takes (distribution, parameters, dim, lower, upper, seed, n): the radius's distribution
// "normal" (std::normal_distribution<Real>(mean, sigma)) or "levy" (LevyFlightDistribution(xm, alpha)) on a
// std::mt19937 at the seed, the walk's own twister at the seed, dimension 1 or 2, and the ellipsoid set from the
// bounds when they are given (empty for none); it returns {"steps"}, n steps in turn.
//
// "LevyFlightDistribution::values" takes (xm, alpha, points, seed, n): it returns {"density", "draws", "min", "max"},
// the density at the points, n draws on a std::mt19937 at the seed, min() and max().
//
// "ZigguratRng::draws" takes (seed, n, dimension, sequences): it returns {"draws", "sequences", "allowsErrorEstimate"},
// n draws of the generator, the first sequences of Ziggurat's sequence generator in the dimension, in turn, and the
// traits' allowsErrorEstimate.
//
// "CopulaRng::draws" takes (copula, theta, seed, n): "clayton", "frank" or "fgm" over a MersenneTwisterUniformRng at
// the seed; it returns {"u", "weights"}, n pairs in turn and their weights.

#include "convert.hpp"

#include <ql/experimental/math/claytoncopularng.hpp>
#include <ql/experimental/math/farliegumbelmorgensterncopularng.hpp>
#include <ql/experimental/math/frankcopularng.hpp>
#include <ql/experimental/math/isotropicrandomwalk.hpp>
#include <ql/experimental/math/levyflightdistribution.hpp>
#include <ql/experimental/math/zigguratrng.hpp>
#include <ql/math/randomnumbers/mt19937uniformrng.hpp>

#include <cstdint>
#include <random>
#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        template <class Distribution>
        json::array walk(Distribution d, Size dim, const std::vector<Real>& lower, const std::vector<Real>& upper,
                         unsigned long seed, Size n) {
            IsotropicRandomWalk<Distribution, std::mt19937> w(std::mt19937(seed), d, dim, Array(dim, 1.0), seed);
            if (!lower.empty())
                w.setDimension(dim, Array(lower.begin(), lower.end()), Array(upper.begin(), upper.end()));
            json::array out;
            std::vector<Real> step(dim);
            for (Size i = 0; i < n; ++i) {
                w.nextReal(step.begin());
                for (Real x : step)
                    out.emplace_back(x);
            }
            return out;
        }

        template <class Rng>
        json::value copulaDraws(Rng rng, Size n) {
            json::array u, weights;
            for (Size i = 0; i < n; ++i) {
                auto s = rng.next();
                u.emplace_back(s.value[0]);
                u.emplace_back(s.value[1]);
                weights.emplace_back(s.weight);
            }
            json::object out;
            out["u"] = u;
            out["weights"] = weights;
            return json::value(out);
        }

        const Register unit({
            {"IsotropicRandomWalk::steps",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 std::string distribution = text(a[0]);
                 std::vector<Real> p = reals(a[1]);
                 Size dim = static_cast<Size>(integer(a[2]));
                 std::vector<Real> lower = reals(a[3]), upper = reals(a[4]);
                 auto seed = static_cast<unsigned long>(integer(a[5]));
                 Size n = static_cast<Size>(integer(a[6]));
                 if (p.size() != 2)
                     throw InvalidParams("a distribution takes two parameters");
                 if (distribution != "normal" && distribution != "levy")
                     throw InvalidParams("unknown distribution " + distribution);
                 return [=] {
                     json::object out;
                     if (distribution == "normal")
                         out["steps"] = walk(std::normal_distribution<Real>(p[0], p[1]), dim, lower, upper, seed, n);
                     else
                         out["steps"] = walk(LevyFlightDistribution(p[0], p[1]), dim, lower, upper, seed, n);
                     return json::value(out);
                 };
             }},
            {"LevyFlightDistribution::values",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Real xm = real(a[0]), alpha = real(a[1]);
                 std::vector<Real> points = reals(a[2]);
                 auto seed = static_cast<unsigned long>(integer(a[3]));
                 Size n = static_cast<Size>(integer(a[4]));
                 return [=] {
                     LevyFlightDistribution d(xm, alpha);
                     json::array density, draws;
                     for (Real x : points)
                         density.emplace_back(d(x));
                     std::mt19937 engine(seed);
                     for (Size i = 0; i < n; ++i)
                         draws.emplace_back(d(engine));
                     json::object out;
                     out["density"] = density;
                     out["draws"] = draws;
                     out["min"] = (d.min)();
                     out["max"] = (d.max)();
                     return json::value(out);
                 };
             }},
            {"ZigguratRng::draws",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 auto seed = static_cast<unsigned long>(integer(a[0]));
                 Size n = static_cast<Size>(integer(a[1]));
                 Size dimension = static_cast<Size>(integer(a[2]));
                 Size sequences = static_cast<Size>(integer(a[3]));
                 return [=] {
                     ZigguratRng rng(seed);
                     json::array draws, seqs;
                     for (Size i = 0; i < n; ++i)
                         draws.emplace_back(rng.next().value);
                     auto rsg = Ziggurat::make_sequence_generator(dimension, seed);
                     for (Size i = 0; i < sequences; ++i)
                         for (Real x : rsg.nextSequence().value)
                             seqs.emplace_back(x);
                     json::object out;
                     out["draws"] = draws;
                     out["sequences"] = seqs;
                     out["allowsErrorEstimate"] = static_cast<bool>(Ziggurat::allowsErrorEstimate);
                     return json::value(out);
                 };
             }},
            {"CopulaRng::draws",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 std::string copula = text(a[0]);
                 Real theta = real(a[1]);
                 auto seed = static_cast<unsigned long>(integer(a[2]));
                 Size n = static_cast<Size>(integer(a[3]));
                 if (copula != "clayton" && copula != "frank" && copula != "fgm")
                     throw InvalidParams("unknown copula " + copula);
                 return [=] {
                     MersenneTwisterUniformRng mt(seed);
                     if (copula == "clayton")
                         return copulaDraws(ClaytonCopulaRng<MersenneTwisterUniformRng>(mt, theta), n);
                     if (copula == "frank")
                         return copulaDraws(FrankCopulaRng<MersenneTwisterUniformRng>(mt, theta), n);
                     return copulaDraws(FarlieGumbelMorgensternCopulaRng<MersenneTwisterUniformRng>(mt, theta), n);
                 };
             }},
        });

    }

}
