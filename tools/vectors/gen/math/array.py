# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Array (ql/math/array.hpp), nanoql's Vector, through the members the wheel exposes: 24 pairs
x, y of the same size, 1 to 8 elements (random, and with NaN, the infinities, signed zeros, and tiny and huge
magnitudes), each with a scalar s and an array z of another size. For each: the element-by-element x + y, x - y,
x * y and x / y; x + s, x - s, x * s and x / s; -x; the dot product; x == y, x != y, x against a copy of itself and
against z; the size; the text; Array(size, s); and the refusals of the element-by-element operations and of the dot
product with z. Each case's inputs hold x, y, z and s, the non-finite ones encoded as strings.

The wheel's calls, by QuantLib-SWIG's linearalgebra.i: ql.Array(list) builds an Array of the list's size and sets
its elements (SWIG's typemap); ql.Array(n, fill) is Array(Size, Real); x + y, x - y, x * y and x / y
(__add__, __sub__, __mul__, __truediv__) call operator+, operator-, operator* and operator/(const Array&,
const Array&), and with a Real operand the (const Array&, Real) overloads; -x calls operator-(const Array&); x @ y
(__matmul__) calls DotProduct; == and != call Array's operator== and operator!=; len(x) calls size(); str(x) streams
the Array with operator<<. SWIG's __rmul__ computes x * s, not s * x, so the scalar-first operators are the C++
harness's (tools/vectors/gen/cxx/math/array.py). Read back by tests/NanoQL.Tests/Vectors/Numerics/ArrayVectorTests.cs."""
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

NAN, INF = math.nan, math.inf
# Pairs with special values: (x, y, s).
SPECIAL = [
    ([NAN, 1.0], [1.0, NAN], 2.0),
    ([INF, -INF, 1.0], [INF, INF, 0.0], 0.0),
    ([0.0, -0.0, 3.0, -2.5], [-0.0, 0.0, 0.0, -0.0], -0.0),
    ([1e-300, 1e300, -1e-310, 5e-324], [1e300, 1e-300, 1e10, 2.0], 1e300),
    ([1.0, 2.0, 3.0], [1.0, 2.0, 3.0], 1.0),
    ([0.1, 0.2, 0.30000000000000004, 123456.5, 1234565.0], [0.3, 0.1, 0.2, 0.5, -1e-7], INF),
]


def as_list(a):
    return [a[i] for i in range(len(a))]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    rnd = random.Random(v.SEED)
    pairs = list(SPECIAL)
    while len(pairs) < 24:
        n = 1 + len(pairs) % 8
        x = [rnd.uniform(-10.0, 10.0) for _ in range(n)]
        y = [rnd.uniform(-10.0, 10.0) for _ in range(n)]
        pairs.append((x, y, rnd.uniform(-5.0, 5.0)))
    out = []
    for k, (x, y, s) in enumerate(pairs):
        z = [1.5] * (len(x) + 1 if k % 2 == 0 else len(x) - 1 if len(x) > 1 else 3)
        a, b, c = ql.Array(x), ql.Array(y), ql.Array(z)
        out.append({"name": f"{k} size {len(x)}",
                    "inputs": {"settings": st, "x": v.enc(x), "y": v.enc(y), "z": v.enc(z), "s": v.enc(s)},
                    "expected": {
                        "sum": v.enc(as_list(a + b)),
                        "difference": v.enc(as_list(a - b)),
                        "product": v.enc(as_list(a * b)),
                        "quotient": v.enc(as_list(a / b)),
                        "sumScalar": v.enc(as_list(a + s)),
                        "differenceScalar": v.enc(as_list(a - s)),
                        "productScalar": v.enc(as_list(a * s)),
                        "quotientScalar": v.enc(as_list(a / s)),
                        "negative": v.enc(as_list(-a)),
                        "dotProduct": v.enc(a @ b),
                        "equal": a == b,
                        "notEqual": a != b,
                        "equalCopy": a == ql.Array(x),
                        "equalOtherSize": a == c,
                        "size": len(a),
                        "text": str(a),
                        "filled": v.enc(as_list(ql.Array(len(x), s))),
                        "sumOtherSize": v.attempt(lambda: as_list(a + c)),
                        "differenceOtherSize": v.attempt(lambda: as_list(a - c)),
                        "productOtherSize": v.attempt(lambda: as_list(a * c)),
                        "quotientOtherSize": v.attempt(lambda: as_list(a / c)),
                        "dotProductOtherSize": v.attempt(lambda: a @ c)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/array.hpp", cases)
