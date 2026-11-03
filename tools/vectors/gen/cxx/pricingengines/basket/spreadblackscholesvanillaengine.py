# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SpreadBlackScholesVanillaEngine (ql/pricingengines/basket/spreadblackscholesvanillaengine.hpp,
.cpp), the spread engines' abstract base, which the wheel doesn't expose, each output computed by the C++ harness
(docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026: what the base hands its derived engine's
formula, read through a derived engine of the harness's own that records its arguments and returns df·(f1 − f2 − strike)
+ rho (tests/NanoQL.Tests mirrors it). European spread calls and puts at positive, zero and negative strikes, at
correlations of either sign, on two Black-Scholes-Merton processes with their own risk-free and dividend rates, each
with a constant volatility or a surface (the variance read at its forward): the NPV, the two forwards (each on its own
process's curves), the strike, the type, the two variances and the discount factor (on the first process's risk-free
curve), and the correlation. Then the base's refusals, their messages QuantLib's: an American exercise, a maximum
basket, and a cash-or-nothing payoff. Every structure is dated at the evaluation date and counts its days on Actual/365
(Fixed); structures dated apart are left out.

A case holds "process1" and "process2" ([spot, r, q, volatility], the volatility a number or [dates, strikes, rows],
a row of volatilities per strike), the "correlation", the "payoff" ([kind, base, optionType, strike]) and the
"exercise" ([kind, date]). The harness's call: SpreadBlackScholesVanillaEngine::inputs
(tools/cxx/src/pricingengines/basket/spreadblackscholesvanillaengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Basket/SpreadBlackScholesVanillaEngineCxxVectorTests.cs."""
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
SURFACE = [[R + 182, R + 365, R + 730], [80.0, 100.0, 120.0],
           [[0.30, 0.28, 0.27], [0.25, 0.24, 0.23], [0.22, 0.22, 0.21]]]
SKEW = [[R + 91, R + 365, R + 1095], [70.0, 95.0, 130.0],
        [[0.35, 0.33, 0.30], [0.28, 0.27, 0.26], [0.24, 0.25, 0.25]]]
FLAT1 = [100.0, 0.05, 0.02, 0.25]
FLAT2 = [95.0, 0.03, 0.01, 0.3]
SURF1 = [100.0, 0.04, 0.0, SURFACE]
SURF2 = [92.0, 0.045, 0.015, SKEW]
YEAR = ["European", R + 365]

SETUPS = [
    ("flat, call, strike 5", FLAT1, FLAT2, 0.5, ["Spread", "PlainVanilla", CALL, 5.0], YEAR),
    ("flat, put, strike -3", FLAT1, FLAT2, -0.3, ["Spread", "PlainVanilla", PUT, -3.0], YEAR),
    ("flat, call, strike 0", FLAT2, FLAT1, 0.9, ["Spread", "PlainVanilla", CALL, 0.0], ["European", R + 548]),
    ("a surface first, call", SURF1, FLAT2, 0.4, ["Spread", "PlainVanilla", CALL, 4.0], YEAR),
    ("a surface second, put", FLAT1, SURF1, -0.6, ["Spread", "PlainVanilla", PUT, 2.0], ["European", R + 270]),
    ("two surfaces, call", SURF1, SURF2, 0.2, ["Spread", "PlainVanilla", CALL, 8.0], ["European", R + 600]),
    ("two surfaces, put, six months", SURF2, SURF1, 0.7, ["Spread", "PlainVanilla", PUT, -5.0], ["European", R + 182]),
]
REFUSALS = [
    ("an American exercise", ["Spread", "PlainVanilla", CALL, 5.0], ["American", R + 365]),
    ("a maximum basket", ["Max", "PlainVanilla", CALL, 5.0], YEAR),
    ("a cash-or-nothing payoff", ["Spread", "CashOrNothing", CALL, 5.0], YEAR),
]
KEYS = ("NPV", "f1", "f2", "strike", "optionType", "variance1", "variance2", "df", "rho")


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def case(name, p1, p2, rho, payoff, exercise, refused=False):
            try:
                r = H.call("SpreadBlackScholesVanillaEngine::inputs", p1, p2, rho, payoff, exercise)
                expected = {k: v.enc(r[k]) for k in KEYS}
                assert not refused, name
            except RuntimeError as e:
                assert refused, (name, e)
                expected = {"error": str(e)}
            out.append({"name": name,
                        "inputs": {"settings": st, "process1": p1, "process2": p2, "correlation": rho,
                                   "payoff": payoff, "exercise": exercise},
                        "expected": expected})

        for setup in SETUPS:
            case(*setup)
        for name, payoff, exercise in REFUSALS:
            case(f"refused: {name}", FLAT1, FLAT2, 0.5, payoff, exercise, refused=True)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/spreadblackscholesvanillaengine.hpp", cases)
