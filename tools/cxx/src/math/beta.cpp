// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/math/beta.hpp, .cpp, which the wheel doesn't expose: betaFunction, betaContinuedFraction and
// incompleteBetaFunction (the binomial distribution's vectors reach the last through its cumulative distribution).

#include "convert.hpp"

#include <ql/math/beta.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"betaFunction",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 Real z = real(a[0]), w = real(a[1]);
                 return [=] { return json::value(betaFunction(z, w)); };
             }},
            {"betaContinuedFraction",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Real x = real(a[0]), y = real(a[1]), z = real(a[2]), accuracy = real(a[3]);
                 Integer maxIteration = integer(a[4]);
                 return [=] { return json::value(betaContinuedFraction(x, y, z, accuracy, maxIteration)); };
             }},
            {"incompleteBetaFunction",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Real x = real(a[0]), y = real(a[1]), z = real(a[2]), accuracy = real(a[3]);
                 Integer maxIteration = integer(a[4]);
                 return [=] { return json::value(incompleteBetaFunction(x, y, z, accuracy, maxIteration)); };
             }},
        });

    }

}
