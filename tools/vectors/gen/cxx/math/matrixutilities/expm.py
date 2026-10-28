# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Expm (ql/math/matrixutilities/expm.hpp, .cpp), which the wheel doesn't bind, each output computed by the
C++ harness: the matrix exponential by adaptive Runge-Kutta of QuantLib's test's 3 x 3 matrix, a random 4 x 4 matrix, a
symmetric one and the zero matrix, at times from 0.01 to 4 and negative, at tolerances 1e-12 and QL_EPSILON. Read back
by tests/NanoQL.Tests/Vectors/Cxx/Numerics/MatrixUtilities/ExpmCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

QL_EPSILON = 2.220446049250313e-16


def matrices():
    rng = random.Random(v.SEED)
    return [("QuantLib's test's", [[5.0, -6.0, -6.0], [-1.0, 4.0, 2.0], [3.0, -6.0, -4.0]]),
            ("random 4 x 4", [[rng.uniform(-1.0, 1.0) for _ in range(4)] for _ in range(4)]),
            ("symmetric", [[-1.0, 0.4, 0.1], [0.4, -2.0, 0.3], [0.1, 0.3, -0.5]]),
            ("zero", [[0.0] * 3 for _ in range(3)])]


def cases():
    out = []
    with cxx.connect() as H:
        for name, m in matrices():
            for t in (0.01, 0.5, 1.0, 4.0, -0.7):
                for tol in (1e-12, QL_EPSILON):
                    st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                    out.append({"name": f"{name}, t {t}, tolerance {tol!r}",
                                "inputs": {"settings": st, "matrix": v.enc(m), "t": t, "tolerance": tol},
                                "expected": {"expm": v.enc(H.call("Expm", m, t, tol))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/expm.hpp", cases)
