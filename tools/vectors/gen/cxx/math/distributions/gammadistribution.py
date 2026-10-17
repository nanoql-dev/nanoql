# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for GammaFunction::value (ql/math/distributions/gammadistribution.hpp), which the wheel doesn't expose
(docs/porting-playbook.md §9.4), each output computed by the C++ harness, at about 200 points over its three branches:
exp(logValue(x)) from 1 up, to its overflow past 171.6; the recursion Γ(x + 1)/x on (-20, 1), at 0, at the negative
integers and between them; and the reflection formula at -20 and below, with test-suite/functions.cpp's testGammaValues
points among them. NaN isn't among them: its recursion has no end, in QuantLib as in nanoql. The wheel's
GammaFunction::logValue is in tools/vectors/gen/math/distributions/gammadistribution.py.
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Distributions/GammaDistributionCxxVectorTests.cs reads it back."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TEST_SUITE = [0.0001, 1.2, 7.3, -1.1, -4.001, -4.999, -19.000001, -19.5, -21.000001, -21.5]
# From 1 up: 50 points to 180, past the overflow; the integers 1 to 10; and the edges.
ABOVE = [1.0 + 179.0 * i / 49 for i in range(50)] + [float(n) for n in range(2, 11)] + [171.5, 171.6, 171.7, 1e10,
                                                                                         math.inf]
# (-20, 1): 70 points, 0 and its neighbours, the negative integers to -19.
BETWEEN = ([-19.9 + 20.8 * i / 69 for i in range(70)] + [0.0, -0.0, 1e-300, -1e-300, 0.5, 0.9999999999999999]
           + [-float(n) for n in range(1, 20)])
# -20 and below: 30 points to -170, the integers -20 to -23, -inf and the edges.
BELOW = [-20.0 - 150.0 * i / 29 for i in range(30)] + [-20.0, -21.0, -22.0, -23.0, -20.000000000000004, -180.5,
                                                       -math.inf]


def points():
    out = TEST_SUITE + ABOVE + BETWEEN + BELOW
    return out


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        xs = points()
        return [{"name": "value", "inputs": {"settings": st, "x": v.enc(xs)},
                 "expected": {"value": [v.attempt(lambda x=x: H.call("GammaFunction::value", x)) for x in xs]}}]


if __name__ == "__main__":
    v.main(__file__, "ql/math/distributions/gammadistribution.hpp", cases)
