# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PolarStudentTRng (ql/experimental/math/polarstudenttrng.hpp), which the wheel doesn't expose,
each output computed by the C++ harness (tools/cxx/src/experimental/math/polarstudenttrng.cpp) on QuantLib's Mersenne
Twister: fifty draws at degrees of freedom from 0.5 to 1e3 and three seeds (QuantLib's default 0, the clock, is refused
by nanoql, so no case takes it); and the degrees of freedom refused (0 and negative). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Math/PolarStudentTRngCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 38960
DEGREES = [0.5, 1.0, 3.0, 5.0, 7.5, 45.0, 1e3]
SEEDS = [1, 42, 4294967295]


def cases():
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        st = H.settings(R)
        return [{"name": "draws",
                 "inputs": {"settings": st, "degrees": DEGREES, "seeds": SEEDS, "count": 50, "refused": [0.0, -2.0]},
                 "expected": {"draws": [call("PolarStudentTRng::draws", df, s, 50) for df in DEGREES for s in SEEDS],
                              "refused": [call("PolarStudentTRng::draws", df, 42, 1) for df in (0.0, -2.0)]}}]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/polarstudenttrng.hpp", cases)
