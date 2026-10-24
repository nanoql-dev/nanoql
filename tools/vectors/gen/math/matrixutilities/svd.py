# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SVD (ql/math/matrixutilities/svd.hpp, .cpp), the singular value decomposition: matrices with
as many or more rows than columns and with fewer (decomposed transposed), random, structured (QuantLib's test
matrices M1 to M4), rank-deficient, with zero rows and columns, and with large and tiny elements, each with its U,
V and S (the matrices' rows) and its singular values. A matrix with a NaN or infinite entry isn't written: QuantLib's
loop never ends on it, and nanoql refuses it (tests/NanoQL.Tests/Divergences/SvdNonFiniteRefusalTests.cs). The other
members, which the wheel doesn't expose, are vectored through the C++ harness
(tools/vectors/gen/cxx/math/matrixutilities/svd.py, which takes this file's matrices).

The wheel's SVD(Matrix) is the C++ class; U(), V() and S() are Matrices and singularValues() an Array. Read back by
tests/NanoQL.Tests/Vectors/Numerics/MatrixUtilities/SVDVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)


def matrices(rnd):
    """(name, rows) of the matrices; shared with the harness generator."""
    out = [
        ("M1", [[1.0, 0.9, 0.7], [0.9, 1.0, 0.4], [0.7, 0.4, 1.0]]),
        ("M2", [[1.0, 0.9, 0.7], [0.9, 1.0, 0.3], [0.7, 0.3, 1.0]]),
        ("M3, fewer rows", [[1.0, 2.0, 3.0, 4.0], [2.0, 0.0, 2.0, 1.0], [0.0, 1.0, 0.0, 0.0]]),
        ("M4", [[1.0, 2.0, 400.0], [2.0, 0.0, 1.0], [30.0, 2.0, 0.0], [2.0, 0.0, 1.05]]),
        ("1x1", [[-2.5]]),
        ("column", [[1.0], [-2.0], [0.5]]),
        ("row", [[1.0, -2.0, 0.5]]),
        ("rank-deficient", [[1.0, 2.0, 3.0], [2.0, 4.0, 6.0], [1.0, 0.0, 1.0], [0.5, 1.0, 1.5]]),
        ("zero column", [[1.0, 0.0, 2.0], [3.0, 0.0, -1.0], [0.5, 0.0, 0.25]]),
        ("zero matrix", [[0.0, 0.0], [0.0, 0.0], [0.0, 0.0]]),
        ("identity", [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]),
        ("scales", [[1e8, 1.0, 0.0], [1e-8, 2.0, 1e-12], [3.0, 1e6, 4.0], [0.0, 1.0, 1e-9]]),
    ]
    for m, n in [(3, 3), (4, 3), (5, 2), (6, 4), (8, 5), (2, 4), (3, 5), (4, 7), (7, 7)]:
        out.append((f"random {m}x{n}", [[rnd.uniform(-2.0, 2.0) for _ in range(n)] for _ in range(m)]))
    # the regression matrix of a Longstaff-Schwartz step: monomials to order 3 of scaled spots, and the payoff
    xs = [rnd.uniform(0.6, 1.0) for _ in range(12)]
    out.append(("regression", [[1.0, x, x * x, x * x * x, max(1.0 - x, 0.0)] for x in xs]))
    return out


def rows(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def cases():
    st = v.settings(R)
    rnd = random.Random(v.SEED)
    out = []
    for name, data in matrices(rnd):
        m = ql.Matrix(data)
        svd = ql.SVD(m)
        out.append({"name": name,
                    "inputs": {"settings": st, "matrix": data},
                    "expected": {"U": v.enc(rows(svd.U())),
                                 "V": v.enc(rows(svd.V())),
                                 "S": v.enc(rows(svd.S())),
                                 "singularValues": v.enc(list(svd.singularValues()))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/svd.hpp", cases)
