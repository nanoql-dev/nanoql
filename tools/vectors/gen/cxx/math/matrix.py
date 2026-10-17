# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's inverse and determinant (ql/math/matrix.hpp, matrix.cpp), which run Boost uBLAS's LU
factorization with partial pivoting, and for the members of Matrix the wheel doesn't expose (docs/porting-playbook.md
§9.4), each output computed by the C++ harness.

60 square matrices of 1 to 6 rows through determinant and inverse, in the Boost survey's families, which reach every
branch of lu_factorize and the solves: random; small integers (ties, zeros); pivot ties, where every row's first
element has the same magnitude and the matrix is near singular, so that taking another tied row as the pivot changes
the results beyond the vectors' tolerance; signed zeros; rows scaled by 2^k, k in [-60, 60]; elements of magnitude
10^±300, whose products overflow and underflow; duplicated rows; singular (a zero row or column, rank one, zero); NaN;
and infinities. inverse's refusal of a singular matrix is recorded where it refuses. Then the refusals of a matrix
that isn't square, and the matrix with no rows.

Six matrices (3x3, 2x4, 4x2 with NaN and a signed zero, 1x1, 3x0, 0x0) through rows, columns, size1, size2, empty,
diagonal, each row (row_begin to row_end), at (its refusal past the last row included), each column (column_begin to
column_end), the unary -, the quotient by a scalar, the compound assignments *=, /=, += and -= (and the last two's
refusal of another size), swap with a matrix of another shape, == and != against a copy and against another matrix,
and the text; and Matrix(Size, Size)'s dimensions.

A matrix argument is the spec {"rows", "columns", "data"}, its elements by rows, which the harness builds with the
iterator-range constructor; a matrix result is its list of rows. tests/NanoQL.Tests/Vectors/Cxx/Numerics/
MatrixCxxVectorTests.cs reads it back and prints how many real outputs are bit-identical to QuantLib's."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

NAN, INF = math.nan, math.inf


def spec(rows):
    r = len(rows)
    c = len(rows[0]) if r else 0
    return {"rows": r, "columns": c, "data": [x for row in rows for x in row]}


def uniform(rnd, r, c, lo=-1.0, hi=1.0):
    return [[rnd.uniform(lo, hi) for _ in range(c)] for _ in range(r)]


def pivot_ties(rnd, n, eps, scale):
    """Every row's first element +-scale, so the first pivot search is an n-row tie; the last row an exact combination
    of the others (dyadic coefficients, so no rounding), its first element +-scale and its last perturbed by eps * scale,
    so the matrix is near singular and the pivot's choice shows in the results."""
    rows = [[rnd.choice([1.0, -1.0])] + [rnd.randint(-9, 9) / 4.0 for _ in range(n - 1)] for _ in range(n - 1)]
    target = rnd.choice([1.0, -1.0])
    c = [rnd.randint(-7, 7) / 8.0 for _ in range(n - 2)]
    c.append((target - sum(c[k] * rows[k][0] for k in range(n - 2))) * rows[n - 2][0])
    last = [sum(c[k] * rows[k][j] for k in range(n - 1)) for j in range(n)]
    assert last[0] == target
    last[-1] += eps
    return [[x * scale for x in row] for row in rows + [last]]


def families(rnd):
    out = []
    for n in range(1, 7):
        out.append(("random", uniform(rnd, n, n)))
    for k in range(10):
        n = 2 + k % 5
        out.append(("small integers", [[float(rnd.randint(-3, 3)) for _ in range(n)] for _ in range(n)]))
    for n in (3, 4, 5):
        for eps in (1e-6, 1e-8, 1e-10):
            out.append(("pivot ties", pivot_ties(rnd, n, eps, 2.0 ** 20)))
    out.append(("pivot ties", pivot_ties(rnd, 4, 1e-8, 1.0)))
    for n in (2, 3, 4, 2, 3, 4):
        out.append(("signed zeros", [[rnd.choice([-0.0, -0.0, 0.0, 1.0, -1.0, 0.5, -2.0]) for _ in range(n)]
                                     for _ in range(n)]))
    for n in (2, 3, 4, 5, 6, 3):
        scaled = []
        for row in uniform(rnd, n, n):
            k = rnd.randint(-60, 60)
            scaled.append([math.ldexp(x, k) for x in row])
        out.append(("rows scaled by 2^k", scaled))
    for n in (2, 3, 4, 5, 6, 3):
        out.append(("magnitudes 10^+-300", [[rnd.choice([1.0, -1.0]) * 10.0 ** rnd.uniform(-300.0, 300.0)
                                             for _ in range(n)] for _ in range(n)]))
    for n in range(2, 7):
        m = uniform(rnd, n, n)
        m[-1] = list(m[0])
        out.append(("duplicated rows", m))
    zero_column = uniform(rnd, 3, 3)
    for row in zero_column:
        row[1] = 0.0
    zero_row = uniform(rnd, 3, 3)
    zero_row[0] = [0.0, 0.0, 0.0]
    u, w = [rnd.uniform(-1.0, 1.0) for _ in range(3)], [rnd.uniform(-1.0, 1.0) for _ in range(3)]
    out += [("singular", zero_column), ("singular", zero_row), ("singular", [[a * b for b in w] for a in u]),
            ("singular", [[0.0, 0.0], [0.0, 0.0]])]
    nan_pivot = uniform(rnd, 2, 2)
    nan_pivot[0][0] = NAN
    nan_inner = uniform(rnd, 3, 3)
    nan_inner[1][2] = NAN
    nan_column = uniform(rnd, 3, 3)
    for row in nan_column:
        row[0] = NAN
    nan_later = uniform(rnd, 4, 4)
    nan_later[3][1] = NAN
    out += [("NaN", nan_pivot), ("NaN", nan_inner), ("NaN", nan_column), ("NaN", nan_later)]
    out += [("infinities", [[INF, 1.0], [2.0, 3.0]]),
            ("infinities", [[1.0, INF, 0.5], [-INF, 2.0, 1.0], [0.25, 1.0, 3.0]]),
            ("infinities", [[2.0, 1.0, 0.0], [1.0, -INF, 1.0], [0.0, 1.0, 2.0]])]
    return out


