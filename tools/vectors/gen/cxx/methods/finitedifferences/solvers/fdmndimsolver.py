# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmNdimSolver (ql/methods/finitedifferences/solvers/fdmndimsolver.hpp), which the wheel
doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15
October 2026: the n-dimensional solver in one to four dimensions on uniform meshers, its operator FdmWienerOp (with
and without a flat risk-free curve), its inner value FdmLogBasketInnerValue of an average basket call or put, European
(no condition) and American (QuantLib's vanilla composite), under Douglas's and Hundsdorfer's schemes; its
interpolateAt and thetaAt at points inside the grid.

The points stay inside each mesher's last point, where QuantLib's spline refuses and nanoql's interpolates
(tests/NanoQL.Tests/Divergences/MultiCubicSplineBoundaryTests.cs).

The harness's call is tools/cxx/src/methods/finitedifferences/solvers/fdmndimsolver.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Solvers/FdmNdimSolverCxxVectorTests.cs."""
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
DOUGLAS = [0, 0.5, 0.5]
HUNDSDORFER = [1, 0.5 + 3 ** 0.5 / 6.0, 0.5]

# (name, meshers, lambdas, r, weights, payoff, exercise, tGrid, scheme)
CASES = [
    ("one dimension, call", [[3.9, 5.3, 21]], [0.04], 0.03, [1.0], [CALL, 100.0], "European", 20, DOUGLAS),
    ("one dimension, American put", [[3.9, 5.3, 21]], [0.04], 0.03, [1.0], [PUT, 100.0], "American", 20, DOUGLAS),
    ("two dimensions, call", [[3.9, 5.3, 15], [4.0, 5.2, 11]], [0.05, 0.02], 0.03, [0.5, 0.5], [CALL, 100.0],
     "European", 15, DOUGLAS),
    ("two dimensions, undiscounted put, Hundsdorfer", [[3.9, 5.3, 15], [4.0, 5.2, 11]], [0.05, 0.02], None,
     [0.6, 0.4], [PUT, 100.0], "European", 15, HUNDSDORFER),
    ("two dimensions, American put", [[3.9, 5.3, 13], [4.0, 5.2, 9]], [0.05, 0.02], 0.04, [0.5, 0.5], [PUT, 100.0],
     "American", 10, DOUGLAS),
    ("three dimensions, call", [[4.0, 5.2, 7], [4.1, 5.1, 6], [4.2, 5.0, 5]], [0.06, 0.03, 0.01], 0.03,
     [0.4, 0.3, 0.3], [CALL, 100.0], "European", 8, DOUGLAS),
    ("four dimensions, put", [[4.0, 5.2, 5], [4.1, 5.1, 5], [4.2, 5.0, 4], [4.3, 4.9, 4]], [0.05, 0.03, 0.02, 0.01],
     0.02, [0.25, 0.25, 0.25, 0.25], [PUT, 100.0], "European", 5, DOUGLAS),
]


def cases():
    rng = random.Random(20261025)
    out = []
    with cxx.connect() as H:
        today = H.call("Date(Day,Month,Year)", 15, 10, 2026)
        st = H.settings(today)
        exercise_date = today + 365
        for name, meshers, lambdas, r, weights, payoff, ex, t_grid, scheme in CASES:
            points = [[m[0] + (0.2 + 0.6 * rng.random()) * (m[1] - m[0]) for m in meshers] for _ in range(4)]
            points.append([0.5 * (m[0] + m[1]) for m in meshers])
            exercise = ["European"] if ex == "European" else ["American", exercise_date]
            res = H.call("FdmNdimSolver::results", today, meshers, lambdas, r, weights, payoff, exercise, 1.0,
                         t_grid, 0, scheme, points)
            out.append({"name": name,
                        "inputs": {"settings": st, "today": today, "meshers": meshers, "lambdas": lambdas, "r": r,
                                   "weights": weights, "payoff": payoff, "exercise": exercise, "maturity": 1.0,
                                   "tGrid": t_grid, "dampingSteps": 0, "scheme": scheme, "points": points},
                        "expected": res})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdmndimsolver.hpp", cases)
