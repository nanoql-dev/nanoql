# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BilinearInterpolation (ql/math/interpolations/bilinearinterpolation.hpp): 20 grids of 2 to
12 x values by 2 to 9 y values (square and not, equal and irregular spacing, a spacing of 1e-6, flat and steep
surfaces, two whose matrix has more rows or columns than the grid, which QuantLib reads in its first ones), each read
at 50 points: its corners and some of its nodes, points inside each cell, points on its edges, points just past its
ends that QuantLib's close() still reads as in range, points outside it with the extrapolation flag set (the nearest
cell's bilinear form extended) and without it (refused), and NaN coordinates, refused unless extrapolating.

- "grid" cases: the value at each point with its flag, as a list over the points.
- "refusals" cases: grids QuantLib refuses to build, with fewer than 2 x or y values.

A refused value is {"throws": true}. Each case's inputs hold the grid, "x", "y" and "z" (the matrix's rows, a row per y
value and a column per x value), and the points as [x, y] pairs with their flags. The wheel's BilinearInterpolation is
SWIG's SafeInterpolation2D<QuantLib::BilinearInterpolation> (interpolation.i), which copies the arrays and the matrix
and forwards each call to the C++ class with its flag. Read back by
tests/NanoQL.Tests/Vectors/Numerics/Interpolations/BilinearInterpolationVectorTests.cs."""
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

EPS = 2.220446049250313e-16
POINTS = 50
NAN = float("nan")


def close(x, y):
    """QuantLib's close(x, y): within 42 machine epsilons of both, relatively."""
    if x == y:
        return True
    diff, tolerance = abs(x - y), 42 * EPS
    if x == 0.0 or y == 0.0:
        return diff < tolerance * tolerance
    return diff <= tolerance * abs(x) and diff <= tolerance * abs(y)


def in_range(p, xs):
    """QuantLib's isInRange in one coordinate."""
    return (xs[0] <= p <= xs[-1]) or close(p, xs[0]) or close(p, xs[-1])


def distinct_sorted(rnd, n, lo, hi):
    xs = sorted({rnd.uniform(lo, hi) for _ in range(n)})
    assert len(xs) == n
    return xs


def grids(rnd):
    """(name, x, y, z), z a list of rows, a row per y value."""
    out = []

    def add(name, xs, ys, f, extra_rows=0, extra_columns=0):
        z = [[f(x, y) for x in xs] + [rnd.uniform(-9.0, 9.0) for _ in range(extra_columns)] for y in ys]
        z += [[rnd.uniform(-9.0, 9.0) for _ in range(len(xs) + extra_columns)] for _ in range(extra_rows)]
        out.append((name, xs, ys, z))

    add("two by two", [0.0, 1.0], [0.0, 1.0], lambda x, y: 1.0 + 2.0 * x + 3.0 * y + 4.0 * x * y)
    add("three by two", [1.0, 2.0, 3.0], [10.0, 20.0], lambda x, y: 100.0 * x + y)
    add("two by four", [-1.0, 1.0], [0.0, 0.5, 1.5, 4.0], lambda x, y: x * x - y)
    add("square five", [0.0, 1.0, 2.0, 3.0, 4.0], [0.0, 1.0, 2.0, 3.0, 4.0], lambda x, y: math.sin(x) * math.cos(y))
    add("flat", [0.0, 0.5, 1.0], [2.0, 3.0, 5.0, 8.0], lambda x, y: 0.25)
    add("steep", [0.0, 1e-3, 2e-3, 1.0], [0.0, 1.0, 2.0], lambda x, y: math.exp(5.0 * x + y))
    add("spacing 1e-6", [1.0 + i * 1e-6 for i in range(6)], [2.0 + j * 1e-6 for j in range(4)],
        lambda x, y: (x - 1.0) * 1e6 + (y - 2.0) * 3e6)
    add("option by swap", [0.25, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0, 30.0], [1.0, 2.0, 5.0, 10.0, 30.0],
        lambda x, y: 0.2 + 0.05 * math.exp(-x / 3.0) - 0.002 * y)
    add("negative axes", [-5.0, -2.0, -1.0, 0.0], [-3.0, -0.5], lambda x, y: x * y)
    add("wide", [float(i) for i in range(12)], [0.0, 10.0], lambda x, y: math.sqrt(1.0 + x) + y)
    add("tall", [0.0, 1.0], [float(j) * j for j in range(9)], lambda x, y: x - math.log1p(y))
    xs, ys = distinct_sorted(rnd, 6, -3.0, 3.0), distinct_sorted(rnd, 4, 0.0, 2.0)
    add("irregular six by four", xs, ys, lambda x, y: rnd.uniform(-1.0, 1.0))
    xs, ys = distinct_sorted(rnd, 3, 0.0, 1.0), distinct_sorted(rnd, 7, -1.0, 1.0)
    add("irregular three by seven", xs, ys, lambda x, y: rnd.uniform(-5.0, 5.0))
    xs, ys = distinct_sorted(rnd, 9, 0.0, 100.0), distinct_sorted(rnd, 9, 0.0, 100.0)
    add("random nine", xs, ys, lambda x, y: rnd.uniform(0.0, 1.0))
    add("day counts", [31.0, 92.0, 182.0, 365.0, 730.0], [365.0, 1826.0, 3652.0],
        lambda x, y: math.exp(-0.03 * (x + y) / 365.0))
    add("large values", [1e5, 2e5, 4e5], [1e6, 3e6], lambda x, y: x * y)
    add("tiny values", [1e-9, 2e-9, 3e-9], [1e-12, 5e-12, 9e-12], lambda x, y: x + y)
    add("zero row", [0.0, 1.0, 2.0], [0.0, 1.0, 2.0], lambda x, y: 0.0 if y == 1.0 else x + y)
    add("extra rows", [0.0, 1.0, 3.0], [0.0, 2.0], lambda x, y: x - 2.0 * y, extra_rows=2)
    add("extra columns", [0.0, 0.5], [1.0, 2.0, 3.0], lambda x, y: x * 10.0 + y, extra_columns=3)
    return out


