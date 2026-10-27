// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/quotes/eurodollarfuturesquote.hpp, .cpp, which the wheel doesn't expose. "EurodollarFuturesImpliedStdDevQuote"
// takes (futures price, call price, put price, strike, guess, accuracy, maximum iterations) and returns the quote's
// first value: its solve from the guess, on the put's price where the rate strike, 100 less the strike, is above the
// rate forward, 100 less the futures price, and on the call's otherwise; a failed solve is the call's refusal.

#include "convert.hpp"

#include <ql/quotes/eurodollarfuturesquote.hpp>
#include <ql/quotes/simplequote.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        Handle<Quote> quote(Real x) { return Handle<Quote>(ext::make_shared<SimpleQuote>(x)); }

        const Register unit({
            {"EurodollarFuturesImpliedStdDevQuote",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Real forward = real(a[0]), callPrice = real(a[1]), putPrice = real(a[2]), strike = real(a[3]),
                      guess = real(a[4]), accuracy = real(a[5]);
                 Integer maxIter = integer(a[6]);
                 if (maxIter == Null<Integer>() || maxIter < 0)
                     throw InvalidParams("the maximum iterations are a Natural");
                 return [=] {
                     EurodollarFuturesImpliedStdDevQuote q(quote(forward), quote(callPrice), quote(putPrice), strike,
                                                           guess, accuracy, static_cast<Natural>(maxIter));
                     return json::value(q.value());
                 };
             }},
        });

    }

}
