# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SymmetricSchurDecomposition (ql/math/matrixutilities/symmetricschurdecomposition.hpp, .cpp),
the symmetric threshold Jacobi algorithm: symmetric matrices of 1 to 8 rows (random, correlation matrices, positive
semi-definite and indefinite, diagonal, the identity and other repeated eigenvalues, whose eigenvectors the sort
orders lexicographically, a zero matrix, large and tiny elements), each with its eigenvalues and its eigenvectors
(the matrix's rows). Then the refusals of a matrix with no rows and of one that isn't square.

The wheel's SymmetricSchurDecomposition(Matrix) is the C++ class, eigenvalues() an Array and eigenvectors() a Matrix.
pseudosqrt.py's matrices are this file's. Read back by tests/NanoQL.Tests/Vectors/Numerics/MatrixUtilities/
SymmetricSchurDecompositionVectorTests.cs."""
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


def correlation(n, decay=0.1, shift=0.02):
    """matrices.cpp's createTestCorrelationMatrix."""
    return [[math.exp(-decay * abs(i - j) - (shift * (i + j) if i != j else 0.0)) for j in range(n)] for i in range(n)]


def random_symmetric(rnd, n, lo=-1.0, hi=1.0):
    m = [[0.0] * n for _ in range(n)]
    for i in range(n):
        for j in range(i, n):
            m[i][j] = m[j][i] = rnd.uniform(lo, hi)
    return m


def random_correlation(rnd, n):
    """A correlation matrix A·Aᵀ normalized to a unit diagonal, A random."""
    a = [[rnd.gauss(0.0, 1.0) for _ in range(n)] for _ in range(n)]
    c = [[sum(a[i][k] * a[j][k] for k in range(n)) for j in range(n)] for i in range(n)]
    return [[c[i][j] / math.sqrt(c[i][i] * c[j][j]) for j in range(n)] for i in range(n)]


def matrices(rnd):
    """(name, rows) of the symmetric matrices; shared with pseudosqrt.py."""
    out = [("1x1", [[2.5]]), ("1x1 negative", [[-0.3]])]
    for n in (2, 3, 4, 6, 8):
        out.append((f"correlation {n}", correlation(n)))
        out.append((f"random correlation {n}", random_correlation(rnd, n)))
        out.append((f"random symmetric {n}", random_symmetric(rnd, n)))
    out.append(("M1", [[1.0, 0.9, 0.7], [0.9, 1.0, 0.4], [0.7, 0.4, 1.0]]))
    out.append(("negative eigenvalue", [[1.0, 0.9, 0.7], [0.9, 1.0, -0.4], [0.7, -0.4, 1.0]]))
    out.append(("Higham's M5", [[2.0, -1.0, 0.0, 0.0], [-1.0, 2.0, -1.0, 0.0], [0.0, -1.0, 2.0, -1.0],
                                [0.0, 0.0, -1.0, 2.0]]))
    out.append(("singular", [[1.0, 1.0, 0.5], [1.0, 1.0, 0.5], [0.5, 0.5, 1.0]]))
    out.append(("covariance", [[0.04, 0.006, 0.0], [0.006, 0.09, -0.012], [0.0, -0.012, 0.0225]]))
    out.append(("diagonal", [[3.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 2.0]]))
    out.append(("identity", [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]))
    out.append(("repeated eigenvalue", [[2.0, 1.0, 1.0], [1.0, 2.0, 1.0], [1.0, 1.0, 2.0]]))
    out.append(("zero", [[0.0, 0.0], [0.0, 0.0]]))
    out.append(("large elements", [[1e150, 3e149], [3e149, 2e150]]))
    out.append(("tiny elements", [[1e-150, 3e-151], [3e-151, 2e-150]]))
    return out


def qlm(m):
    """The wheel's Matrix of a list of rows; Matrix() for none."""
    return ql.Matrix(m) if m else ql.Matrix()


def rows(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def cases():
    st = v.settings(ql.Date(22, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for name, m in matrices(rnd):
        d = ql.SymmetricSchurDecomposition(qlm(m))
        out.append({"name": name, "inputs": {"settings": st, "matrix": m},
                    "expected": {"eigenvalues": list(d.eigenvalues()), "eigenvectors": rows(d.eigenvectors())}})
    for name, m in (("no rows", []), ("not square", [[1.0, 0.5, 0.2], [0.5, 1.0, 0.1]])):
        out.append({"name": name, "inputs": {"settings": st, "matrix": m},
                    "expected": {"eigenvalues": v.attempt(
                        lambda m=m: list(ql.SymmetricSchurDecomposition(qlm(m)).eigenvalues()))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/symmetricschurdecomposition.hpp", cases)
