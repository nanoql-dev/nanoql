# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LagrangeInterpolation (ql/math/interpolations/lagrangeinterpolation.hpp): seven ascending node
sets of 2 to 21 points (equal spacing, QuantLib's own test's irregular seven, Chebyshev points, a set with a node at
zero, random points, a spacing of 1e-4), each read at its nodes, at points within a few machine epsilons of them, at
points between them, and at points past its ends: the value and derivative, with the extrapolation flag set outside
the range except at the two farthest points, where QuantLib refuses to extrapolate; and the primitive and second
derivative, which QuantLib doesn't implement, at one point.

The outputs nanoql fixes are left out: the value and derivative at x = 0 where 0 is a node, which QuantLib's node test
misses, dividing by zero (tests/NanoQL.Tests/Divergences/LagrangeZeroNodeTests.cs); and descending node sets, where
QuantLib's binary search never finds a node (the same file). The wheel's LagrangeInterpolation is SWIG's
SafeInterpolation<QuantLib::LagrangeInterpolation> (interpolation.i), which copies the two arrays and forwards each call
to the C++ class with its flag. Its value(y, x), which the wheel doesn't expose, is vectored through the C++ harness
(tools/vectors/gen/cxx/math/interpolations/lagrangeinterpolation.py). Read back by
tests/NanoQL.Tests/Vectors/Numerics/Interpolations/LagrangeInterpolationVectorTests.cs."""
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


def node_sets(rnd):
    chebyshev = sorted(math.cos((2 * i + 1) * math.pi / 22) for i in range(11))
    xs = sorted({rnd.uniform(-3.0, 5.0) for _ in range(9)})
    sets = [("two points", [0.0, 1.0], [1.0, 3.0]),
            ("equal spacing", [0.0, 1.0, 2.0, 3.0, 4.0], [5.0, 4.0, 3.5, 2.0, 1.0]),
            ("QuantLib's seven", [-1.0, -0.5, -0.25, 0.1, 0.4, 0.75, 0.96], None),
            ("Chebyshev points", chebyshev, [math.exp(x) / math.cos(x) for x in chebyshev]),
            ("node at zero", [-2.0, -1.0, 0.0, 0.5, 2.0], [1.0, -1.0, 2.0, 0.5, 3.0]),
            ("random nine", xs, [rnd.uniform(-2.0, 2.0) for _ in xs]),
            ("spacing 1e-4", [1.0 + i * 1e-4 for i in range(21)], [math.sin(10.0 * i) for i in range(21)])]
    # QuantLib's own test's function on its seven nodes
    name, x7, _ = sets[2]
    sets[2] = (name, x7, [abs(x) ** 3 - 2 * x * x + 4 * x - 1 for x in x7])
    return sets


def sample(rnd, xs):
    """The points and their extrapolation flags."""
    lo, hi = xs[0], xs[-1]
    span = hi - lo
    points = [(x, False) for x in xs]
    points += [(x + k * EPS * max(abs(x), 1.0), False) for x in xs[1:-1] for k in (-3, 3)]
    points += [((a + b) / 2, False) for a, b in zip(xs, xs[1:])]
    points += [(rnd.uniform(lo, hi), False) for _ in range(5)]
    points += [(lo - 0.1 * span, True), (hi + 0.1 * span, True), (lo - span, False), (hi + span, False)]
    return [(x, flag) for x, flag in points if not (x == 0.0 and 0.0 in xs)]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for name, xs, ys in node_sets(rnd):
        interpolation = ql.LagrangeInterpolation(ql.Array(xs), ql.Array(ys))
        points = sample(rnd, xs)
        mid = (xs[0] + xs[1]) / 2
        out.append({"name": name,
                    "inputs": {"settings": st, "x": xs, "y": ys, "points": [[x, flag] for x, flag in points]},
                    "expected": {
                        "value": [v.attempt(lambda: interpolation(x, flag)) for x, flag in points],
                        "derivative": [v.attempt(lambda: interpolation.derivative(x, flag)) for x, flag in points],
                        "primitive": v.attempt(lambda: interpolation.primitive(mid)),
                        "secondDerivative": v.attempt(lambda: interpolation.secondDerivative(mid))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/lagrangeinterpolation.hpp", cases)
