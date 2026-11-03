# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Laplace interpolation, Moore-Penrose inverse and piecewise function and integral
(ql/experimental/math/laplaceinterpolation.hpp, .cpp, moorepenroseinverse.hpp, piecewisefunction.hpp,
piecewiseintegral.hpp, .cpp), which the wheel doesn't bind, each output computed by the C++ harness
(tools/cxx/src/experimental/math/laplaceandpiecewise.cpp).

- "laplace" cases: matrices from QuantLib's interpolations test (an inner point, a boundary, a corner, one row and one
  column, the non-equidistant grids, the field case at 100 iterations a point) and a 4 × 5 matrix with five missing
  values; and a 3 × 3 × 2 grid through LaplaceInterpolation itself.
- "moore-penrose" cases: tall, square and rank-deficient tall matrices, QuantLib's 8 × 6 case among them, at the default
  tolerance and at 1e-10. A wide matrix is nanoql's (A^T)^+^T, where QuantLib's reads past its singular values, left
  out.
- "piecewise" cases: QuantLib's piecewise integral case on its step function and the segment rule; the exponential over
  breaks at 0.5, 1.5 and 3 on Simpson's rule, avoiding and not; every break above 0, where nanoql's nudges are
  QuantLib's; and QL_PIECEWISE_FUNCTION at and between its breaks.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Math/LaplaceAndPiecewiseCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46311  # Friday 16 October 2026
na = None
STD = [[1.0, 2.0, 4.0], [6.0, 6.5, 7.0], [5.0, 3.0, 2.0]]


def missing(i, j):
    m = [list(r) for r in STD]
    m[i][j] = na
    return m


TX = [0.0849315, 0.257534, 0.509589, 1.00548, 2.00274, 3.00274, 4.00274, 5.00548, 7.00822, 10.0082, 15.011, 20.0137,
      30.0219, 70.0493]
TY = [0.25, 1, 2, 3, 4, 5, 7, 10, 15, 20, 30, 100]
FIELD = [[na, na] + [1.0] * 11 + [na]] * 2 + [[na] * 4 + [1.0] * 9 + [na]] + [[na, na] + [1.0] * 11 + [na]] * 2 \
    + [[na] * 3 + [1.0] * 10 + [na]] * 3 + [[na] * 3 + [1.0] * 9 + [na] * 2] + [[na] * 4 + [1.0] * 8 + [na] * 2] \
    + [[na] * 5 + [1.0] * 7 + [na] * 2] + [[na] * 14]
