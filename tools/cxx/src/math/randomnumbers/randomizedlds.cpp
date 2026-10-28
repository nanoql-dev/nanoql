// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/randomizedlds.hpp, which the wheel doesn't bind: the first n points of a fresh RandomizedLDS,
// the randomizer renewed after every `every` points (QuantLib's nextRandomizer, which also restarts the
// low-discrepancy sequence), and each point's weight. Its pseudo-random generator is QuantLib's default,
// RandomSequenceGenerator<MersenneTwisterUniformRng>, from a seed in [1, 4294967295] (0 is QuantLib's clock seed).
//
//     RandomizedLDS<SobolRsg>::draws   (dim, sobolSeed, mtSeed, n, every)
//     RandomizedLDS<LatticeRsg>::draws (rule, N, dim, mtSeed, n, every)

#include "convert.hpp"

#include <ql/math/randomnumbers/latticerules.hpp>
#include <ql/math/randomnumbers/latticersg.hpp>
#include <ql/math/randomnumbers/mt19937uniformrng.hpp>
#include <ql/math/randomnumbers/randomizedlds.hpp>
#include <ql/math/randomnumbers/randomsequencegenerator.hpp>
#include <ql/math/randomnumbers/sobolrsg.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        typedef RandomSequenceGenerator<MersenneTwisterUniformRng> Prs;

        unsigned long seed(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 1 || v.get_int64() > 4294967295LL)
                throw InvalidParams("expected a seed in [1, 4294967295], got " + json::serialize(v));
            return static_cast<unsigned long>(v.get_int64());
        }

        Size count(const json::value& v, std::int64_t least, std::int64_t most) {
            if (!v.is_int64() || v.get_int64() < least || v.get_int64() > most)
                throw InvalidParams("expected a count, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        LatticeRule::type rule(const json::value& v) {
            std::string s = text(v);
            if (s == "A")
                return LatticeRule::A;
            if (s == "B")
                return LatticeRule::B;
            if (s == "C")
                return LatticeRule::C;
            if (s == "D")
                return LatticeRule::D;
            throw InvalidParams("expected a lattice rule, A to D, got " + s);
        }

        template <class LDS>
        json::value draws(RandomizedLDS<LDS, Prs>& g, Size n, Size every) {
            json::array points, weights;
            for (Size i = 0; i < n; ++i) {
                if (i > 0 && i % every == 0)
                    g.nextRandomizer();
                const auto& x = g.nextSequence();
                json::array s;
                for (Real e : x.value)
                    s.emplace_back(e);
                points.emplace_back(std::move(s));
                weights.emplace_back(x.weight);
            }
            json::object out;
            out["nextSequence"] = std::move(points);
            out["weight"] = std::move(weights);
            return json::value(std::move(out));
        }

        const Register unit({
            {"RandomizedLDS<SobolRsg>::draws",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Size dim = count(a[0], 1, 1000);
                 unsigned long ldsSeed = seed(a[1]), prsSeed = seed(a[2]);
                 Size n = count(a[3], 0, 100000), every = count(a[4], 1, 100000);
                 return [=] {
                     RandomizedLDS<SobolRsg, Prs> g(SobolRsg(dim, ldsSeed), Prs(dim, prsSeed));
                     return draws(g, n, every);
                 };
             }},
            {"RandomizedLDS<LatticeRsg>::draws",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 LatticeRule::type name = rule(a[0]);
                 Integer bigN = integer(a[1]);
                 Size dim = count(a[2], 1, 3600);
                 unsigned long prsSeed = seed(a[3]);
                 Size n = count(a[4], 0, 100000), every = count(a[5], 1, 100000);
                 return [=] {
                     std::vector<Real> z;
                     LatticeRule::getRule(name, z, bigN);
                     RandomizedLDS<LatticeRsg, Prs> g(LatticeRsg(dim, z, static_cast<Size>(bigN)),
                                                      Prs(dim, MersenneTwisterUniformRng(prsSeed)));
                     return draws(g, n, every);
                 };
             }},
        });

    }

}
