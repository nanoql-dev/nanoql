// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/randomnumbers/faurersg.hpp, which the wheel doesn't bind: the first n draws of a fresh FaureRsg of a
// dimension, each draw's sequence and integer sequence. MSVC's 32-bit long sets the digits the sequence carries.
//
//     FaureRsg::draws  (dimensionality, n): {"nextSequence": [[...]], "lastIntSequence": [[...]]}

#include "convert.hpp"

#include <ql/math/randomnumbers/faurersg.hpp>

#include <cstdint>

using namespace QuantLib;

namespace harness {

    namespace {

        Size positive(const json::value& v, const char* what, std::int64_t most) {
            if (!v.is_int64() || v.get_int64() < 1 || v.get_int64() > most)
                throw InvalidParams(std::string("expected ") + what + ", got " + json::serialize(v));
            return static_cast<Size>(v.get_int64());
        }

        const Register unit({
            {"FaureRsg::draws",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Size dimensionality = positive(a[0], "a dimension", 100000);
                 Size n = positive(a[1], "a count of draws", 1000000);
                 return [=] {
                     FaureRsg rsg(dimensionality);
                     json::array sequences, integers;
                     for (Size i = 0; i < n; ++i) {
                         const std::vector<Real>& x = rsg.nextSequence().value;
                         json::array s, k;
                         for (Real e : x)
                             s.emplace_back(e);
                         for (long e : rsg.lastIntSequence())
                             k.emplace_back(static_cast<std::int64_t>(e));
                         sequences.emplace_back(std::move(s));
                         integers.emplace_back(std::move(k));
                     }
                     json::object out;
                     out["nextSequence"] = std::move(sequences);
                     out["lastIntSequence"] = std::move(integers);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
