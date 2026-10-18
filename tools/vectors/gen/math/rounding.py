# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Rounding (ql/math/rounding.hpp): the five kinds (UpRounding, DownRounding, ClosestRounding,
FloorTruncation, CeilingTruncation) at every precision QuantLib documents, 0 to 16, with rounding digits 1, 5 and 9,
and the default Rounding(), which doesn't round; each applied to the 30 values below: both zeros, both infinities and
NaN, which QuantLib's std::modf passes through; ties and near-ties at several precisions; the cases of QuantLib's own
test; and magnitudes up to 1e300, whose scaled values overflow to infinity at high precisions.

Each case's inputs are "kind" (the class's name, or "Rounding" for the default), "precision" and "digit" (null for
the default), and "values". Read back by tests/NanoQL.Tests/Vectors/Numerics/RoundingVectorTests.cs.

The wheel's classes are QuantLib-SWIG's proxies (QuantLib-SWIG 1.44, SWIG/rounding.i): Rounding() is C++
QuantLib::Rounding's default constructor; each kind is the C++ class of its name, whose constructor (precision,
digit) passes its type to Rounding's; __call__ is Rounding::operator()."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

KINDS = {"UpRounding": ql.UpRounding, "DownRounding": ql.DownRounding, "ClosestRounding": ql.ClosestRounding,
         "FloorTruncation": ql.FloorTruncation, "CeilingTruncation": ql.CeilingTruncation}
PRECISIONS = range(17)
DIGITS = [1, 5, 9]
VALUES = [
    0.0, -0.0, math.inf, -math.inf, math.nan,
    0.5, -0.5, 2.5, 0.125, -0.125, 0.15, 1.005, -2.675, 9.95, 0.45,
    0.86313513, -7.64555346, 0.13961605, -1.0622867, 6.3454247,
    7.0, 123456.789, -98765.4321, 1e-17, -3e-9, 0.9999999999999999,
    4503599627370495.5, 1e20, -1e100, 1e300,
]
assert len(VALUES) == 30


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = [{"name": "Rounding()",
            "inputs": {"settings": st, "kind": "Rounding", "precision": None, "digit": None, "values": v.enc(VALUES)},
            "expected": {"rounded": [v.enc(ql.Rounding()(x)) for x in VALUES]}}]
    for kind, cls in KINDS.items():
        for p in PRECISIONS:
            for d in DIGITS:
                r = cls(p, d)
                out.append({"name": f"{kind}({p}, {d})",
                            "inputs": {"settings": st, "kind": kind, "precision": p, "digit": d,
                                       "values": v.enc(VALUES)},
                            "expected": {"rounded": [v.enc(r(x)) for x in VALUES]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/rounding.hpp", cases)
