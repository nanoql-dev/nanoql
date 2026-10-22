# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CholeskyDecomposition and CholeskySolveFor (ql/math/matrixutilities/
choleskydecomposition.hpp, .cpp): symmetricschurdecomposition.py's matrices, each decomposed strictly and flexibly
(a non-positive pivot refused, or read as 0), with the solve of L·Lᵀ·x = b for a random b on each flexible
decomposition; then the refusal of a matrix that isn't square, the matrix with no rows, and the solve's refusal of
a right-hand side of another size.

The wheel's CholeskyDecomposition(Matrix, flexible) and CholeskySolveFor(Matrix, Array) are the C++ functions. Read
back by tests/NanoQL.Tests/Vectors/Numerics/MatrixUtilities/CholeskyDecompositionVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import QuantLib as ql  # noqa: E402

import symmetricschurdecomposition as ssd  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    st = v.settings(ql.Date(22, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for name, m in ssd.matrices(rnd) + [("no rows", [])]:
        n = len(m)
        b = [rnd.uniform(-1.0, 1.0) for _ in range(n)]
        out.append({"name": name, "inputs": {"settings": st, "matrix": m, "b": b},
                    "expected": {
                        "strict": v.attempt(lambda m=m: ssd.rows(ql.CholeskyDecomposition(ssd.qlm(m), False))),
                        "flexible": v.attempt(lambda m=m: ssd.rows(ql.CholeskyDecomposition(ssd.qlm(m), True))),
                        "solve": v.attempt(lambda m=m, b=b: list(ql.CholeskySolveFor(
                            ql.CholeskyDecomposition(ssd.qlm(m), True), ql.Array(b))))}})
    m = [[1.0, 0.5, 0.2], [0.5, 1.0, 0.1]]
    out.append({"name": "not square", "inputs": {"settings": st, "matrix": m, "b": [1.0, 2.0]},
                "expected": {"flexible": v.attempt(lambda: ssd.rows(ql.CholeskyDecomposition(ssd.qlm(m), True)))}})
    m = [[1.0, 0.5], [0.5, 1.0]]
    out.append({"name": "right-hand side of another size", "inputs": {"settings": st, "matrix": m, "b": [1.0, 2.0, 3.0]},
                "expected": {"solve": v.attempt(lambda: list(ql.CholeskySolveFor(
                    ql.CholeskyDecomposition(ssd.qlm(m), True), ql.Array([1.0, 2.0, 3.0]))))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/choleskydecomposition.hpp", cases)
