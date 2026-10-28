# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for SparseILUPreconditioner (ql/math/matrixutilities/sparseilupreconditioner.hpp, .cpp), which the wheel
doesn't bind, each output computed by the C++ harness: the incomplete LU factors at fill levels 0 to 3 and the
preconditioner applied to random vectors, on banded and random sparse matrices whose entries are random reals, so
nothing cancels; and on fdmlinearop.cpp's test matrix. Matrices whose eliminations cancel an entry to 0, where nanoql
keeps each fill entry's own level (the S-core plan's CR7 (xxiv); tests/NanoQL.Tests/Divergences/SparseIluTests.cs), are
left out. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/MatrixUtilities/SparseILUPreconditionerCxxVectorTests.cs."""
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

    def banded(n, bands):
        return [[rng.uniform(4.0, 6.0) if i == j else (rng.uniform(-1.0, 1.0) if abs(i - j) in bands else 0.0)
                 for j in range(n)] for i in range(n)]

    def sparse(n, density):
        return [[rng.uniform(4.0, 6.0) if i == j else (rng.uniform(-1.0, 1.0) if rng.random() < density else 0.0)
                 for j in range(n)] for i in range(n)]

    def test_matrix(n, m, theta):
        a = [[0.0] * (n * m) for _ in range(n * m)]
        for i in range(n):
            for j in range(m):
                k = i * m + j
                a[k][k] = 1.0
                if 0 < i < n - 1 and 0 < j < m - 1:
                    delta = theta / ((2) * (2))
                    a[k][(i - 1) * m + j - 1] = delta
                    a[k][(i - 1) * m + j + 1] = -delta
                    a[k][(i + 1) * m + j - 1] = -delta
                    a[k][(i + 1) * m + j + 1] = delta
        return a

    return [("tridiagonal 8", banded(8, {1})), ("bands 1 and 3, 10", banded(10, {1, 3})),
            ("random 8, density 0.35", sparse(8, 0.35)), ("random 12, density 0.25", sparse(12, 0.25)),
            ("fdmlinearop's test matrix, 5 x 4", test_matrix(5, 4, 1.0))]


def cases():
    rng = random.Random(v.SEED + 1)
    out = []
    with cxx.connect() as H:
        for name, m in matrices():
            bs = [[rng.uniform(-1.0, 1.0) for _ in m] for _ in range(2)]
            for lfil in range(4):
                st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                r = H.call("SparseILUPreconditioner", m, lfil, bs)
                out.append({"name": f"{name}, fill level {lfil}",
                            "inputs": {"settings": st, "matrix": v.enc(m), "lfil": lfil, "b": v.enc(bs)},
                            "expected": {"l": v.enc(r["l"]), "u": v.enc(r["u"]), "apply": v.enc(r["apply"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/sparseilupreconditioner.hpp", cases)
