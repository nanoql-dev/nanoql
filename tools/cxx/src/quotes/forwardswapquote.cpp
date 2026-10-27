// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/quotes/forwardswapquote.hpp, .cpp, which the wheel doesn't expose. "ForwardSwapQuote" takes (swap index, spread,
// forward start), the swap index a spec of cashflows/cmsspec.hpp, the spread a value whose 0 is QuantLib's empty handle,
// which it reads as 0, and the forward start a period [length, units]; under the settings already set it returns
// {"value", "valueDate", "startDate", "fixingDate"}.

#include "cashflows/cmsspec.hpp"
#include "convert.hpp"

#include <ql/quotes/forwardswapquote.hpp>
#include <ql/quotes/simplequote.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"ForwardSwapQuote",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 cms::SwapIndexSpec s = cms::swapIndex(a[0]);
                 Real spread = real(a[1]);
                 std::vector<Integer> p = integers(a[2]);
                 if (p.size() != 2 || p[1] < Days || p[1] > Years)
                     throw InvalidParams("a period is [length, units]");
                 return [=] {
                     Handle<Quote> spreadHandle;
                     if (spread != 0.0)
                         spreadHandle = Handle<Quote>(ext::make_shared<SimpleQuote>(spread));
                     ForwardSwapQuote q(cms::makeSwapIndex(s), spreadHandle,
                                        Period(p[0], static_cast<TimeUnit>(p[1])));
                     json::object out;
                     out["value"] = q.value();
                     out["valueDate"] = result(q.valueDate());
                     out["startDate"] = result(q.startDate());
                     out["fixingDate"] = result(q.fixingDate());
                     return json::value(out);
                 };
             }},
        });

    }

}
