# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AmericanPayoffAtHit (ql/pricingengines/americanpayoffathit.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4): the value, delta, gamma and
rho of cash-or-nothing and asset-or-nothing calls and puts paid at the hit, at spots on both sides of the strike and
on it, over discounts, dividend discounts and variances from markets of negative to high rates and volatilities of
0.1% to 60% over a quarter to three years, a random draw of 400 of them. Then the refusals of a spot, a discount or a
dividend discount that isn't positive, of a negative variance, and of a negative maturity for the rho.

The inputs nanoql fixes are left out: where λ² = μ² − 2 ln D / σ²T is negative, QuantLib's value is NaN
(tests/NanoQL.Tests/Divergences/AmericanPayoffAtHitNegativeRateTests.cs); and below a variance of ε, where QuantLib
divides by it (AmericanPayoffZeroVarianceTests.cs). A payoff is a spec of tools/cxx/src/instruments/payoffspec.hpp.
Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/AmericanPayoffAtHitCxxVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

CALL, PUT = 1, -1
EPS = 2.220446049250313e-16


def payoff(kind, option, strike):
    if kind == "cash":
        return {"type": "CashOrNothingPayoff", "optionType": option, "strike": strike, "cashPayoff": 10.0}
    return {"type": "AssetOrNothingPayoff", "optionType": option, "strike": strike}


def lambda_squared(discount, dividend_discount, variance):
    mu = math.log(dividend_discount / discount) / variance - 0.5
    return mu * mu - 2.0 * math.log(discount) / variance


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []
        while len(out) < 400:
            t = rnd.choice([0.25, 1.0, 3.0])
            r = rnd.choice([-0.01, 0.0, 0.03, 0.08])
            q = rnd.choice([-0.005, 0.0, 0.02, 0.06])
            vol = rnd.choice([0.001, 0.01, 0.05, 0.2, 0.6])
            spot = rnd.choice([80.0, 95.0, 100.0, 105.0, 120.0])
            strike = rnd.choice([90.0, 100.0, 110.0])
            kind, option = rnd.choice(["cash", "asset"]), rnd.choice([CALL, PUT])
            discount, dividend_discount, variance = math.exp(-r * t), math.exp(-q * t), vol * vol * t
            if variance < EPS or lambda_squared(discount, dividend_discount, variance) < 0.0:
                continue
            args = [spot, discount, dividend_discount, variance, payoff(kind, option, strike)]
            out.append({"name": f"{len(out)} {kind} {'call' if option == CALL else 'put'} K {strike} S {spot}",
                        "inputs": {"settings": st, "spot": spot, "discount": discount,
                                   "dividendDiscount": dividend_discount, "variance": variance,
                                   "payoff": args[4], "maturity": t},
                        "expected": {"value": v.enc(H.call("AmericanPayoffAtHit::value", *args)),
                                     "delta": v.enc(H.call("AmericanPayoffAtHit::delta", *args)),
                                     "gamma": v.enc(H.call("AmericanPayoffAtHit::gamma", *args)),
                                     "rho": v.enc(H.call("AmericanPayoffAtHit::rho", *args, t))}})
        cash = payoff("cash", CALL, 110.0)
        for name, spot, discount, dividend_discount, variance in [("zero spot", 0.0, 0.95, 0.98, 0.04),
                                                                  ("zero discount", 100.0, 0.0, 0.98, 0.04),
                                                                  ("zero dividend discount", 100.0, 0.95, 0.0, 0.04),
                                                                  ("negative variance", 100.0, 0.95, 0.98, -0.04)]:
            args = [spot, discount, dividend_discount, variance, cash]
            out.append({"name": name,
                        "inputs": {"settings": st, "spot": spot, "discount": discount,
                                   "dividendDiscount": dividend_discount, "variance": variance, "payoff": cash,
                                   "maturity": 1.0},
                        "expected": {"value": v.attempt(lambda: H.call("AmericanPayoffAtHit::value", *args))}})
        args = [100.0, 0.95, 0.98, 0.04, cash]
        out.append({"name": "negative maturity",
                    "inputs": {"settings": st, "spot": 100.0, "discount": 0.95, "dividendDiscount": 0.98,
                               "variance": 0.04, "payoff": cash, "maturity": -1.0},
                    "expected": {"rho": v.attempt(lambda: H.call("AmericanPayoffAtHit::rho", *args, -1.0))}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/americanpayoffathit.hpp", cases)
