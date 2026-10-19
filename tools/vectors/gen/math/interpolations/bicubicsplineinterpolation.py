# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BicubicSpline (ql/math/interpolations/bicubicsplineinterpolation.hpp): the 20 grids of
tools/vectors/gen/math/interpolations/bilinearinterpolation.py (square and not, equal and irregular spacing, a spacing of
1e-6, flat and steep surfaces, two whose matrix has more rows or columns than the grid, which QuantLib reads in its
first ones), each read at its 50 points, and 3 grids built on the Bicubic factory, each read at 50 points: its corners
and some of its nodes, points inside each cell, points on its edges, points just past its ends that QuantLib's close()
still reads as in range, points outside it with the extrapolation flag set (each row's natural spline extrapolated,
then the spline through their values) and without it (refused), and NaN coordinates, refused unless extrapolating.

- "grid" cases: the value at each point with its flag, as a list over the points.
- "refusals" cases: grids QuantLib refuses to build, with fewer than 2 x or y values.

A refused value is {"throws": true}. Each case's inputs hold the grid, "x", "y" and "z" (the matrix's rows, a row per y
value and a column per x value), the points as [x, y] pairs with their flags, and "factory", whether nanoql builds the
interpolation with its Bicubic factory. The wheel's BicubicSpline is SWIG's SafeInterpolation2D<QuantLib::BicubicSpline>
(interpolation.i), which copies the arrays and the matrix and forwards each call to the C++ class with its flag; its
Bicubic is an empty struct with no interpolate(), so the factory's grids are the class's, as QuantLib's Bicubic::
interpolate returns BicubicSpline(xBegin, xEnd, yBegin, yEnd, z). No grid has a matrix smaller than the grid, which
QuantLib reads past and nanoql refuses (Divergences/Interpolation2DSizeTests.cs). The derivatives, which the wheel
doesn't bind, are the harness's (cxx/math/interpolations/bicubicsplineinterpolation). Read back by
tests/NanoQL.Tests/Vectors/Numerics/Interpolations/BicubicSplineInterpolationVectorTests.cs."""
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

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_bilinearinterpolation", os.path.join(v.GEN_ROOT, "math", "interpolations", "bilinearinterpolation.py"))
bil = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bil)


def factory_grids(rnd):
    """(name, x, y, z) for the factory's grids."""
    xs = [0.0, 0.25, 0.5, 1.0, 2.0, 3.0]
    ys = [0.5, 1.0, 2.0, 5.0]
    out = [("factory: six by four", xs, ys, [[math.sin(x) * math.exp(-0.1 * y) for x in xs] for y in ys])]
    xs = bil.distinct_sorted(rnd, 5, 0.0, 1.0)
    ys = bil.distinct_sorted(rnd, 5, 0.0, 1.0)
    out.append(("factory: random five", xs, ys, [[rnd.uniform(-1.0, 1.0) for _ in xs] for _ in ys]))
    xs = [0.01, 0.02, 0.03, 0.05, 0.07, 0.1]
    ys = [1.0, 2.0, 5.0, 10.0]
    out.append(("factory: a cap volatility surface", xs, ys,
                [[0.15 + 0.1 * math.exp(-0.2 * y) + 4.0 * (x - 0.035) ** 2 for x in xs] for y in ys]))
    return out


def grid_case(rnd, st, name, xs, ys, z, factory):
    points, allow = bil.sample(rnd, xs, ys)
    f = ql.BicubicSpline(xs, ys, bil.matrix(z))
    return {"name": name,
            "inputs": {"settings": st, "kind": "grid", "factory": factory, "x": v.enc(xs), "y": v.enc(ys),
                       "z": v.enc(z), "points": v.enc([list(p) for p in points]), "allowExtrapolation": allow},
            "expected": {"value": [v.attempt(lambda p=p, a=a: f(p[0], p[1], a)) for p, a in zip(points, allow)]}}


def refusal_case(st, name, xs, ys, z):
    def build():
        m = ql.Matrix(len(z), len(z[0]) if z else 0)
        for i, row in enumerate(z):
            for j, value in enumerate(row):
                m[i][j] = value
        return ql.BicubicSpline(xs, ys, m)(xs[0] if xs else 0.0, ys[0], True)

    return {"name": f"refusal {name}",
            "inputs": {"settings": st, "kind": "refusal", "factory": False, "x": v.enc(xs), "y": v.enc(ys),
                       "z": v.enc(z)},
            "expected": {"value": v.attempt(build)}}


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = [grid_case(rnd, st, *g, False) for g in bil.grids(rnd)]
    out += [grid_case(rnd, st, *g, True) for g in factory_grids(rnd)]
    out += [refusal_case(st, *r) for r in bil.REFUSALS]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/bicubicsplineinterpolation.hpp", cases)
