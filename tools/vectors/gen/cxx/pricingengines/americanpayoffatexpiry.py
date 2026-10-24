# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AmericanPayoffAtExpiry (ql/pricingengines/americanpayoffatexpiry.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4): the value of cash-or-nothing
and asset-or-nothing calls and puts paid at expiry, knock-in and knock-out, at spots on both sides of the strike and on
it, over discounts, dividend discounts and variances from markets of negative to high rates and volatilities of 0.1%
to 60% over a quarter to three years, a random draw of 400 of them. Then the refusals of a spot, a discount or a
dividend discount that isn't positive, and of a negative variance.

The inputs nanoql fixes are left out: below a variance of ε, where QuantLib takes the spot's side of the strike (the
S-van plan's VR4 (ii), tests/NanoQL.Tests/Divergences/AmericanPayoffZeroVarianceTests.cs); and where QuantLib's
master after the pin changes the value (#2855, taken under VR5 (i)): where N(D₂), computed from the formula, underflows
or the power (H/S)^2μ overflows, both judged here with a margin. A payoff is a spec of
tools/cxx/src/instruments/payoffspec.hpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/AmericanPayoffAtExpiryCxxVectorTests.cs."""
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


def reaches_2855(kind, option, spot, strike, discount, dividend_discount, variance):
    """Whether #2855's log form could fire: N(D₂) from the formula near underflow, or the power near overflow."""
    reached = strike <= spot if option == CALL else strike >= spot
    in_the_money = (option == CALL and strike < spot) or (option == PUT and strike > spot)
    if reached or in_the_money:
        return False
    mu = math.log(dividend_discount / discount) / variance - 0.5 + (1.0 if kind == "asset" else 0.0)
    sd = math.sqrt(variance)
    eta = -1.0 if option == CALL else 1.0
    d2 = eta * (math.log(strike / spot) / sd + mu * sd)
    log_power = 2.0 * mu * math.log(strike / spot)
    return d2 < -37.0 or log_power > 700.0


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
            kind, option, knock_in = rnd.choice(["cash", "asset"]), rnd.choice([CALL, PUT]), rnd.choice([True, False])
            discount, dividend_discount, variance = math.exp(-r * t), math.exp(-q * t), vol * vol * t
            if variance < EPS or reaches_2855(kind, option, spot, strike, discount, dividend_discount, variance):
                continue
            p = payoff(kind, option, strike)
            out.append({"name": f"{len(out)} {kind} {'call' if option == CALL else 'put'} K {strike} S {spot}"
                                + ("" if knock_in else " knock-out"),
                        "inputs": {"settings": st, "spot": spot, "discount": discount,
                                   "dividendDiscount": dividend_discount, "variance": variance, "payoff": p,
                                   "knockIn": knock_in},
                        "expected": {"value": v.enc(H.call("AmericanPayoffAtExpiry::value", spot, discount,
                                                           dividend_discount, variance, p, knock_in))}})
        cash = payoff("cash", CALL, 110.0)
        for name, spot, discount, dividend_discount, variance in [("zero spot", 0.0, 0.95, 0.98, 0.04),
                                                                  ("zero discount", 100.0, 0.0, 0.98, 0.04),
                                                                  ("zero dividend discount", 100.0, 0.95, 0.0, 0.04),
                                                                  ("negative variance", 100.0, 0.95, 0.98, -0.04)]:
            args = [spot, discount, dividend_discount, variance, cash, True]
            out.append({"name": name,
                        "inputs": {"settings": st, "spot": spot, "discount": discount,
                                   "dividendDiscount": dividend_discount, "variance": variance, "payoff": cash,
                                   "knockIn": True},
                        "expected": {"value": v.attempt(lambda: H.call("AmericanPayoffAtExpiry::value", *args))}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/americanpayoffatexpiry.hpp", cases)
