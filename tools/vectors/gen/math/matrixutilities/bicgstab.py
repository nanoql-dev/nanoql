# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for BiCGstab (ql/math/matrixutilities/bicgstab.hpp, .cpp): the solution of random dense systems of sizes 1 to
12, diagonally dominant or not, with and without an initial guess, without a preconditioner, with a Jacobi (diagonal)
one and with the exact inverse, at relative tolerances 1e-8 and 1e-12; a zero right-hand side, which returns it;
refusals ("max number of iterations exceeded") with no iteration allowed from a zero guess and with one for a system of
12, each far from the last-iteration edge nanoql answers where QuantLib refuses
(Divergences/BiCGstabLastIterationTests.cs); and a breakdown short of the tolerance ("could not converge"): the system
[[1, 1], [-1, 0]], whose first step's omega is 0 and whose second's rho is. The systems, seeded, are written into the
inputs and shared with tools/vectors/gen/math/matrixutilities/gmres.py. The wheel's solve returns the solution alone;
the iteration count and the error are vectored through the C++ harness
(tools/vectors/gen/cxx/math/matrixutilities/bicgstab.py). The wheel's class is a SWIG wrapper of the C++ class, with no
logic of its own, calling the Python products. Read back by
tests/NanoQL.Tests/Vectors/Numerics/MatrixUtilities/BiCGstabVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

SIZES = [1, 2, 3, 5, 8, 12]
PRECONDITIONERS = ["none", "jacobi", "inverse"]


def systems():
    """Seeded random systems: (name, matrix rows, right-hand side, initial guess or None)."""
    rng = random.Random(20261020)
    out = []
    for n in SIZES:
        for dominant in (True, False):
            rows = [[rng.uniform(-1.0, 1.0) + (n if dominant and i == j else 0.0) for j in range(n)] for i in range(n)]
            b = [rng.uniform(-2.0, 2.0) for _ in range(n)]
            x0 = [rng.uniform(-1.0, 1.0) for _ in range(n)]
            kind = "dominant" if dominant else "dense"
            out.append((f"{kind} {n}", rows, b, None))
            out.append((f"{kind} {n}, guess", rows, b, x0))
    return out


def proxy(rows, kind):
    m = ql.Matrix(rows)
    n = len(rows)
    if kind == "none":
        return None
    if kind == "jacobi":
        d = [rows[i][i] for i in range(n)]
        return ql.MatrixMultiplicationProxy(lambda x: ql.Array([x[i] / d[i] for i in range(n)]))
    inverse = ql.inverse(m)
    return ql.MatrixMultiplicationProxy(lambda x: inverse * x)


def product(rows):
    m = ql.Matrix(rows)
    return ql.MatrixMultiplicationProxy(lambda x: m * x)


def solution(solve):
    return v.attempt(lambda: list(solve()))


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, rows, b, x0 in systems():
        n = len(rows)
        for kind in PRECONDITIONERS:
            for rel_tol in (1e-8, 1e-12):
                m = proxy(rows, kind)
                solver = (ql.BiCGstab(product(rows), 2 * n + 5, rel_tol) if m is None
                          else ql.BiCGstab(product(rows), 2 * n + 5, rel_tol, m))
                args = [ql.Array(b)] + ([] if x0 is None else [ql.Array(x0)])
                out.append({"name": f"{name}, {kind}, {rel_tol!r}",
                            "inputs": {"settings": st, "A": rows, "b": b, "x0": x0, "preconditioner": kind,
                                       "maxIter": 2 * n + 5, "relTol": rel_tol},
                            "expected": {"x": solution(lambda: solver.solve(*args))}})
    rng = random.Random(7)
    rows = [[rng.uniform(-1.0, 1.0) + (3.0 if i == j else 0.0) for j in range(3)] for i in range(3)]
    big = [r for r in systems() if r[0] == "dense 12"][0]
    for name, a, b, x0, max_iter in (("zero right-hand side", rows, [0.0, 0.0, 0.0], None, 5),
                                     ("no iteration allowed", rows, [1.0, 2.0, 3.0], None, 0),
                                     ("one iteration for 12", big[1], big[2], None, 1),
                                     ("breakdown", [[1.0, 1.0], [-1.0, 0.0]], [1.0, 0.0], None, 5)):
        solver = ql.BiCGstab(product(a), max_iter, 1e-8)
        out.append({"name": name,
                    "inputs": {"settings": st, "A": a, "b": b, "x0": x0, "preconditioner": "none", "maxIter": max_iter,
                               "relTol": 1e-8},
                    "expected": {"x": solution(lambda: solver.solve(ql.Array(b)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/bicgstab.hpp", cases)
