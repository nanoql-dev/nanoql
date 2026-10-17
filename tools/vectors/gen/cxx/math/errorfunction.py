# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for ErrorFunction::operator() (ql/math/errorfunction.hpp, FDLIBM's erf), which the wheel doesn't expose
(docs/porting-playbook.md §9.4), each output computed by the C++ harness: one case per branch of |x| (below
16·DBL_MIN, below 2^-28, below 0.84375, below 1.25, below 1/0.35, below 6, and from 6 on), each with points of both
signs spread over the branch and the doubles next to its bounds; and the special values, ±0, ±∞ and NaN. About 400
points. tests/NanoQL.Tests/Vectors/Cxx/Numerics/ErrorFunctionCxxVectorTests.cs reads it back."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

DBL_MIN = 2.2250738585072014e-308
# The branches of |x|, each [low, high) with its points: the bounds' neighbours and an even spread, geometric where
# the branch spans orders of magnitude.
BRANCHES = [("below 16 DBL_MIN", 5e-324, 16 * DBL_MIN, True),
            ("below 2^-28", 16 * DBL_MIN, 3.7252902984e-09, True),
            ("below 0.84375", 3.7252902984e-09, 0.84375, True),
            ("below 1.25", 0.84375, 1.25, False),
            ("below 1/0.35", 1.25, 2.85714285714285, False),
            ("below 6", 2.85714285714285, 6.0, False),
            ("from 6", 6.0, 1e300, True)]
SPREAD = 26


def spread(low, high, geometric):
    if geometric:
        return [math.exp(math.log(low) + (math.log(high) - math.log(low)) * i / SPREAD) for i in range(SPREAD)]
    return [low + (high - low) * i / SPREAD for i in range(SPREAD)]


def magnitudes(low, high, geometric):
    edges = [low, math.nextafter(low, math.inf), math.nextafter(high, 0.0)]
    return sorted(set(edges + spread(low, high, geometric)[1:]))


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def case(name, xs):
            return {"name": name, "inputs": {"settings": st, "x": v.enc(xs)},
                    "expected": {"erf": [v.attempt(lambda x=x: H.call("ErrorFunction::operator()", x)) for x in xs]}}

        out = []
        for name, low, high, geometric in BRANCHES:
            ms = magnitudes(low, high, geometric)
            out.append(case(name, ms + [-m for m in ms]))
        out.append(case("special values", [0.0, -0.0, math.inf, -math.inf, math.nan, 1e308, -1e308]))
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/errorfunction.hpp", cases)
