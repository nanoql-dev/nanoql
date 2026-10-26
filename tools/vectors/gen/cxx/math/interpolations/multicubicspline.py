# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiCubicSpline (ql/math/interpolations/multicubicspline.hpp), which the wheel doesn't
expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4): splines in one to five dimensions on
grids of four to six points a dimension, uniform and not, through a smooth function's values, evaluated at seeded
points inside the grid, at interior grid nodes, on the lower boundary, below it with and without extrapolation (flat
there, or refused), and beyond the upper boundary without extrapolation (refused); and grids QuantLib refuses (three
points, a repeated point).

The last grid point of a dimension isn't asked, which QuantLib refuses as extrapolation and nanoql interpolates, nor a
point beyond it with extrapolation allowed, where QuantLib reads past the data
(tests/NanoQL.Tests/Divergences/MultiCubicSplineBoundaryTests.cs).

The harness's call is tools/cxx/src/math/interpolations/multicubicspline.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Math/Interpolations/MultiCubicSplineCxxVectorTests.cs."""
import itertools
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


def f(x):
    return math.sqrt(2.0 + sum(math.sin((k + 1) * xi) * math.exp(-0.1 * xi * xi) for k, xi in enumerate(x)))


GRIDS = [
    ("one dimension", [[0.0, 0.5, 1.2, 1.5, 2.3, 3.0]]),
    ("two dimensions", [[-1.0, -0.4, 0.1, 0.7, 1.0], [0.0, 1.0, 2.0, 3.0]]),
    ("three dimensions", [[0.0, 0.3, 0.6, 0.9], [1.0, 1.5, 2.5, 3.0, 3.5], [-0.5, 0.0, 0.5, 1.0]]),
    ("four dimensions", [[0.0, 0.25, 0.5, 0.75, 1.0], [0.0, 1.0, 2.0, 3.0], [0.0, 0.5, 1.0, 1.5],
                         [2.0, 2.2, 2.6, 3.0]]),
    ("five dimensions", [[0.0, 0.3, 0.6, 0.9], [1.0, 1.5, 2.0, 2.5], [0.0, 0.2, 0.4, 0.6, 0.8], [3.0, 3.1, 3.3, 3.6],
                         [-1.0, 0.0, 1.0, 2.0]]),
]


def values_of(grid):
    return [f(list(p)) for p in itertools.product(*grid)]


def points(grid, rng):
    n = len(grid)
    out = [("random", [g[0] + rng.random() * (g[-1] - g[0]) for g in grid]) for _ in range(6)]
    out.append(("interior node", [g[1] for g in grid]))
    out.append(("lower boundary", [g[0] for g in grid]))
    below = [g[0] for g in grid]
    below[0] -= 0.1
    out.append(("below the grid", below))
    beyond = [g[1] for g in grid]
    beyond[n - 1] = grid[n - 1][-1] + 0.1
    out.append(("beyond the grid", beyond))
    return out


def cases():
    rng = random.Random(20261025)
    out = []
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        for name, grid in GRIDS:
            values = values_of(grid)
            for flags_name, flags in (("", []), (", extrapolating", [True] * len(grid))):
                pts = points(grid, rng)
                res = [v.attempt(lambda x=x: H.call("MultiCubicSpline::operator()", grid, values, flags, x))
                       for _, x in pts if not (flags and _ == "beyond the grid")]
                xs = [x for pname, x in pts if not (flags and pname == "beyond the grid")]
                out.append({"name": name + flags_name,
                            "inputs": {"settings": st, "grid": grid, "values": values, "extrapolation": flags,
                                       "points": xs},
                            "expected": {"value": res}})
        for name, grid in (("three points (refused)", [[0.0, 1.0, 2.0]]),
                           ("a repeated point (refused)", [[0.0, 1.0, 1.0, 2.0, 3.0]])):
            values = values_of(grid)
            out.append({"name": name, "inputs": {"settings": st, "grid": grid, "values": values, "extrapolation": [],
                                                 "points": [[0.5]]},
                        "expected": {"value": [v.attempt(
                            lambda: H.call("MultiCubicSpline::operator()", grid, values, [], [0.5]))]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/multicubicspline.hpp", cases)
