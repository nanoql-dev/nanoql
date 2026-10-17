// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/mt19937uniformrng.hpp: the members of MersenneTwisterUniformRng the wheel doesn't expose,
// nextInt32 and the constructor from a key, whose words the wheel can't pass. A generator argument is the spec
// {"seed": n}, the seed constructor's, or {"seeds": [n, ...]}, the key constructor's, each value an unsigned 32-bit
// integer (the full range, where the wheel's seed is a signed 32-bit one), a seed not 0, which QuantLib replaces with
// one drawn from the clock; the call builds a fresh generator from it.

#include "convert.hpp"

#include <ql/math/randomnumbers/mt19937uniformrng.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        unsigned long word(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > 4294967295LL)
                throw InvalidParams("expected an unsigned 32-bit integer, got " + json::serialize(v));
            return static_cast<unsigned long>(v.get_int64());
        }

        struct GeneratorSpec {
            bool keyed;
            unsigned long seed;
            std::vector<unsigned long> seeds;
        };

        GeneratorSpec generator(const json::value& v) {
            if (!v.is_object() || v.get_object().size() != 1)
                throw InvalidParams("expected a MersenneTwisterUniformRng spec, {\"seed\"} or {\"seeds\"}, got " +
                                    json::serialize(v));
            const json::object& o = v.get_object();
            if (const json::value* seed = o.if_contains("seed")) {
                // QuantLib reads a seed of 0 as one drawn from the clock, which no vector may depend on.
                if (word(*seed) == 0UL)
                    throw InvalidParams("a MersenneTwisterUniformRng spec's seed is in [1, 4294967295]");
                return {false, word(*seed), {}};
            }
            const json::value* seeds = o.if_contains("seeds");
            if (seeds == nullptr || !seeds->is_array() || seeds->get_array().empty())
                throw InvalidParams("a MersenneTwisterUniformRng spec takes a seed or a non-empty key, got " +
                                    json::serialize(v));
            std::vector<unsigned long> key;
            for (const json::value& w : seeds->get_array())
                key.push_back(word(w));
            return {true, 0UL, key};
        }

        Size count(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > 1000000)
                throw InvalidParams("expected a count of draws, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        const Register unit({
            // nextInt32(spec, n): the first n integers of a fresh generator.
            {"MersenneTwisterUniformRng::nextInt32",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 GeneratorSpec s = generator(a[0]);
                 Size n = count(a[1]);
                 return [=] {
                     MersenneTwisterUniformRng rng = s.keyed ? MersenneTwisterUniformRng(s.seeds)
                                                             : MersenneTwisterUniformRng(s.seed);
                     json::array out;
                     for (Size i = 0; i < n; ++i)
                         out.emplace_back(static_cast<std::int64_t>(rng.nextInt32()));
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
