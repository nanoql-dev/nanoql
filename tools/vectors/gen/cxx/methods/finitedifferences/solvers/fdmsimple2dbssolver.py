# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmSimple2dBSSolver (ql/methods/finitedifferences/solvers/fdmsimple2dbssolver.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4), on the
evaluation date 15 October 2026: on the problem FdSimpleBSSwingEngine builds (a Black-Scholes mesher of the log spot by
a uniform mesher of the rights used, the swing condition at the exercise times, the maturity the last of them), its
valueAt, deltaAt and gammaAt (bumped by 1% of the spot) and thetaAt at spots around the strike and at the rights' log
coordinates 0 to the maximum's (a = 1, e, e²).

Forward and plain-vanilla calls and puts at 100 on spots of 100, flat curves at 4% and 2% (and at 0 and 5%),
volatilities of 25% and 40%, exercise times monthly over half a year and quarterly over a year, minimums of none and
some, maximums of 2 and 4, on grids of 25 x 60 and 50 x 100.

The harness's call is tools/cxx/src/methods/finitedifferences/solvers/fdmsimple2dbssolver.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Solvers/FdmSimple2dBSSolverCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

CALL, PUT = 1, -1
MONTHLY = [i / 12.0 for i in range(1, 7)]
QUARTERLY = [0.25, 0.5, 0.75, 1.0]
SPOTS = [80.0, 95.0, 100.0, 105.0, 125.0]

# (name, spot, r, q, vol, payoff, times, minimum, maximum, tGrid, xGrid)
CASES = [
    ("forward put, monthly, at most 2", 100.0, 0.04, 0.02, 0.25, ["Forward", PUT, 100.0], MONTHLY, 0, 2, 25, 60),
    ("forward call, monthly, at most 4", 100.0, 0.04, 0.02, 0.25, ["Forward", CALL, 100.0], MONTHLY, 0, 4, 25, 60),
    ("forward put, quarterly, 1 to 2", 100.0, 0.0, 0.05, 0.4, ["Forward", PUT, 100.0], QUARTERLY, 1, 2, 25, 60),
    ("plain put, quarterly, at most 2", 100.0, 0.04, 0.02, 0.4, ["PlainVanilla", PUT, 100.0], QUARTERLY, 0, 2, 50, 100),
    ("plain call, quarterly, all four forced", 100.0, 0.04, 0.02, 0.25, ["PlainVanilla", CALL, 100.0], QUARTERLY, 4, 4,
     25, 60),
]


def cases():
    with cxx.connect() as H:
        today = H.call("Date(Day,Month,Year)", 15, 10, 2026)
        st = H.settings(today)
        out = []
        for name, spot, r, q, vol, payoff, times, minimum, maximum, t_grid, x_grid in CASES:
            points = [[s, a] for s in SPOTS for a in [math.exp(k) for k in range(0, min(maximum, 2) + 1)]]
            res = H.call("FdmSimple2dBSSolver::results", today, spot, r, q, vol, payoff, times, minimum, maximum,
                         t_grid, x_grid, points, 0.01 * spot)
            out.append({"name": name,
                        "inputs": {"settings": st, "today": today, "spot": spot, "r": r, "q": q, "vol": vol,
                                   "payoff": payoff, "exerciseTimes": times, "minExercises": minimum,
                                   "maxExercises": maximum, "tGrid": t_grid, "xGrid": x_grid, "points": points,
                                   "eps": 0.01 * spot},
                        "expected": res})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdmsimple2dbssolver.hpp", cases)
