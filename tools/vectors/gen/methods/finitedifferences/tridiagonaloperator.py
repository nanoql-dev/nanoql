# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TridiagonalOperator (ql/methods/finitedifferences/tridiagonaloperator.hpp, .cpp) through
the members the wheel exposes: 20 operators of 3 to 10 rows, built from three random diagonals (the main one
dominant in half of them), each with a vector u, a right-hand side r, row values and a scalar s. For each: the size;
the operator as built; after setFirstRow, setMidRow on an inner row and setLastRow; after setMidRows on a fresh copy;
applyTo(u); solveFor(r); the sum and the difference with a second operator of its size; the product by s; and the
refusals of diagonals of the wrong size, of applyTo and solveFor with a vector of another size, of setMidRow on the
first and the last row, and of a sum of operators of different sizes. Then the identity of 2 to 10 rows.

The wheel has no accessor for the diagonals, so an operator is read through applyTo on three comb vectors, the k-th
holding 1 where the index is k modulo 3 and 0 elsewhere: element j of the k-th product is exactly one coefficient of
row j (the main, lower or upper one), since the other two are multiplied by 0. Each "operator" output holds the three
products.

The wheel's calls, by QuantLib-SWIG's operators.i: ql.TridiagonalOperator(low, mid, high) is the three-diagonal
constructor (SWIG's typemap builds the Arrays from lists); size, applyTo, solveFor(const Array&), setFirstRow,
setMidRow, setMidRows, setLastRow and identity are the members; L + M and L - M (__add__, __sub__) call operator+ and
operator-(const TridiagonalOperator&, const TridiagonalOperator&), and L * s (__mul__) operator*(const
TridiagonalOperator&, Real). SWIG's __rmul__ also computes L * s, and its __div__ is unreachable in Python 3, so
s * L and L / s are the C++ harness's (tools/vectors/gen/cxx/methods/finitedifferences/tridiagonaloperator.py).
Read back by tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/TridiagonalOperatorVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def as_list(a):
    return [a[i] for i in range(len(a))]


def combs(n):
    return [[1.0 if j % 3 == k else 0.0 for j in range(n)] for k in range(3)]


def read(L, n):
    """The operator, as its products with the three comb vectors."""
    return [as_list(L.applyTo(ql.Array(c))) for c in combs(n)]


def diagonals(rnd, n, dominant):
    low = [rnd.uniform(-1.0, 1.0) for _ in range(n - 1)]
    high = [rnd.uniform(-1.0, 1.0) for _ in range(n - 1)]
    mid = [(rnd.uniform(2.5, 4.0) if dominant else rnd.uniform(-2.0, 2.0)) for _ in range(n)]
    return low, mid, high


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for k in range(20):
        n = 3 + k % 8
        low, mid, high = diagonals(rnd, n, k % 2 == 0)
        low2, mid2, high2 = diagonals(rnd, n, True)
        u = [rnd.uniform(-5.0, 5.0) for _ in range(n)]
        r = [rnd.uniform(-5.0, 5.0) for _ in range(n)]
        row = 1 + k % (n - 2)
        rows = {"first": [rnd.uniform(-3.0, 3.0) for _ in range(2)], "mid": [rnd.uniform(-3.0, 3.0) for _ in range(3)],
                "last": [rnd.uniform(-3.0, 3.0) for _ in range(2)], "midRows": [rnd.uniform(-3.0, 3.0) for _ in range(3)]}
        s = rnd.uniform(-3.0, 3.0)

        def make():
            return ql.TridiagonalOperator(low, mid, high)

        L, M = make(), ql.TridiagonalOperator(low2, mid2, high2)
        after_rows = make()
        after_rows.setFirstRow(*rows["first"])
        after_rows.setMidRow(row, *rows["mid"])
        after_rows.setLastRow(*rows["last"])
        after_mid_rows = make()
        after_mid_rows.setMidRows(*rows["midRows"])
        shorter = ql.TridiagonalOperator(low2[:-1], mid2[:-1], high2[:-1])
        out.append({"name": f"{k} size {n}",
                    "inputs": {"settings": st, "low": low, "mid": mid, "high": high, "low2": low2, "mid2": mid2,
                               "high2": high2, "u": u, "r": r, "row": row, "rows": rows, "s": s},
                    "expected": {
                        "size": L.size(),
                        "operator": read(L, n),
                        "afterRowSetters": read(after_rows, n),
                        "afterSetMidRows": read(after_mid_rows, n),
                        "applyTo": as_list(L.applyTo(ql.Array(u))),
                        "solveFor": v.attempt(lambda: as_list(L.solveFor(ql.Array(r)))),
                        "sum": read(L + M, n),
                        "difference": read(L - M, n),
                        "product": read(L * s, n),
                        "lowWrongSize": v.attempt(lambda: ql.TridiagonalOperator(low[:-1], mid, high).size()),
                        "highWrongSize": v.attempt(lambda: ql.TridiagonalOperator(low, mid, high + [1.0]).size()),
                        "applyToWrongSize": v.attempt(lambda: as_list(L.applyTo(ql.Array(u + [1.0])))),
                        "solveForWrongSize": v.attempt(lambda: as_list(L.solveFor(ql.Array(r[:-1])))),
                        "setMidRowFirst": v.attempt(lambda: make().setMidRow(0, 1.0, 2.0, 3.0)),
                        "setMidRowLast": v.attempt(lambda: make().setMidRow(n - 1, 1.0, 2.0, 3.0)),
                        "sumWrongSize": v.attempt(lambda: (L + shorter).size())}})
    sizes = list(range(2, 11))
    out.append({"name": "identity",
                "inputs": {"settings": st, "sizes": sizes},
                "expected": {"operator": [read(ql.TridiagonalOperator.identity(n), n) for n in sizes]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/tridiagonaloperator.hpp", cases)
