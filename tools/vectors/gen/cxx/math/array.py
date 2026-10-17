# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members and functions of QuantLib's Array (ql/math/array.hpp), nanoql's Vector, that the wheel
doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4): Array(Size) and
Array(Size, Real, Real); Norm2, Abs, Sqrt, Log, Exp and Pow; resize, front, back, at (its refusal past the end
included) and empty; the compound assignments +=, -=, *= and /= with an Array and with a Real, and their refusals of
an Array of another size; the scalar-first operators s + x, s - x, s * x and s / x; the unary +; and swap. Ten arrays
x of 1 to 8 elements (random, and with NaN, the infinities, signed zeros, negatives, and tiny and huge magnitudes),
each with a partner y of its size, an array z of another size, a scalar s and an exponent p, and an empty array for
the members an empty array allows. tests/NanoQL.Tests/Vectors/Cxx/Numerics/ArrayCxxVectorTests.cs reads it back."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

NAN, INF = math.nan, math.inf
# (x, y, s, p) with special values.
SPECIAL = [
    ([NAN, 1.0, -4.0], [2.0, NAN, 0.5], 2.0, 0.5),
    ([INF, -INF, 0.0, -0.0], [1.0, INF, -0.0, 0.0], -0.0, -2.3),
    ([1e-300, 1e300, -1e-310, 5e-324, 700.0, -745.5], [1e300, 2.0, 1e10, 3.0, 2.0, 1.0], 1e300, 3.0),
    ([0.25, 2.0, 9.0, 100.0], [4.0, 0.5, 3.0, 0.01], INF, 0.0),
]
CONSTRUCTED = [(0, 1.0, 1.0), (1, -2.5, 3.0), (5, 42.0, 3.0), (10, 1.0, 1.0), (8, 0.1, 0.1), (4, 1e300, 1e300),
               (3, 0.0, -0.0), (6, -1.0, 0.25)]
SIZES = [0, 1, 2, 5, 8]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = [{"name": "constructors",
                "inputs": {"settings": st, "sizes": SIZES, "constructed": [list(c) for c in CONSTRUCTED]},
                "expected": {"sizeConstructor": [H.call("Array(Size)", n) for n in SIZES],
                             "valueIncrementConstructor": [call("Array(Size,Real,Real)", *c) for c in CONSTRUCTED]}}]

        empty = []
        out.append({"name": "empty",
                    "inputs": {"settings": st, "x": empty, "resizes": [0, 3]},
                    "expected": {"norm2": call("Norm2", empty),
                                 "empty": H.call("Array::empty", empty),
                                 "at": [call("Array::at", empty, 0)],
                                 "resizeSize": [H.call("Array::resize", empty, n)["size"] for n in (0, 3)],
                                 "resizeKept": [v.enc(H.call("Array::resize", empty, n)["kept"]) for n in (0, 3)]}})

        rnd = random.Random(v.SEED)
        arrays = list(SPECIAL)
        while len(arrays) < 10:
            n = 1 + (len(arrays) * 3) % 8
            arrays.append(([rnd.uniform(-10.0, 10.0) for _ in range(n)], [rnd.uniform(-10.0, 10.0) for _ in range(n)],
                           rnd.uniform(-5.0, 5.0), rnd.uniform(-3.0, 3.0)))
        for k, (x, y, s, p) in enumerate(arrays):
            n = len(x)
            z = [2.0] * (n + 2)
            resizes = [0, max(n - 1, 0), n, n + 3]
            indices = list(range(n + 2))
            out.append({"name": f"{k} size {n}",
                        "inputs": {"settings": st, "x": v.enc(x), "y": v.enc(y), "z": v.enc(z), "s": v.enc(s),
                                   "p": v.enc(p), "resizes": resizes, "indices": indices},
                        "expected": {
                            "norm2": call("Norm2", x),
                            "abs": call("Abs", x),
                            "sqrt": call("Sqrt", x),
                            "log": call("Log", x),
                            "exp": call("Exp", x),
                            "pow": call("Pow", x, p),
                            "front": call("Array::front", x),
                            "back": call("Array::back", x),
                            "empty": H.call("Array::empty", x),
                            "at": [call("Array::at", x, i) for i in indices],
                            "resizeSize": [H.call("Array::resize", x, m)["size"] for m in resizes],
                            "resizeKept": [v.enc(H.call("Array::resize", x, m)["kept"]) for m in resizes],
                            "addAssign": call("Array::operator+=(const Array&)", x, y),
                            "addAssignScalar": call("Array::operator+=(Real)", x, s),
                            "subtractAssign": call("Array::operator-=(const Array&)", x, y),
                            "subtractAssignScalar": call("Array::operator-=(Real)", x, s),
                            "multiplyAssign": call("Array::operator*=(const Array&)", x, y),
                            "multiplyAssignScalar": call("Array::operator*=(Real)", x, s),
                            "divideAssign": call("Array::operator/=(const Array&)", x, y),
                            "divideAssignScalar": call("Array::operator/=(Real)", x, s),
                            "addAssignOtherSize": call("Array::operator+=(const Array&)", x, z),
                            "subtractAssignOtherSize": call("Array::operator-=(const Array&)", x, z),
                            "multiplyAssignOtherSize": call("Array::operator*=(const Array&)", x, z),
                            "divideAssignOtherSize": call("Array::operator/=(const Array&)", x, z),
                            "scalarSum": call("operator+(Real,const Array&)", s, x),
                            "scalarDifference": call("operator-(Real,const Array&)", s, x),
                            "scalarProduct": call("operator*(Real,const Array&)", s, x),
                            "scalarQuotient": call("operator/(Real,const Array&)", s, x),
                            "positive": call("operator+(const Array&)", x),
                            "swap": call("Array::swap", x, z)}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/array.hpp", cases)
