# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackScholesCalculator (ql/pricingengines/blackscholescalculator.hpp, .cpp) through the C++
harness, which the wheel doesn't expose: the value and the spot-based Greeks (delta, elasticity, gamma, theta, theta per
day) of plain-vanilla, cash-or-nothing, asset-or-nothing and gap calls and puts at strikes below, at and above the
forward, on three markets (spot, growth, standard deviation, discount) and the times the thetas take.

Zero standard deviations are left out: BlackCalculator's Greeks there are nanoql's fixes
(tests/NanoQL.Tests/Divergences/BlackCalculatorZeroVolatilityTests.cs).

A case holds the "payoff" ([kind, optionType, strike, extra], extra the cash-or-nothing's cash or the gap's second
strike), "spot", "growth", "stdDev", "discount" and "maturity". The harness's call: BlackScholesCalculator::greeks
(tools/cxx/src/pricingengines/blackscholescalculator.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/BlackScholesCalculatorCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
CALL, PUT = 1, -1
MARKETS = [
    ("spot 100, a year at 25%", 100.0, 0.98019867330675525, 0.25, 0.95122942450071402, 1.0),
    ("spot 80, six months at 40%", 80.0, 1.0, 0.40 * 0.5 ** 0.5, 0.98511193960306, 0.5),
    ("spot 120, two years at 15%", 120.0, 0.92311634638663578, 0.15 * 2.0 ** 0.5, 0.85214378896621135, 2.0),
]
KINDS = [("PlainVanilla", 0.0), ("CashOrNothing", 10.0), ("AssetOrNothing", 0.0), ("Gap", None)]
KEYS = ("value", "delta", "elasticity", "gamma", "theta", "thetaPerDay")


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for mname, spot, growth, sd, discount, t in MARKETS:
            for kind, extra in KINDS:
                for option_type in (CALL, PUT):
                    for factor in (0.85, 1.0, 1.15):
                        strike = spot * factor
                        payoff = [kind, option_type, strike, strike * 0.95 if extra is None else extra]
                        r = H.call("BlackScholesCalculator::greeks", payoff, spot, growth, sd, discount, t)
                        out.append({"name": f"{mname}, {kind}, {'call' if option_type == CALL else 'put'}, "
                                            f"strike {strike:g}",
                                    "inputs": {"settings": st, "payoff": payoff, "spot": spot, "growth": growth,
                                               "stdDev": sd, "discount": discount, "maturity": t},
                                    "expected": {k: v.enc(r[k]) for k in KEYS}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/blackscholescalculator.hpp", cases)
