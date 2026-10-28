// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/math/polarstudenttrng.hpp, which the wheel doesn't expose, on QuantLib's Mersenne Twister:
//
//     PolarStudentTRng::draws (degreesFreedom, seed, count): [[value, weight]...] from the seed constructor

#include "convert.hpp"

#include <ql/experimental/math/polarstudenttrng.hpp>
#include <ql/math/randomnumbers/mt19937uniformrng.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"PolarStudentTRng::draws",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 Real df = real(a[0]);
                 auto seed = static_cast<BigNatural>(int64(a[1]));
                 auto count = static_cast<Size>(integer(a[2]));
                 return [=] {
                     PolarStudentTRng<MersenneTwisterUniformRng> g(df, seed);
                     json::array out;
                     for (Size i = 0; i < count; i++) {
                         auto s = g.next();
                         json::array d;
                         d.emplace_back(s.value);
                         d.emplace_back(s.weight);
                         out.emplace_back(std::move(d));
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