MEMBERS = [
    ("3x3", 3, 3), ("2x4", 2, 4), ("4x2 with NaN and a signed zero", 4, 2), ("1x1", 1, 1), ("3x0", 3, 0), ("0x0", 0, 0)]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        for k, (family, rows) in enumerate(families(rnd)):
            m = spec(rows)
            out.append({"name": f"{k} {family} {m['rows']}x{m['columns']}",
                        "inputs": {"settings": st, "m": v.enc(m)},
                        "expected": {"determinant": call("determinant", m), "inverse": call("inverse", m)}})
        for r, c in ((2, 3), (3, 2), (1, 0), (0, 1), (0, 0)):
            m = spec(uniform(rnd, r, c)) if r else {"rows": 0, "columns": c, "data": []}
            out.append({"name": f"shape {r}x{c}",
                        "inputs": {"settings": st, "m": v.enc(m)},
                        "expected": {"determinant": call("determinant", m), "inverse": call("inverse", m)}})

        shapes = [[0, 0], [2, 3], [0, 4], [3, 0], [5, 5]]
        out.append({"name": "Matrix(Size, Size)",
                    "inputs": {"settings": st, "shapes": shapes},
                    "expected": {"dimensions": [H.call("Matrix(Size,Size)", r, c) for r, c in shapes]}})

        for name, r, c in MEMBERS:
            data = uniform(rnd, r, c, -4.0, 4.0)
            if name.startswith("4x2"):
                data[1][0], data[2][1] = NAN, -0.0
            m = {"rows": r, "columns": c, "data": [x for row in data for x in row]}
            b = {"rows": r, "columns": c, "data": [rnd.uniform(-4.0, 4.0) for _ in range(r * c)]}
            wrong = {"rows": r + 1, "columns": c, "data": [1.0] * ((r + 1) * c)}
            other = {"rows": c + 1, "columns": r, "data": [rnd.uniform(-4.0, 4.0) for _ in range((c + 1) * r)]}
            s = rnd.uniform(-3.0, 3.0)
            out.append({"name": f"members {name}",
                        "inputs": {"settings": st, "m": v.enc(m), "b": v.enc(b), "wrong": v.enc(wrong),
                                   "other": v.enc(other), "s": s},
                        "expected": {
                            "rows": H.call("Matrix::rows", m),
                            "columns": H.call("Matrix::columns", m),
                            "size1": H.call("Matrix::size1", m),
                            "size2": H.call("Matrix::size2", m),
                            "empty": H.call("Matrix::empty", m),
                            "diagonal": call("Matrix::diagonal", m),
                            "storage": call("Matrix::begin", m),
                            "row": [call("Matrix::row_begin", m, i) for i in range(r)],
                            "at": [call("Matrix::at", m, i) for i in range(r + 1)],
                            "column": [call("Matrix::column_begin", m, j) for j in range(c)],
                            "negative": call("operator-(const Matrix&)", m),
                            "quotient": call("operator/(const Matrix&,Real)", m, s),
                            "multiplyAssign": call("Matrix::operator*=(Real)", m, s),
                            "divideAssign": call("Matrix::operator/=(Real)", m, s),
                            "addAssign": call("Matrix::operator+=(const Matrix&)", m, b),
                            "subtractAssign": call("Matrix::operator-=(const Matrix&)", m, b),
                            "addAssignWrongSize": call("Matrix::operator+=(const Matrix&)", m, wrong),
                            "subtractAssignWrongSize": call("Matrix::operator-=(const Matrix&)", m, wrong),
                            "swap": call("Matrix::swap", m, other),
                            "equalCopy": H.call("Matrix::operator==", m, m),
                            "notEqualCopy": H.call("Matrix::operator!=", m, m),
                            "equalOther": H.call("Matrix::operator==", m, b),
                            "notEqualOther": H.call("Matrix::operator!=", m, b),
                            "text": H.call("operator<<(Matrix)", m)}})
        # outerProduct's two refusals, an empty first or second vector, beside a product the wheel's cases don't
        # reach: the empty vectors' (matrix.hpp's "null first vector" and "null second vector").
        pairs = [[[], [1.0, 2.0]], [[1.0, 2.0], []], [[], []], [[-0.5], [3.0, -1.25, 0.0]]]
        out.append({"name": "outerProduct refusals",
                    "inputs": {"settings": st, "pairs": pairs},
                    "expected": {"outerProduct": [call("outerProduct(const Array&,const Array&)", x, y)
                                                  for x, y in pairs]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrix.hpp", cases)
