# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for BicubicSpline's five derivatives (ql/math/interpolations/bicubicsplineinterpolation.hpp), which the wheel
doesn't expose, each output computed by the C++ harness: 14 grids, each read at 30 points.

The grids are those of tools/vectors/gen/math/interpolations/bilinearinterpolation.py whose matrix is the grid's size
(QuantLib reads past a smaller matrix, and its derivatives in x read the x values past their end for a wider one, so
the harness refuses both), and a 20 by 20 grid of testBicubicDerivatives' function, y/10 sin(x) + cos(y). The points
are its nodes, points inside its cells, its edges, points just past its ends that the one-dimensional splines' close()
still reads as in range, and points outside it in x, in y and in both: QuantLib's derivatives skip the 2-D range check,
and the spline each differentiates refuses a point outside its own range ("interpolation range is [a, b]:
extrapolation at x not allowed") while the splines it is built from extrapolate, so derivativeX and secondDerivativeX
refuse a point outside in x and extrapolate in y, derivativeY and secondDerivativeY the reverse, and derivativeXY
refuses either.

Each case's outputs are derivativeX, derivativeY, secondDerivativeX, secondDerivativeY and derivativeXY at each point,
as lists over the points, a refused value {"throws": true}. Each case's inputs hold the grid ("x", "y" and "z", the
matrix's rows, a row per y value and a column per x value) and the points as [x, y] pairs. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/BicubicSplineInterpolationCxxVectorTests.cs."""
import importlib.util
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_bilinearinterpolation", os.path.join(v.GEN_ROOT, "math", "interpolations", "bilinearinterpolation.py"))
bil = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bil)

EPS = bil.EPS
DERIVATIVES = ["derivativeX", "derivativeY", "secondDerivativeX", "secondDerivativeY", "derivativeXY"]
POINTS = 30


def grids(rnd):
    """(name, x, y, z) for the grids whose matrix is the grid's size, and testBicubicDerivatives' function."""
    out = [g for g in bil.grids(rnd) if len(g[3]) == len(g[2]) and len(g[3][0]) == len(g[1])][:13]
    xs = ys = [i / 4.0 for i in range(20)]
    out.append(("y/10 sin(x) + cos(y), 20 by 20", xs, ys, [[y / 10 * math.sin(x) + math.cos(y) for x in xs] for y in ys]))
    return out


def sample(rnd, xs, ys):
    x0, x1, y0, y1 = xs[0], xs[-1], ys[0], ys[-1]
    sx, sy = x1 - x0, y1 - y0
    points = [(x0, y0), (x1, y1), (xs[len(xs) // 2], ys[len(ys) // 2])]
    points += [(rnd.uniform(x0, x1), rnd.uniform(y0, y1)) for _ in range(16)]
    points += [(x0, rnd.uniform(y0, y1)), (rnd.uniform(x0, x1), y1)]
    points += [(x0 - 10 * EPS * abs(x0) if x0 != 0.0 else -1e-300, rnd.uniform(y0, y1))]
    points += [(x1 + 0.2 * sx, rnd.uniform(y0, y1)), (x0 - 0.1 * sx, rnd.uniform(y0, y1)),
               (rnd.uniform(x0, x1), y1 + 0.3 * sy), (rnd.uniform(x0, x1), y0 - 0.2 * sy), (x1 + sx, y1 + sy)]
    while len(points) < POINTS:
        points.append((rnd.uniform(x0, x1), rnd.uniform(y0, y1)))
    return points


def cases():
    rnd = random.Random(v.SEED)
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    with cxx.connect() as H:
        for name, xs, ys, z in grids(rnd):
            spec = {"x": v.enc(xs), "y": v.enc(ys), "z": v.enc(z)}
            points = sample(rnd, xs, ys)
            expected = {d: [v.attempt(lambda p=p, d=d: H.call(f"BicubicSpline::{d}", spec, p[0], p[1])) for p in points]
                        for d in DERIVATIVES}
            out.append({"name": name, "inputs": {"settings": st, **spec, "points": v.enc([list(p) for p in points])},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/bicubicsplineinterpolation.hpp", cases)
