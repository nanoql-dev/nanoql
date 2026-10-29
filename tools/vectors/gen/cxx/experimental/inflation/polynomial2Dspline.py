# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's 2-D polynomial spline (ql/experimental/inflation/polynomial2Dspline.hpp), which the wheel
doesn't bind, each output computed by the C++ harness (tools/cxx/src/experimental/inflation/polynomial2dspline.cpp).

- "values" cases: grids of six times by five strikes and of three by two, their values smooth, a kink and a step, read
  on the nodes, between them and outside the grid, with and without extrapolation (refused without it outside).
- "refused" cases: z's rows not one per y value.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Inflation/Polynomial2DSplineCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

NAME = "Polynomial2DSpline::values"
X = [1.0, 2.0, 3.0, 5.0, 7.0, 10.0]
Y = [-0.01, 0.0, 0.01, 0.02, 0.03]
SURFACES = [
    ("smooth", X, Y, [[0.02 * x * math.exp(-30.0 * y) + 0.001 * x * x for x in X] for y in Y]),
    ("a kink", X, Y, [[max(0.0, 0.025 - y) * x + 0.0005 * x for x in X] for y in Y]),
    ("a step", X, Y, [[(1.0 if x >= 5.0 else 0.0) + y for x in X] for y in Y]),
    ("three by two", [0.5, 1.5, 4.0], [0.0, 0.05], [[1.0, 2.0, 2.5], [0.5, 1.75, 3.0]]),
]


def points(x, y):
    inside = [[xi, yj] for xi in x for yj in y]
    between = [[(x[i] + x[i + 1]) / 2, (y[j] + y[j + 1]) / 2] for i in range(len(x) - 1) for j in range(len(y) - 1)]
    outside = [[x[0] - 0.5, y[0]], [x[-1] + 2.0, y[-1]], [x[1], y[0] - 0.01], [x[-1] + 1.0, y[-1] + 0.02]]
    return inside + between + outside


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(45000)
        for label, x, y, z in SURFACES:
            pts = points(x, y)
            for extrapolate in (False, True):
                out.append({"name": f"{label}, extrapolation {extrapolate}",
                            "inputs": {"settings": st, "kind": "values", "x": x, "y": y, "z": z, "points": pts,
                                       "allowExtrapolation": extrapolate},
                            "expected": {"values": H.call(NAME, x, y, z, pts, extrapolate)}})
        z = SURFACES[0][3][:-1]
        out.append({"name": "refused: a row short",
                    "inputs": {"settings": st, "kind": "refused", "x": X, "y": Y, "z": z, "points": [],
                               "allowExtrapolation": False},
                    "expected": {"construction": v.attempt(lambda: H.call(NAME, X, Y, z, [], False))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/inflation/polynomial2Dspline.hpp", cases)
