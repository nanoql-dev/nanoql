# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LinearInterpolation (ql/math/interpolations/linearinterpolation.hpp): ten node sets of 2 to
30 points (equal and irregular spacing, flat stretches, day counts, a spacing of 1e-6, year fractions), each read at 50
points: its ends and some of its nodes, points inside the range, points just past its ends that QuantLib's close() still
reads as in range, and points outside it. At each point the value, derivative, second derivative and primitive, with
the extrapolation flag set outside the range, except at the two farthest points, where QuantLib refuses to
extrapolate. The wheel's LinearInterpolation is SWIG's SafeInterpolation<QuantLib::LinearInterpolation>
(interpolation.i), which copies the two arrays and forwards each call to the C++ class with its flag. Read back by
tests/NanoQL.Tests/Vectors/Numerics/Interpolations/LinearInterpolationVectorTests.cs."""
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


def close(x, y):
    """QuantLib's close(x, y): within 42 machine epsilons of both, relatively."""
    if x == y:
        return True
    diff, tolerance = abs(x - y), 42 * EPS
    if x == 0.0 or y == 0.0:
        return diff < tolerance * tolerance
    return diff <= tolerance * abs(x) and diff <= tolerance * abs(y)


def in_range(x, xs):
    """QuantLib's isInRange."""
    return (xs[0] <= x <= xs[-1]) or close(x, xs[0]) or close(x, xs[-1])


def distinct_sorted(rnd, n, lo, hi):
    xs = sorted({rnd.uniform(lo, hi) for _ in range(n)})
    assert len(xs) == n
    return xs


def node_sets(rnd):
    days = [0.0, 31.0, 92.0, 182.0, 365.0, 730.0, 1095.0, 1826.0, 3652.0, 7305.0]
    years = [0.25 * (i + 1) for i in range(8)] + [2.5 + i for i in range(22)]
    sets = [
        ("two points", [0.0, 1.0], [1.0, 3.0]),
        ("three points", [-1.0, 0.5, 2.0], [2.0, -1.0, 0.5]),
        ("equal spacing", [0.0, 1.0, 2.0, 3.0, 4.0], [5.0, 4.0, 3.0, 2.0, 1.0]),
        ("flat stretches", [0.0, 0.5, 1.5, 2.0, 3.5, 4.0, 6.0, 7.0], [1.0, 1.0, 2.0, 2.0, 2.0, -0.5, -0.5, 3.0]),
    ]
    xs = distinct_sorted(rnd, 6, -3.0, 3.0)
    sets.append(("irregular six", xs, [rnd.uniform(-2.0, 2.0) for _ in xs]))
    sets.append(("day counts", days, [math.exp(-0.035 * d / 365.0) for d in days]))
    xs = [1.0 + i * 1e-6 for i in range(12)]
    sets.append(("spacing 1e-6", xs, [rnd.uniform(-1.0, 1.0) for _ in xs]))
    xs = distinct_sorted(rnd, 15, 0.0, 10.0)
    sets.append(("random fifteen", xs, [rnd.uniform(-5.0, 5.0) for _ in xs]))
    xs = distinct_sorted(rnd, 20, -5.0, 5.0)
    sets.append(("random twenty", xs, [rnd.uniform(0.0, 1.0) for _ in xs]))
    sets.append(("year fractions", years, [0.01 + 0.03 * (1.0 - math.exp(-t / 5.0)) for t in years]))
    return sets


def sample(rnd, xs):
    """50 points and their extrapolation flags: the flag is set outside the range, but not at the two farthest
    points, where QuantLib throws."""
    lo, hi = xs[0], xs[-1]
    span = hi - lo if hi > lo else 1.0
    step = max(1, len(xs) // 8)
    points = [lo, hi] + xs[::step][:8]
    # just past the ends, still in range by close()
    points += [lo - 10 * EPS * abs(lo) if lo != 0.0 else -1e-300, hi + 10 * EPS * abs(hi) if hi != 0.0 else 1e-300]
    nearest = [lo - 0.01 * span, lo - 0.5 * span, hi + 0.01 * span, hi + 0.5 * span]
    farthest = [lo - 2.0 * span, hi + 2.0 * span]
    points += nearest + farthest
    while len(points) < POINTS:
        points.append(rnd.uniform(lo, hi))
    allow = [not in_range(p, xs) and p not in farthest for p in points]
    return points, allow


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for name, xs, ys in node_sets(rnd):
        points, allow = sample(rnd, xs)
        f = ql.LinearInterpolation(xs, ys)
        out.append({"name": name,
                    "inputs": {"settings": st, "x": v.enc(xs), "y": v.enc(ys), "points": v.enc(points),
                               "allowExtrapolation": allow},
                    "expected": {
                        "value": [v.attempt(lambda p=p, a=a: f(p, a)) for p, a in zip(points, allow)],
                        "derivative": [v.attempt(lambda p=p, a=a: f.derivative(p, a)) for p, a in zip(points, allow)],
                        "secondDerivative": [v.attempt(lambda p=p, a=a: f.secondDerivative(p, a))
                                             for p, a in zip(points, allow)],
                        "primitive": [v.attempt(lambda p=p, a=a: f.primitive(p, a)) for p, a in zip(points, allow)]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/linearinterpolation.hpp", cases)
