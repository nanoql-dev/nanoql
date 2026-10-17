// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/randomsequencegenerator.hpp: the members of RandomSequenceGenerator<MersenneTwisterUniformRng>
// the wheel doesn't expose, nextInt32Sequence and lastSequence, and its two constructors' checks. A generator argument
// is the spec {"dimension", "seed"}, built with the seed constructor, or {"dimension", "rng": {"seed"}}, built from a
// MersenneTwisterUniformRng, each seed an unsigned 32-bit integer but 0, which QuantLib replaces with one drawn from
// the clock; the call builds a fresh generator from it. A sequence result is a list of numbers.

#include "convert.hpp"

#include <ql/math/randomnumbers/mt19937uniformrng.hpp>
#include <ql/math/randomnumbers/randomsequencegenerator.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        using Generator = RandomSequenceGenerator<MersenneTwisterUniformRng>;

        unsigned long seedValue(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 1 || v.get_int64() > 4294967295LL)
                throw InvalidParams("expected a seed in [1, 4294967295], got " + json::serialize(v));
            return static_cast<unsigned long>(v.get_int64());
        }

        Size size(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > 1000000)
                throw InvalidParams("expected a size, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        struct GeneratorSpec {
            Size dimension;
            bool fromRng;
            unsigned long seed;
        };

        GeneratorSpec generator(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a RandomSequenceGenerator spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const json::value* dimension = o.if_contains("dimension");
            if (dimension == nullptr || o.size() != 2)
                throw InvalidParams("a RandomSequenceGenerator spec is {\"dimension\", \"seed\"} or {\"dimension\", "
                                    "\"rng\"}, got " + json::serialize(v));
            if (const json::value* seed = o.if_contains("seed"))
                return {size(*dimension), false, seedValue(*seed)};
            const json::value* rng = o.if_contains("rng");
            if (rng == nullptr || !rng->is_object() || rng->get_object().size() != 1 ||
                !rng->get_object().contains("seed"))
                throw InvalidParams("a RandomSequenceGenerator spec's rng is {\"seed\"}, got " + json::serialize(v));
            return {size(*dimension), true, seedValue(rng->get_object().at("seed"))};
        }

        Generator make(const GeneratorSpec& s) {
            if (s.fromRng)
                return Generator(s.dimension, MersenneTwisterUniformRng(s.seed));
            return Generator(s.dimension, s.seed);
        }

        template <class T>
        json::value list(const std::vector<T>& x) {
            json::array out;
            for (const T& e : x)
                out.emplace_back(e);
            return json::value(std::move(out));
        }

        const Register unit({
            // nextInt32Sequence(spec, n): a fresh generator's first n integer sequences.
            {"RandomSequenceGenerator::nextInt32Sequence",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 GeneratorSpec s = generator(a[0]);
                 Size n = size(a[1]);
                 return [=] {
                     const Generator g = make(s);
                     json::array out;
                     for (Size i = 0; i < n; ++i) {
                         json::array draw;
                         for (BigNatural x : g.nextInt32Sequence())
                             draw.emplace_back(static_cast<std::int64_t>(x));
                         out.emplace_back(std::move(draw));
                     }
                     return json::value(std::move(out));
                 };
             }},
            // lastSequence(spec, n): after n nextSequence draws of a fresh generator, its last sequence and weight.
            {"RandomSequenceGenerator::lastSequence",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 GeneratorSpec s = generator(a[0]);
                 Size n = size(a[1]);
                 return [=] {
                     const Generator g = make(s);
                     for (Size i = 0; i < n; ++i)
                         g.nextSequence();
                     const Generator::sample_type& last = g.lastSequence();
                     return json::value(json::object{{"value", list(last.value)}, {"weight", last.weight}});
                 };
             }},
            {"RandomSequenceGenerator::dimension",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 GeneratorSpec s = generator(a[0]);
                 return [=] { return json::value(static_cast<std::int64_t>(make(s).dimension())); };
             }},
        });

    }

}
