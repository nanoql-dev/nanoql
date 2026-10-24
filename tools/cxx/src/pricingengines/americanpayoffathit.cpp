// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/americanpayoffathit.hpp, .cpp: the American payoff at the hit, which the wheel doesn't expose.
// Each method builds the calculator inside the call from (spot, discount, dividendDiscount, variance, payoff), the
// payoff a spec of instruments/payoffspec.hpp, and returns:
//
//     AmericanPayoffAtHit::value      (spot, discount, dividendDiscount, variance, payoff)
//     AmericanPayoffAtHit::delta      (spot, discount, dividendDiscount, variance, payoff)
//     AmericanPayoffAtHit::gamma      (spot, discount, dividendDiscount, variance, payoff)
//     AmericanPayoffAtHit::rho        (spot, discount, dividendDiscount, variance, payoff, maturity)
//
// The payoff is cash-or-nothing or asset-or-nothing: on another, QuantLib leaves the amount paid unset.

#include "convert.hpp"
#include "instruments/payoffspec.hpp"

#include <ql/pricingengines/americanpayoffathit.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        using payoffs::PayoffSpec;

        struct Inputs {
            Real spot, discount, dividendDiscount, variance;
            PayoffSpec payoff;
        };

        Inputs inputs(const Args& a) {
            Inputs in{real(a[0]), real(a[1]), real(a[2]), real(a[3]), payoffs::payoff(a[4])};
            if (in.payoff.type != "CashOrNothingPayoff" && in.payoff.type != "AssetOrNothingPayoff")
                throw InvalidParams("a cash-or-nothing or asset-or-nothing payoff required");
            return in;
        }

        ext::shared_ptr<StrikedTypePayoff> striked(const PayoffSpec& s) {
            return ext::dynamic_pointer_cast<StrikedTypePayoff>(payoffs::makePayoff(s));
        }

        Call atHit(const Args& a, Real (*f)(const AmericanPayoffAtHit&)) {
            arity(a, 5);
            Inputs in = inputs(a);
            return [=] {
                AmericanPayoffAtHit p(in.spot, in.discount, in.dividendDiscount, in.variance, striked(in.payoff));
                return json::value(f(p));
            };
        }

        const Register unit({
            {"AmericanPayoffAtHit::value",
             [](const Args& a) { return atHit(a, [](const AmericanPayoffAtHit& p) { return p.value(); }); }},
            {"AmericanPayoffAtHit::delta",
             [](const Args& a) { return atHit(a, [](const AmericanPayoffAtHit& p) { return p.delta(); }); }},
            {"AmericanPayoffAtHit::gamma",
             [](const Args& a) { return atHit(a, [](const AmericanPayoffAtHit& p) { return p.gamma(); }); }},
            {"AmericanPayoffAtHit::rho",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Inputs in = inputs(a);
                 Real maturity = real(a[5]);
                 return [=] {
                     AmericanPayoffAtHit p(in.spot, in.discount, in.dividendDiscount, in.variance, striked(in.payoff));
                     return json::value(p.rho(maturity));
                 };
             }},
        });

    }

}