LAPLACE = [
    ("inner point", missing(1, 1), [], [], 1e-6, 10),
    ("boundary", missing(0, 1), [], [], 1e-6, 10),
    ("corner", missing(0, 0), [], [], 1e-6, 10),
    ("column", [[na], [na], [3.0], [5.0], [7.0], [na]], [], [], 1e-6, 10),
    ("row", [[na, na, 3.0, 5.0, 7.0, na]], [], [], 1e-6, 10),
    ("non-equidistant inner point", missing(1, 1), [1.0, 2.0, 4.0], [1.0, 2.0, 4.0], 1e-6, 10),
    ("non-equidistant corner", missing(0, 0), [1.0, 2.0, 4.0], [1.0, 3.0, 7.0], 1e-6, 10),
    ("field case", FIELD, TX, TY, 1e-6, 100),
    ("four by five", [[1.0, na, 3.0, 2.5, 4.0], [na, 2.0, 2.2, na, 3.1], [0.5, 1.5, na, 2.0, 2.5],
                      [0.7, 1.1, 1.9, 2.4, na]], [0.0, 1.0, 2.5, 4.0, 7.0], [1.0, 2.0, 3.0, 5.0], 1e-8, 10),
]
GRID = [[0.0, 1.0, 3.0], [0.0, 0.5, 2.0], [1.0, 2.0]]
GRID_VALUES = [1.0, 2.0, 3.0, 1.5, na, 3.5, 2.0, 3.0, na, na, 2.2, 3.4, 1.1, 2.1, 3.1, na, 2.6, 3.6]
MATRICES = [
    ("tall", [[1.0, 2.0, 0.5], [0.3, -1.0, 2.0], [4.0, 0.1, -0.7], [1.5, 1.5, 1.5]]),
    ("square", [[2.0, 1.0, 0.0], [1.0, 3.0, 1.0], [0.0, 1.0, 4.0]]),
    ("rank deficient", [[1.0, 2.0, 3.0], [2.0, 4.0, 6.0], [1.0, 0.0, 1.0], [0.0, 2.0, 2.0]]),
    ("QuantLib's 8 x 6", [[64, 2, 3, 61, 60, 6], [9, 55, 54, 12, 13, 51], [17, 47, 46, 20, 21, 43],
                          [40, 26, 27, 37, 36, 30], [32, 34, 35, 29, 28, 38], [41, 23, 22, 44, 45, 19],
                          [49, 15, 14, 52, 53, 11], [8, 58, 59, 5, 4, 62]]),
]
STEP = [5, 1.0, 2.0, 3.0, 4.0, 5.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0]
STEP_INTERVALS = [[-1.0, 0.0], [0.0, 1.0], [0.0, 1.5], [0.0, 2.5], [0.0, 7.0], [3.5, 4.5], [5.0, 10.0], [9.0, 10.0],
                  [2.0, 2.0], [4.5, 1.5]]
EXP_INTERVALS = [[0.0, 1.0], [0.2, 2.0], [0.5, 3.0], [1.0, 4.0], [3.5, 5.0]]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        for name, rows, x, y, tol, it in LAPLACE:
            args = [rows, x, y, tol, it]
            out.append({"name": f"laplace: {name}", "inputs": {"settings": st, "args": args},
                        "expected": H.call("laplaceInterpolation", *args)})
        args = [GRID_VALUES, GRID, 1e-8, 10]
        out.append({"name": "laplace grid: three by three by two", "inputs": {"settings": st, "args": args},
                    "expected": H.call("LaplaceInterpolation::values", *args)})
        for name, rows in MATRICES:
            for tol in (None, 1e-10):
                args = [rows, tol]
                out.append({"name": f"moore-penrose: {name}, tol {tol}", "inputs": {"settings": st, "args": args},
                            "expected": H.call("moorePenroseInverse", *args)})
        args = [[1.0, 2.0, 3.0, 4.0, 5.0], True, "segment", [1], "step", STEP, STEP_INTERVALS]
        out.append({"name": "piecewise: QuantLib's step on the segment rule", "inputs": {"settings": st, "args": args},
                    "expected": H.call("PiecewiseIntegral::integrate", *args)})
        for avoid in (True, False):
            args = [[1.5, 0.5, 3.0], avoid, "simpson", [1e-10, 20], "exp", [], EXP_INTERVALS]
            out.append({"name": f"piecewise: exp on Simpson's rule, avoid {avoid}",
                        "inputs": {"settings": st, "args": args},
                        "expected": H.call("PiecewiseIntegral::integrate", *args)})
        args = [[1.0, 2.0, 3.0], [10.0, 20.0, 30.0, 40.0], [0.5, 1.0, 1.5, 2.0, 2.999, 3.0, 4.0]]
        out.append({"name": "piecewise function: three breaks", "inputs": {"settings": st, "args": args},
                    "expected": H.call("QL_PIECEWISE_FUNCTION", *args)})
        args = [[1.0, 2.0, 3.0], [10.0, 20.0], [0.5, 1.0, 2.5, 3.0, 9.0]]
        out.append({"name": "piecewise function: fewer values", "inputs": {"settings": st, "args": args},
                    "expected": H.call("QL_PIECEWISE_FUNCTION", *args)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/laplaceinterpolation.hpp", cases)
