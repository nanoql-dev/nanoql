// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/xoshiro256starstaruniformrng.hpp at the seeds the wheel can't pass: its binding takes a signed
// 32-bit seed and exposes next() only, where QuantLib's constructors take 64-bit seeds and a 64-bit state, and its
// nextInt64 and nextReal. A generator argument is the spec {"seed": "<decimal>"} or {"state": ["<s0>", "<s1>", "<s2>",
// "<s3>"]}, each an unsigned 64-bit integer as a decimal string (JSON's numbers lose digits above 2^53), a seed not 0,
// which QuantLib replaces with one drawn from the clock; the call builds a fresh generator from it. nextInt64's draws
// come back as decimal strings likewise.
//
//     Xoshiro256StarStarUniformRng::nextInt64  (spec, n)
//     Xoshiro256StarStarUniformRng::nextReal   (spec, n)

#include "convert.hpp"

#include <ql/math/randomnumbers/xoshiro256starstaruniformrng.hpp>

#include <cstdint>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        std::uint64_t word(const json::value& v) {
            if (!v.is_string())
                throw InvalidParams("expected an unsigned 64-bit integer as a decimal string, got " +
                                    json::serialize(v));
            std::string s(v.get_string().c_str());
            if (s.empty() || s.find_first_not_of("0123456789") != std::string::npos || s.size() > 20)
                throw InvalidParams("expected an unsigned 64-bit integer as a decimal string, got " + s);
            return std::stoull(s);
        }

        Xoshiro256StarStarUniformRng generator(const json::value& v) {
            if (!v.is_object() || v.get_object().size() != 1)
                throw InvalidParams("expected a Xoshiro256StarStarUniformRng spec, {\"seed\"} or {\"state\"}, got " +
                                    json::serialize(v));
            const json::object& o = v.get_object();
            if (const json::value* seed = o.if_contains("seed")) {
                // QuantLib reads a seed of 0 as one drawn from the clock, which no vector may depend on.
                std::uint64_t s = word(*seed);
                if (s == 0)
                    throw InvalidParams("a Xoshiro256StarStarUniformRng spec's seed is not 0");
                return Xoshiro256StarStarUniformRng(s);
            }
            const json::value* state = o.if_contains("state");
            if (state == nullptr || !state->is_array() || state->get_array().size() != 4)
                throw InvalidParams("a Xoshiro256StarStarUniformRng spec takes a seed or a state of four words, got " +
                                    json::serialize(v));
            const json::array& s = state->get_array();
            return Xoshiro256StarStarUniformRng(word(s[0]), word(s[1]), word(s[2]), word(s[3]));
        }

        Size count(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > 1000000)
                throw InvalidParams("expected a count of draws, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        const Register unit({
            {"Xoshiro256StarStarUniformRng::nextInt64",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value spec = a[0];
                 generator(spec);
                 Size n = count(a[1]);
                 return [=] {
                     Xoshiro256StarStarUniformRng rng = generator(spec);
                     json::array out;
                     for (Size i = 0; i < n; ++i)
                         out.emplace_back(std::to_string(rng.nextInt64()));
                     return json::value(std::move(out));
                 };
             }},
            {"Xoshiro256StarStarUniformRng::nextReal",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 json::value spec = a[0];
                 generator(spec);
                 Size n = count(a[1]);
                 return [=] {
                     Xoshiro256StarStarUniformRng rng = generator(spec);
                     json::array out;
                     for (Size i = 0; i < n; ++i)
                         out.emplace_back(rng.nextReal());
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