def sample(rnd, xs, ys):
    """50 points and their extrapolation flags: the flag is set outside the range, but not at four of the points
    outside, where QuantLib throws."""
    x0, x1, y0, y1 = xs[0], xs[-1], ys[0], ys[-1]
    sx, sy = x1 - x0, y1 - y0
    points = [(x0, y0), (x1, y0), (x0, y1), (x1, y1)]
    points += [(xs[i], ys[j]) for i, j in ((len(xs) // 2, len(ys) // 2), (1, len(ys) - 2), (len(xs) - 2, 1))]
    points += [(x0, rnd.uniform(y0, y1)), (x1, rnd.uniform(y0, y1)), (rnd.uniform(x0, x1), y0),
               (rnd.uniform(x0, x1), y1)]
    # just past the ends, still in range by close()
    points += [(x0 - 10 * EPS * abs(x0) if x0 != 0.0 else -1e-300, rnd.uniform(y0, y1)),
               (rnd.uniform(x0, x1), y1 + 10 * EPS * abs(y1) if y1 != 0.0 else 1e-300)]
    # outside, extrapolating
    extrapolated = [(x0 - 0.1 * sx, rnd.uniform(y0, y1)), (x1 + 0.5 * sx, rnd.uniform(y0, y1)),
                    (rnd.uniform(x0, x1), y0 - 0.25 * sy), (rnd.uniform(x0, x1), y1 + sy),
                    (x0 - sx, y0 - sy), (x1 + 2.0 * sx, y1 + 0.1 * sy), (x0 - 0.5 * sx, y1 + 0.5 * sy),
                    (NAN, rnd.uniform(y0, y1)), (rnd.uniform(x0, x1), NAN)]
    # outside, refused
    refused = [(x1 + 0.3 * sx, rnd.uniform(y0, y1)), (rnd.uniform(x0, x1), y0 - sy), (x0 - sx, y1 + sy),
               (NAN, NAN)]
    points += extrapolated + refused
    while len(points) < POINTS:
        points.append((rnd.uniform(x0, x1), rnd.uniform(y0, y1)))
    allow = [not (in_range(p[0], xs) and in_range(p[1], ys)) and p not in refused for p in points]
    for p in extrapolated:
        assert allow[points.index(p)] or (p[0] != p[0] or p[1] != p[1]), p
    return points, allow


def matrix(z):
    m = ql.Matrix(len(z), len(z[0]))
    for i, row in enumerate(z):
        for j, value in enumerate(row):
            m[i][j] = value
    return m


def grid_case(rnd, st, name, xs, ys, z):
    points, allow = sample(rnd, xs, ys)
    f = ql.BilinearInterpolation(xs, ys, matrix(z))
    return {"name": name,
            "inputs": {"settings": st, "kind": "grid", "x": v.enc(xs), "y": v.enc(ys), "z": v.enc(z),
                       "points": v.enc([list(p) for p in points]), "allowExtrapolation": allow},
            "expected": {"value": [v.attempt(lambda p=p, a=a: f(p[0], p[1], a)) for p, a in zip(points, allow)]}}


REFUSALS = [
    ("one x value", [1.0], [0.0, 1.0], [[1.0], [2.0]]),
    ("one y value", [0.0, 1.0, 2.0], [5.0], [[1.0, 2.0, 3.0]]),
    ("no x values", [], [0.0, 1.0], [[], []]),
    ("one by one", [1.0], [1.0], [[4.0]]),
]


def refusal_case(st, name, xs, ys, z):
    def build():
        m = ql.Matrix(len(z), len(z[0]) if z else 0)
        for i, row in enumerate(z):
            for j, value in enumerate(row):
                m[i][j] = value
        return ql.BilinearInterpolation(xs, ys, m)(xs[0] if xs else 0.0, ys[0], True)

    return {"name": f"refusal {name}",
            "inputs": {"settings": st, "kind": "refusal", "x": v.enc(xs), "y": v.enc(ys), "z": v.enc(z)},
            "expected": {"value": v.attempt(build)}}


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = [grid_case(rnd, st, *g) for g in grids(rnd)]
    out += [refusal_case(st, *r) for r in REFUSALS]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/bilinearinterpolation.hpp", cases)
