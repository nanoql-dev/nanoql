# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Matrix (ql/math/matrix.hpp) through the members and functions the wheel exposes. 20 cases,
each a matrix a of 1 to 6 rows and columns with a partner b of its size, a matrix c whose rows are a's columns, a
square matrix q, vectors v and w of a's rows and columns, and a scalar s: a + b, a - b, a c, a s and s a, the
transpose, the outer product of v and w, the inverse of q, v a and a w, the rows and columns, the text, the matrix
Matrix(rows, columns, s) fills, and the refusals of a sum, a difference and products with operands of the wrong size.
Then 40 square matrices of 1 to 6 rows through inverse: random, small integers (ties, zeros), near-singular, and
singular, whose refusal is recorded. Each case's inputs hold its matrices as lists of rows.

The wheel's calls, by QuantLib-SWIG's linearalgebra.i: ql.Matrix(rows) builds Matrix(rows, columns) and sets each
element (SWIG's typemap); ql.Matrix(r, c, fill) is Matrix(Size, Size, Real); a + b and a - b (__add__, __sub__) call
operator+ and operator-(const Matrix&, const Matrix&); a * s calls operator*(const Matrix&, Real), s * a (__rmul__)
operator*(Real, const Matrix&), a * c operator*(const Matrix&, const Matrix&), a * w operator*(const Matrix&,
const Array&), and v * a (Array's __mul__) operator*(const Array&, const Matrix&); ql.transpose, ql.outerProduct and
ql.inverse call transpose, outerProduct(const Array&, const Array&) and inverse; rows() and columns() the members;
str(a) streams the Matrix with operator<<. inverse runs Boost uBLAS's LU factorization. The wheel's Matrix has no
operator/ in Python 3 (SWIG names it __div__): the quotient is the C++ harness's (tools/vectors/gen/cxx/math/matrix.py).
Read back by tests/NanoQL.Tests/Vectors/Numerics/MatrixVectorTests.cs, which also prints how many outputs are
bit-identical to QuantLib's."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def rows_of(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def as_list(a):
    return [a[i] for i in range(len(a))]


def random_matrix(rnd, r, c, lo=-5.0, hi=5.0):
    return [[rnd.uniform(lo, hi) for _ in range(c)] for _ in range(r)]


def inverse_matrices(rnd):
    """40 square matrices of 1 to 6 rows: random, small integers, near-singular, singular."""
    out = []
    for k in range(12):
        n = 1 + k % 6
        out.append(("random", random_matrix(rnd, n, n, -1.0, 1.0)))
    for k in range(12):
        n = 2 + k % 5
        out.append(("small integers", [[float(rnd.randint(-3, 3)) for _ in range(n)] for _ in range(n)]))
    for k in range(8):
        n = 2 + k % 5
        m = random_matrix(rnd, n, n, -1.0, 1.0)
        # the last row nearly the sum of the others
        m[-1] = [sum(m[i][j] for i in range(n - 1)) + (1e-9 if j == 0 else 0.0) for j in range(n)]
        out.append(("near singular", m))
    for k in range(8):
        n = 2 + k % 4
        m = random_matrix(rnd, n, n, -1.0, 1.0)
        if k % 2 == 0:
            m[-1] = list(m[0])  # duplicated row
        else:
            for row in m:
                row[k % n] = 0.0  # zero column
        out.append(("singular", m))
    return out


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for k in range(20):
        r, c, kk = 1 + k % 6, 1 + (k * 5 + 2) % 6, 1 + (k * 7 + 3) % 6
        n = 1 + (k * 3 + 1) % 6
        a, b = random_matrix(rnd, r, c), random_matrix(rnd, r, c)
        cm, q = random_matrix(rnd, c, kk), random_matrix(rnd, n, n, -1.0, 1.0)
        vv, ww = [rnd.uniform(-5.0, 5.0) for _ in range(r)], [rnd.uniform(-5.0, 5.0) for _ in range(c)]
        s = rnd.uniform(-3.0, 3.0)
        wrong, wrong_product = random_matrix(rnd, r + 1, c), random_matrix(rnd, c + 1, kk)
        A, B, C, Q = ql.Matrix(a), ql.Matrix(b), ql.Matrix(cm), ql.Matrix(q)
        W, WP = ql.Matrix(wrong), ql.Matrix(wrong_product)
        V, Wv = ql.Array(vv), ql.Array(ww)
        out.append({"name": f"{k} {r}x{c}",
                    "inputs": {"settings": st, "a": a, "b": b, "c": cm, "q": q, "v": vv, "w": ww, "s": s,
                               "wrong": wrong, "wrongProduct": wrong_product},
                    "expected": {
                        "sum": rows_of(A + B),
                        "difference": rows_of(A - B),
                        "product": rows_of(A * C),
                        "scaled": rows_of(A * s),
                        "scaledLeft": rows_of(s * A),
                        "transpose": rows_of(ql.transpose(A)),
                        "outerProduct": rows_of(ql.outerProduct(V, Wv)),
                        "inverse": v.attempt(lambda: rows_of(ql.inverse(Q))),
                        "vectorTimesMatrix": as_list(V * A),
                        "matrixTimesVector": as_list(A * Wv),
                        "rows": A.rows(),
                        "columns": A.columns(),
                        "text": str(A),
                        "filled": rows_of(ql.Matrix(r, c, s)),
                        "sumWrongSize": v.attempt(lambda: rows_of(A + W)),
                        "differenceWrongSize": v.attempt(lambda: rows_of(A - W)),
                        "productWrongSize": v.attempt(lambda: rows_of(A * WP)),
                        "vectorTimesMatrixWrongSize": v.attempt(lambda: as_list(ql.Array(r + 1, 1.0) * A)),
                        "matrixTimesVectorWrongSize": v.attempt(lambda: as_list(A * ql.Array(c + 1, 1.0)))}})
    for k, (family, m) in enumerate(inverse_matrices(rnd)):
        M = ql.Matrix(m)
        out.append({"name": f"inverse {k} {family} {M.rows()}x{M.rows()}",
                    "inputs": {"settings": st, "m": m},
                    "expected": {"inverse": v.attempt(lambda: rows_of(ql.inverse(M)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrix.hpp", cases)
