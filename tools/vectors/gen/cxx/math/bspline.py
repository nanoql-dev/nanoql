# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BSpline (ql/math/bspline.hpp, .cpp), which the wheel doesn't expose (docs/porting-playbook.md
§9.4), each output computed by the C++ harness: the basis functions N_{i,p}(x) of linear, quadratic and cubic
B-splines on uniform and uneven knots, testBSplines' knots among them, at 41 points across and past the knots, every
index; and the constructions and indices QuantLib refuses (p = 0, n = 0, p > n, a wrong number of knots, decreasing
knots, an index past n).

Left out: knots with a repeat, where QuantLib's recursion divides by a zero span and gives NaN, and nanoql reads the
term as 0 (tests/NanoQL.Tests/Divergences/BSplineRepeatedKnotTests.cs).

The harness's call is tools/cxx/src/math/bspline.cpp's. Each case's inputs hold its settings, "p", "n", "knots",
"indices" and "x". Read back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/BSplineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

SPLINES = [
    ("linear, uniform", 1, 4, [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0]),
    ("quadratic, testBSplines' knots", 2, 3, [-1.0, 0.5, 0.75, 1.2, 3.0, 4.0, 5.0]),
    ("cubic, uniform", 3, 5, [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0]),
    ("cubic, the fitting method's knots", 3, 5, [-30.0, -20.0, 0.0, 5.0, 10.0, 15.0, 20.0, 25.0, 30.0, 40.0]),
    ("cubic, uneven", 3, 7, [-20.0, -10.0, -5.0, 0.0, 2.0, 5.0, 8.0, 12.0, 18.0, 25.0, 35.0, 45.0]),
]
REFUSALS = [
    ("p = 0", 0, 2, [0.0, 1.0, 2.0, 3.0], [0]),
    ("n = 0", 1, 0, [0.0, 1.0, 2.0], [0]),
    ("p > n", 3, 2, [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0], [0]),
    ("wrong number of knots", 2, 3, [0.0, 1.0, 2.0, 3.0, 4.0, 5.0], [0]),
    ("decreasing knots", 1, 2, [0.0, 2.0, 1.0, 3.0, 4.0], [0]),
    ("index past n", 1, 2, [0.0, 1.0, 2.0, 3.0, 4.0], [3]),
]


def points(knots):
    lo, hi = knots[0], knots[-1]
    span = hi - lo
    return [lo - 0.1 * span + k * 1.2 * span / 40 for k in range(41)]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 16, 6, 2025))
        for name, p, n, knots in SPLINES:
            xs = points(knots) + knots
            indices = list(range(n + 1))
            r = H.call("BSpline::values", p, n, knots, indices, xs)
            out.append({"name": name, "inputs": {"settings": st, "p": p, "n": n, "knots": knots, "indices": indices,
                                                 "x": xs},
                        "expected": {"values": r["values"]}})
        for name, p, n, knots, indices in REFUSALS:
            out.append({"name": "refusal: " + name,
                        "inputs": {"settings": st, "p": p, "n": n, "knots": knots, "indices": indices, "x": [0.5]},
                        "expected": {"values": v.attempt(lambda p=p, n=n, k=knots, i=indices:
                                                         H.call("BSpline::values", p, n, k, i, [0.5])["values"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/bspline.hpp", cases)
