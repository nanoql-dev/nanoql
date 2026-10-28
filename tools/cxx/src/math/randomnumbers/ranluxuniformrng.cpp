// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/ranluxuniformrng.hpp, which the wheel doesn't bind: the first n draws of next() of a fresh
// Ranlux3UniformRng or Ranlux4UniformRng, each built from a seed, a non-negative 64-bit integer (QuantLib's
// std::uint_fast64_t), the standard engine's seeding MSVC's.
//
//     Ranlux3UniformRng::next  (seed, n)
//     Ranlux4UniformRng::next  (seed, n)

#include "convert.hpp"

#include <ql/math/randomnumbers/ranluxuniformrng.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        std::uint_fast64_t seed(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0)
                throw InvalidParams("expected a non-negative 64-bit seed, got " + json::serialize(v));
            return static_cast<std::uint_fast64_t>(v.get_int64());
        }

        Size count(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > 1000000)
                throw InvalidParams("expected a count of draws, got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        template <class RNG>
        Method draws() {
            return [](const Args& a) -> Call {
                arity(a, 2);
                std::uint_fast64_t s = seed(a[0]);
                Size n = count(a[1]);
                return [=] {
                    RNG rng(s);
                    json::array out;
                    for (Size i = 0; i < n; ++i)
                        out.emplace_back(rng.next().value);
                    return json::value(std::move(out));
                };
            };
        }

        const Register unit({
            {"Ranlux3UniformRng::next", draws<Ranlux3UniformRng>()},
            {"Ranlux4UniformRng::next", draws<Ranlux4UniformRng>()},
        });

    }

}
