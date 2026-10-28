// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/randomdefaultmodel.hpp, which the wheel doesn't expose, on experimental/credit/portfoliospec's
// pool and copula:
//
//     GaussianRandomDefaultModel::sequences (pool, copula, accuracy, seed, tmax, n, resetAfter): the pool's default
//         times after each of n sequences, by name, the model reset after the sequence numbered resetAfter (null for
//         never); a tmax of null is QuantLib's default, QL_MAX_REAL

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "experimental/credit/portfoliospec.hpp"

#include <ql/experimental/credit/randomdefaultmodel.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"GaussianRandomDefaultModel::sequences",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 json::value pool = a[0], copula = a[1];
                 Real accuracy = real(a[2]);
                 std::int64_t seed = int64(a[3]);
                 Real tmax = a[4].is_null() ? QL_MAX_REAL : real(a[4]);
                 Integer n = integer(a[5]);
                 Integer resetAfter = a[6].is_null() ? -1 : integer(a[6]);
                 return [=] {
                     ext::shared_ptr<Pool> p = credit::portfolio::makePool(pool);
                     GaussianRandomDefaultModel m(p, p->defaultKeys(),
                                                  Handle<OneFactorCopula>(
                                                      credit::portfolio::makeOneFactorCopula(copula)),
                                                  accuracy, static_cast<long>(seed));
                     json::array out;
                     for (Integer k = 0; k < n; k++) {
                         m.nextSequence(tmax);
                         json::array times;
                         for (const std::string& name : p->names())
                             times.emplace_back(p->getTime(name));
                         out.emplace_back(std::move(times));
                         if (k == resetAfter)
                             m.reset();
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
