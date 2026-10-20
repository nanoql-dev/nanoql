# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the beta functions (ql/math/beta.hpp, .cpp), which the wheel doesn't expose, each output computed by
the C++ harness: betaFunction over a grid of positive arguments, and its refusal of a non-positive one through the
gamma function's logarithm; betaContinuedFraction and incompleteBetaFunction over a grid of shapes and points with the
header's default accuracy and iteration limit (1e-16 and 100), 0 and 1 among the points, which incompleteBetaFunction
answers exactly; both at explicit limits, among them too few iterations, which QuantLib refuses ("a or b too big, or
maxIteration too small in betacf"); and incompleteBetaFunction's refusals of a non-positive shape and of a point
outside [0, 1]. Read back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/BetaCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

ARGUMENTS = [0.1, 0.5, 1.0, 2.5, 7.0, 30.0]
SHAPES = [(0.5, 0.5), (1.0, 1.0), (2.0, 5.0), (5.0, 2.0), (10.0, 10.0), (0.2, 3.0), (40.0, 60.0)]
POINTS = [0.0, 1e-8, 0.05, 0.2, 0.4, 0.5, 0.6, 0.8, 0.95, 1.0 - 1e-8, 1.0]
# (a, b, x, accuracy, maxIteration)
EXPLICIT = [(2.0, 5.0, 0.3, 1e-8, 100), (2.0, 5.0, 0.3, 1e-16, 3), (40.0, 60.0, 0.4, 1e-16, 5),
            (40.0, 60.0, 0.4, 1e-12, 200), (500.0, 500.0, 0.5, 1e-16, 100), (0.5, 0.5, 0.9, 1e-14, 1)]
REFUSED = [(0.0, 1.0, 0.5), (-1.0, 1.0, 0.5), (1.0, 0.0, 0.5), (1.0, -2.0, 0.5), (2.0, 3.0, -0.1),
           (2.0, 3.0, 1.1)]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def call(method, *args):
            return v.attempt(lambda: H.call(method, *args))

        out = []
        pairs = [(z, w) for z in ARGUMENTS for w in ARGUMENTS] + [(0.0, 1.0), (-1.0, 2.0), (1.0, -0.5)]
        out.append({"name": "betaFunction",
                    "inputs": {"settings": st, "z": [z for z, _ in pairs], "w": [w for _, w in pairs]},
                    "expected": {"betaFunction": [call("betaFunction", z, w) for z, w in pairs]}})
        for a, b in SHAPES:
            # the continued fraction converges below (a + 1)/(a + b + 2), where incompleteBetaFunction calls it
            out.append({"name": f"a {a!r}, b {b!r}",
                        "inputs": {"settings": st, "a": [a] * len(POINTS), "b": [b] * len(POINTS), "x": POINTS,
                                   "accuracy": None, "maxIteration": None},
                        "expected": {
                            "betaContinuedFraction": [call("betaContinuedFraction", a, b, x, 1e-16, 100)
                                                      for x in POINTS],
                            "incompleteBetaFunction": [call("incompleteBetaFunction", a, b, x, 1e-16, 100)
                                                       for x in POINTS]}})
        out.append({"name": "explicit accuracy and iterations",
                    "inputs": {"settings": st, "a": [r[0] for r in EXPLICIT], "b": [r[1] for r in EXPLICIT],
                               "x": [r[2] for r in EXPLICIT], "accuracy": [r[3] for r in EXPLICIT],
                               "maxIteration": [r[4] for r in EXPLICIT]},
                    "expected": {"betaContinuedFraction": [call("betaContinuedFraction", *r) for r in EXPLICIT],
                                 "incompleteBetaFunction": [call("incompleteBetaFunction", *r) for r in EXPLICIT]}})
        out.append({"name": "refused arguments",
                    "inputs": {"settings": st, "a": [r[0] for r in REFUSED], "b": [r[1] for r in REFUSED],
                               "x": [r[2] for r in REFUSED], "accuracy": None, "maxIteration": None},
                    "expected": {"incompleteBetaFunction": [call("incompleteBetaFunction", *r, 1e-16, 100)
                                                            for r in REFUSED]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/beta.hpp", cases)
