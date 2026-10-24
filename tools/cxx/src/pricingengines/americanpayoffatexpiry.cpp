// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/americanpayoffatexpiry.hpp, .cpp: the American payoff at expiry, which the wheel doesn't expose.
// The method builds the calculator inside the call, the payoff a spec of instruments/payoffspec.hpp, and returns its
// value:
//
//     AmericanPayoffAtExpiry::value   (spot, discount, dividendDiscount, variance, payoff, knockIn)
//
// The payoff is cash-or-nothing or asset-or-nothing: on another, QuantLib leaves the amount paid unset.

#include "convert.hpp"
#include "instruments/payoffspec.hpp"

#include <ql/pricingengines/americanpayoffatexpiry.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"AmericanPayoffAtExpiry::value",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Real spot = real(a[0]), discount = real(a[1]), dividendDiscount = real(a[2]), variance = real(a[3]);
                 payoffs::PayoffSpec payoff = payoffs::payoff(a[4]);
                 if (payoff.type != "CashOrNothingPayoff" && payoff.type != "AssetOrNothingPayoff")
                     throw InvalidParams("a cash-or-nothing or asset-or-nothing payoff required");
                 bool knockIn = boolean(a[5]);
                 return [=] {
                     AmericanPayoffAtExpiry p(spot, discount, dividendDiscount, variance,
                                              ext::dynamic_pointer_cast<StrikedTypePayoff>(payoffs::makePayoff(payoff)),
                                              knockIn);
                     return json::value(p.value());
                 };
             }},
        });

    }

}
