# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the QR decomposition and solve (ql/math/matrixutilities/qrdecomposition.hpp, .cpp), which the wheel
doesn't bind, each output computed by the C++ harness: square, tall and wide matrices from a fixed random sample and
QuantLib's test's matrices, decomposed with and without pivoting (Q, R and the permutation), and solved for random
right-hand sides with and without pivoting, with and without a diagonal d. QuantLib's rank-deficient solve, MINPACK's
documented contract, is among them. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/MatrixUtilities/QRDecompositionCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def matrices():
    rng = random.Random(v.SEED)

    def rand(r, c):
        return [[rng.uniform(-1.0, 1.0) for _ in range(c)] for _ in range(r)]

    m1 = [[1.0, 0.9, 0.7], [0.9, 1.0, 0.4], [0.7, 0.4, 1.0]]
    m3 = [[1, 2, 3, 4], [2, 0, 2, 1], [0, 1, 0, 0]]
    m4 = [[1, 2, 400], [2, 0, 1], [30, 2, 0], [2, 0, 1.05]]
    deficient = [[1.0, 2.0, 3.0], [2.0, 4.0, 6.0], [1.0, 0.0, 1.0], [0.0, 1.0, 1.0]]
    return [("M1", m1), ("M3, wide", [[float(x) for x in r] for r in m3]), ("M4, tall", m4),
            ("random 5 x 5", rand(5, 5)), ("random 8 x 3", rand(8, 3)), ("random 3 x 7", rand(3, 7)),
            ("rank deficient 4 x 3", deficient)]


def cases():
    out = []
    rng = random.Random(v.SEED + 1)
    with cxx.connect() as H:
        for name, m in matrices():
            for pivot in (True, False):
                st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                r = H.call("qrDecomposition", m, pivot)
                bs = [[rng.uniform(-1.0, 1.0) for _ in m] for _ in range(2)]
                d = [rng.uniform(0.0, 1.0) for _ in m[0]]
                solves = [{"b": v.enc(b), "d": v.enc(dd)} for b in bs for dd in ([], d)]
                out.append({"name": f"{name}, {'pivoting' if pivot else 'no pivoting'}",
                            "inputs": {"settings": st, "matrix": v.enc(m), "pivot": pivot, "solves": solves},
                            "expected": {"q": v.enc(r["q"]), "r": v.enc(r["r"]), "ipvt": r["ipvt"],
                                         "x": [v.enc(H.call("qrSolve", m, s["b"], pivot, s["d"])) for s in solves]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/qrdecomposition.hpp", cases)
