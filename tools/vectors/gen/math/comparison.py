# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's floating-point comparisons (ql/math/comparison.hpp): close and close_enough, each with the
default 42 epsilons and with n = 1, 42 and 100, for each of 12 numbers x against 9 others: x itself, and x moved by
relative steps from 1e-16 to 1e-6 (zero moved by absolute steps from 1e-300 to 1, the infinities against each other,
a large finite number and zero). Each case's inputs hold x, the others and the n values, the non-finite ones encoded
as strings. The wheel's close and close_enough call the wrapped C++ functions. Read back by
tests/NanoQL.Tests/Vectors/Numerics/ComparisonVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

INF = float("inf")
NUMBERS = [1.0, -1.0, 0.1, 123.456, -7.5e-5, 1e-300, 1e300, 2.2250738585072014e-308, 0.0, INF, -INF, 1e-10]
RELATIVE_STEPS = [1e-16, 1e-15, 5e-15, 9.3e-15, 9.4e-15, 1e-14, 1e-13, 1e-6]
ZERO_OTHERS = [1e-300, 1e-200, 1e-29, 1e-28, 1e-27, 1e-20, 1e-10, 1.0]
INFINITE_OTHERS = [INF, -INF, 1e300, 0.0, 1.0, -1e300, 1e-300, 42.0]
NS = [1, 42, 100]


def others(x):
    if x == 0.0:
        return [x] + ZERO_OTHERS
    if x in (INF, -INF):
        return [x] + INFINITE_OTHERS
    return [x] + [x * (1.0 + s) for s in RELATIVE_STEPS]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for i, x in enumerate(NUMBERS):
        ys = others(x)
        out.append({"name": f"{i} {x!r}",
                    "inputs": {"settings": st, "x": v.enc(x), "others": v.enc(ys), "n": NS},
                    "expected": {"close": [ql.close(x, y) for y in ys],
                                 "closeN": [[ql.close(x, y, n) for n in NS] for y in ys],
                                 "closeEnough": [ql.close_enough(x, y) for y in ys],
                                 "closeEnoughN": [[ql.close_enough(x, y, n) for n in NS] for y in ys]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/comparison.hpp", cases)
