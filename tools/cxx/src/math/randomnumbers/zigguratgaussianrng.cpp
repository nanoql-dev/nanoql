// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/zigguratgaussianrng.hpp over Xoshiro256StarStarUniformRng at the 64-bit seeds the wheel's
// binding can't pass: the first n draws of next() of a fresh ZigguratGaussianRng over a generator built from the seed,
// an unsigned 64-bit integer as a decimal string, not 0 (QuantLib's clock seed).
//
//     ZigguratGaussianRng<Xoshiro256StarStarUniformRng>::next  (seed, n)

#include "convert.hpp"

#include <ql/math/randomnumbers/xoshiro256starstaruniformrng.hpp>
#include <ql/math/randomnumbers/zigguratgaussianrng.hpp>

#include <cstdint>
#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        std::uint64_t seed(const json::value& v) {
            if (!v.is_string())
                throw InvalidParams("expected a seed as a decimal string, got " + json::serialize(v));
            std::string s(v.get_string().c_str());
            if (s.empty() || s.find_first_not_of("0123456789") != std::string::npos || s.size() > 20)
                throw InvalidParams("expected a seed as a decimal string, got " + s);
            std::uint64_t r = std::stoull(s);
            // QuantLib reads a seed of 0 as one drawn from the clock, which no vector may depend on.
            if (r == 0)
                throw InvalidParams("the seed is not 0");
            return r;
        }

        Size count(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > 1000000)
                throw InvalidParams("expected a count of draws, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        const Register unit({
            {"ZigguratGaussianRng<Xoshiro256StarStarUniformRng>::next",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 std::uint64_t s = seed(a[0]);
                 Size n = count(a[1]);
                 return [=] {
                     ZigguratGaussianRng<Xoshiro256StarStarUniformRng> rng(Xoshiro256StarStarUniformRng{s});
                     json::array out;
                     for (Size i = 0; i < n; ++i)
                         out.emplace_back(rng.next().value);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
