# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmShoutLogInnerValueCalculator
(ql/methods/finitedifferences/utilities/fdmshoutloginnervaluecalculator.hpp, .cpp), which the wheel doesn't expose,
each output computed by the C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026: on the
mesher FdBlackScholesShoutEngine builds, the shout's exercise value, innerValue and avgInnerValue, at every point at
times from today to maturity.

Shout calls and puts at the money and away from it on spots of 100, flat curves at 5% and 2% (and at 0 and 3%),
volatilities of 20% and 35%, maturities of half a year and two years, on meshers of 15 and 31 points; without
dividends, and with dividends paid before every time read, where the escrowed level and the spot are one. A dividend
between the time read and maturity, where QuantLib strikes the residual option at the escrowed level and nanoql at the
spot (the S-van plan's VR4 (xxx), tests/NanoQL.Tests/Divergences/ShoutDividendStrikeTests.cs), is left out.

The harness's call is tools/cxx/src/methods/finitedifferences/utilities/fdmshoutloginnervaluecalculator.cpp's. Read
back by tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Utilities/FdmShoutLogInnerValueCalculatorCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

CALL, PUT = 1, -1

# (name, spot, r, q, vol, dividends [[days, amount]], maturity, payoff, xGrid, times)
CASES = [
    ("call at 100, half a year", 100.0, 0.05, 0.02, 0.2, [], 0.5, [CALL, 100.0], 15, [0.0, 0.1, 0.25, 0.49, 0.5]),
    ("put at 110, half a year", 100.0, 0.05, 0.02, 0.2, [], 0.5, [PUT, 110.0], 15, [0.0, 0.25, 0.5]),
    ("put at 90, two years", 100.0, 0.0, 0.03, 0.35, [], 2.0, [PUT, 90.0], 31, [0.0, 1.0, 1.9, 2.0]),
    ("call at 120, two years", 100.0, 0.05, 0.0, 0.35, [], 2.0, [CALL, 120.0], 31, [0.0, 0.5, 1.5]),
    ("put at 100, a dividend before the times", 100.0, 0.05, 0.02, 0.2, [[30, 3.0]], 1.0, [PUT, 100.0], 15,
     [0.25, 0.5, 1.0]),
]


def cases():
    with cxx.connect() as H:
        today = H.call("Date(Day,Month,Year)", 15, 10, 2026)
        st = H.settings(today)
        out = []
        for name, spot, r, q, vol, dividends, maturity, payoff, x_grid, times in CASES:
            divs = [[today + d, amount] for d, amount in dividends]
            res = H.call("FdmShoutLogInnerValueCalculator::innerValue", today, spot, r, q, vol, divs, maturity, payoff,
                         x_grid, times)
            out.append({"name": name,
                        "inputs": {"settings": st, "today": today, "spot": spot, "r": r, "q": q, "vol": vol,
                                   "dividends": divs, "maturity": maturity, "payoff": payoff, "xGrid": x_grid,
                                   "times": times},
                        "expected": res})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdmshoutloginnervaluecalculator.hpp", cases)
