# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the log interpolations the wheel exposes (ql/math/interpolations/loginterpolation.hpp):
LogLinearInterpolation and the six named log-cubic interpolations, LogCubicNaturalSpline,
MonotonicLogCubicNaturalSpline, KrugerLogCubic, FritschButlandLogCubic, LogParabolic and MonotonicLogParabolic. Each
on ten node sets of 2 to 30 positive points (two and three points; day counts and year fractions of discount factors;
flat stretches; QuantLib's Gaussian test data; increasing, oscillating and random data; values near 1e-9), each read at
50 points: its ends and some of its nodes, points inside the range, points just past its ends that QuantLib's close()
still reads as in range, and points outside it. At each point the value, derivative, second derivative and primitive
(which QuantLib doesn't implement, so it throws), with the extrapolation flag set outside the range, except at the two
farthest points, where QuantLib refuses to extrapolate. Then each class's refusal of a y value of 0. Each wheel class is
SWIG's SafeInterpolation over the C++ class of the same name (interpolation.i), which copies the two arrays and
forwards each call to it with its flag. Read back by
tests/NanoQL.Tests/Vectors/Numerics/Interpolations/LogInterpolationVectorTests.cs."""
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
CLASSES = ["LogLinearInterpolation", "LogCubicNaturalSpline", "MonotonicLogCubicNaturalSpline", "KrugerLogCubic",
           "FritschButlandLogCubic", "LogParabolic", "MonotonicLogParabolic"]
REFUSED = ([0.0, 0.5, 1.0, 2.0], [1.0, 0.5, 0.0, 2.0])


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
    days = [0.0, 31.0, 92.0, 182.0, 365.0, 730.0, 1095.0, 1826.0, 3652.0, 7305.0]
    years = [0.25 * (i + 1) for i in range(8)] + [2.5 + i for i in range(22)]
    gaussian_x = x_range(-1.7, 1.9, 9)
    sets = [
        ("two points", [0.0, 1.0], [1.0, 0.5]),
        ("three points", [0.5, 1.0, 2.0], [0.9, 1.4, 1.1]),
        ("day counts", days, [math.exp(-0.035 * d / 365.0) for d in days]),
        ("year fractions", years, [math.exp(-(0.01 + 0.03 * (1.0 - math.exp(-t / 5.0))) * t) for t in years]),
        ("flat stretches", [0.0, 0.5, 1.5, 2.0, 3.5, 4.0, 6.0, 7.0], [1.0, 1.0, 2.0, 2.0, 2.0, 0.5, 0.5, 3.0]),
        ("Gaussian", gaussian_x, [math.exp(-x * x) for x in gaussian_x]),
    ]
    xs = distinct_sorted(rnd, 7, 0.0, 10.0)
    sets.append(("increasing", xs, sorted(rnd.uniform(0.5, 3.0) for _ in xs)))
    xs = distinct_sorted(rnd, 16, -4.0, 4.0)
    sets.append(("oscillating", xs, [1.2 + math.sin(2.0 * t) + rnd.uniform(-0.1, 0.1) for t in xs]))
    xs = distinct_sorted(rnd, 25, 0.0, 30.0)
    sets.append(("random", xs, [rnd.uniform(0.5, 5.0) for _ in xs]))
    xs = distinct_sorted(rnd, 12, 0.0, 5.0)
    sets.append(("near 1e-9", xs, [rnd.uniform(1e-10, 1e-8) for _ in xs]))
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
    # The constructor's refusal of a y value that isn't positive: QuantLib computes the logarithms at construction.
    xs, ys = REFUSED
    for cls in CLASSES:
        out.append({"name": f"{cls} refuses a y value of 0",
                    "inputs": {"settings": st, "type": cls, "x": xs, "y": ys, "points": [0.25],
                               "allowExtrapolation": [False]},
                    "expected": {"construction": v.attempt(lambda c=cls: getattr(ql, c)(xs, ys)(0.25))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/loginterpolation.hpp", cases)
