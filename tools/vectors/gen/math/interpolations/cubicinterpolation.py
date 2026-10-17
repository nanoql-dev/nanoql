# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the six named cubic interpolations the wheel exposes (ql/math/interpolations/cubicinterpolation.hpp):
CubicNaturalSpline, MonotonicCubicNaturalSpline, KrugerCubic, FritschButlandCubic, Parabolic and MonotonicParabolic,
each on ten node sets of 2 to 25 points (two and three points, which the local schemes read apart; QuantLib's RPN15A
and Gaussian test data; flat stretches; slopes -1 and 2 side by side, Fritsch-Butland's limiting case; monotonic,
decreasing, oscillating and random data), each read at 50 points: its ends and some of its nodes, points inside the
range, points just past its ends that QuantLib's close() still reads as in range, and points outside it. At each point
the value, derivative, second derivative and primitive, with the extrapolation flag set outside the range, except at
the two farthest points, where QuantLib refuses to extrapolate. Each wheel class is SWIG's SafeInterpolation over the
C++ class of the same name (interpolation.i), which copies the two arrays and forwards each call to it with its flag.
Read back by tests/NanoQL.Tests/Vectors/Numerics/Interpolations/CubicInterpolationVectorTests.cs."""
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
CLASSES = ["CubicNaturalSpline", "MonotonicCubicNaturalSpline", "KrugerCubic", "FritschButlandCubic", "Parabolic",
           "MonotonicParabolic"]


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


def x_range(start, finish, points):
    """interpolations.cpp's xRange."""
    dx = (finish - start) / (points - 1)
    return [start + i * dx for i in range(points - 1)] + [finish]


def node_sets(rnd):
    gaussian_x = x_range(-1.7, 1.9, 9)
    sets = [
        ("two points", [0.0, 1.0], [1.0, 3.0]),
        ("three points", [-1.0, 0.5, 2.0], [2.0, -1.0, 0.5]),
        ("RPN15A", [7.99, 8.09, 8.19, 8.7, 9.2, 10.0, 12.0, 15.0, 20.0],
         [0.0, 2.76429e-5, 4.37498e-5, 0.169183, 0.469428, 0.943740, 0.998636, 0.999919, 0.999994]),
        ("Gaussian", gaussian_x, [math.exp(-x * x) for x in gaussian_x]),
        ("flat stretches", [0.0, 0.5, 1.5, 2.0, 3.5, 4.0, 6.0, 7.0], [1.0, 1.0, 2.0, 2.0, 2.0, -0.5, -0.5, 3.0]),
        ("slopes -1 and 2", [0.0, 1.0, 2.0, 3.0, 4.0, 5.0], [0.0, -1.0, 1.0, 1.5, 0.5, 2.0]),
    ]
    xs = distinct_sorted(rnd, 7, 0.0, 10.0)
    sets.append(("increasing", xs, sorted(rnd.uniform(0.01, 0.06) for _ in xs)))
    xs = distinct_sorted(rnd, 12, 0.0, 30.0)
    sets.append(("decreasing", xs, [math.exp(-0.04 * t) for t in xs]))
    xs = distinct_sorted(rnd, 16, -4.0, 4.0)
    sets.append(("oscillating", xs, [math.sin(2.0 * t) + rnd.uniform(-0.1, 0.1) for t in xs]))
    xs = distinct_sorted(rnd, 25, 0.0, 30.0)
    sets.append(("random", xs, [rnd.uniform(-5.0, 5.0) for _ in xs]))
    return sets


def sample(rnd, xs):
    """50 points and their extrapolation flags: the flag is set outside the range, but not at the two farthest
    points, where QuantLib throws."""
    lo, hi = xs[0], xs[-1]
    span = hi - lo
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
    sets = [(name, xs, ys) + sample(rnd, xs) for name, xs, ys in node_sets(rnd)]
    out = []
    for cls in CLASSES:
        for name, xs, ys, points, allow in sets:
            f = getattr(ql, cls)(xs, ys)
            pa = list(zip(points, allow))
            out.append({"name": f"{cls} {name}",
                        "inputs": {"settings": st, "type": cls, "x": v.enc(xs), "y": v.enc(ys),
                                   "points": v.enc(points), "allowExtrapolation": allow},
                        "expected": {
                            "value": [v.attempt(lambda p=p, a=a: f(p, a)) for p, a in pa],
                            "derivative": [v.attempt(lambda p=p, a=a: f.derivative(p, a)) for p, a in pa],
                            "secondDerivative": [v.attempt(lambda p=p, a=a: f.secondDerivative(p, a)) for p, a in pa],
                            "primitive": [v.attempt(lambda p=p, a=a: f.primitive(p, a)) for p, a in pa]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/cubicinterpolation.hpp", cases)
