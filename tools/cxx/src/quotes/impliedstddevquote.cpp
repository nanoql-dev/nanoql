// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/quotes/impliedstddevquote.hpp, .cpp, which the wheel doesn't expose. "ImpliedStdDevQuote" takes (option type,
// forward, price, strike, guess, accuracy, maximum iterations), the type QuantLib's Option::Type integer, and returns
// the quote's first value: its solve from the guess, 0 where the solve fails.

#include "convert.hpp"

#include <ql/option.hpp>
#include <ql/quotes/impliedstddevquote.hpp>
#include <ql/quotes/simplequote.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"ImpliedStdDevQuote",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Integer type = integer(a[0]);
                 Real forward = real(a[1]), price = real(a[2]), strike = real(a[3]), guess = real(a[4]),
                      accuracy = real(a[5]);
                 Integer maxIter = integer(a[6]);
                 if (type != Option::Call && type != Option::Put)
                     throw InvalidParams("an option type is Call (1) or Put (-1)");
                 if (maxIter == Null<Integer>() || maxIter < 0)
                     throw InvalidParams("the maximum iterations are a Natural");
                 return [=] {
                     ImpliedStdDevQuote q(static_cast<Option::Type>(type),
                                          Handle<Quote>(ext::make_shared<SimpleQuote>(forward)),
                                          Handle<Quote>(ext::make_shared<SimpleQuote>(price)), strike, guess,
                                          accuracy, static_cast<Natural>(maxIter));
                     return json::value(q.value());
                 };
             }},
        });

    }

}
