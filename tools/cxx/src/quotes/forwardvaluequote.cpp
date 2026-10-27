// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/quotes/forwardvaluequote.hpp, .cpp, which the wheel doesn't expose. "ForwardValueQuote" takes (index, fixing
// date), the index an IborIndex spec of indexes/indexspec.hpp, and returns the quote's value, the index's fixing at the
// date under the settings already set.

#include "convert.hpp"
#include "indexes/indexspec.hpp"

#include <ql/quotes/forwardvaluequote.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"ForwardValueQuote",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 indexes::IndexSpec s = indexes::iborIndex(a[0]);
                 DateSpec d = date(a[1]);
                 return [=] {
                     ForwardValueQuote q(indexes::makeIborIndex(s), makeDate(d));
                     return json::value(q.value());
                 };
             }},
        });

    }

}
