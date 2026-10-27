# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's quadratic (ql/math/quadratic.hpp, .cpp), which the wheel doesn't expose, each output computed
by the C++ harness: the turning point, its value, the value at a point, the discriminant and the roots of quadratics
with two roots, a double root and none (the turning point twice), leading coefficients of either sign, and seeded ones.

The harness unit is tools/cxx/src/math/quadratic.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Math/QuadraticCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310


def cases():
    rng = random.Random(20261027)
    fixed = [("two roots", 1.0, -3.0, 2.0, 0.5), ("a double root", 1.0, -2.0, 1.0, 3.0), ("no root", 1.0, 0.0, 1.0, -1.0),
             ("negative leading, two roots", -2.0, 1.0, 3.0, 0.25), ("negative leading, no root", -1.0, 0.5, -2.0, 2.0),
             ("tiny leading", 1e-12, 1.0, -1.0, 7.0), ("a caplet calibration's", 0.0201, 0.0784, -0.0045, -2.2)]
    for i in range(12):
        fixed.append((f"seeded {i}", rng.uniform(-2.0, 2.0), rng.uniform(-3.0, 3.0), rng.uniform(-2.0, 2.0),
                      rng.uniform(-5.0, 5.0)))
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for name, a, b, c, x in fixed:
            out.append({"name": name, "inputs": {"settings": st, "a": a, "b": b, "c": c, "x": x},
                        "expected": v.enc(H.call("quadratic", a, b, c, x))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/quadratic.hpp", cases)
