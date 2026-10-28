// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/methods/finitedifferences/trbdf2.hpp and zerocondition.hpp, which the wheel doesn't bind: the old framework's
// TR-BDF2 scheme rolling values back on a time-independent tridiagonal operator, through FiniteDifferenceModel, with
// the zero condition or none.
//
//     TRBDF2::rollback  (low, mid, high, values, from, to, steps, zeroCondition):  the values rolled back

#include "convert.hpp"

#include <ql/methods/finitedifferences/finitedifferencemodel.hpp>
#include <ql/methods/finitedifferences/trbdf2.hpp>
#include <ql/methods/finitedifferences/zerocondition.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"TRBDF2::rollback",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 std::vector<Real> low = reals(a[0]), mid = reals(a[1]), high = reals(a[2]), values = reals(a[3]);
                 if (mid.size() < 2 || low.size() + 1 != mid.size() || high.size() + 1 != mid.size()
                     || values.size() != mid.size())
                     throw InvalidParams("diagonals of sizes n - 1, n, n - 1 and n values, n at least 2");
                 Real from = real(a[4]), to = real(a[5]);
                 Integer steps = integer(a[6]);
                 if (steps < 0)
                     throw InvalidParams("a number of steps is non-negative");
                 bool zero = boolean(a[7]);
                 return [=] {
                     TridiagonalOperator l(Array(low.begin(), low.end()), Array(mid.begin(), mid.end()),
                                           Array(high.begin(), high.end()));
                     std::vector<ext::shared_ptr<BoundaryCondition<TridiagonalOperator>>> bcs;
                     FiniteDifferenceModel<TRBDF2<TridiagonalOperator>> model(l, bcs);
                     Array u(values.begin(), values.end());
                     if (zero)
                         model.rollback(u, from, to, static_cast<Size>(steps), ZeroCondition<Array>());
                     else
                         model.rollback(u, from, to, static_cast<Size>(steps));
                     json::array out;
                     for (Real x : u)
                         out.emplace_back(x);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
