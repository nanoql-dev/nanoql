# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for GMRES (ql/math/matrixutilities/gmres.hpp, .cpp): the solution of tools/vectors/gen/math/matrixutilities/
bicgstab.py's seeded random systems of sizes 2 to 12 (size 1 breaks down at the first step, where QuantLib's process
crashes), with and without an initial guess, without a preconditioner, with a Jacobi one and with the exact inverse, at
relative tolerances 1e-8 and 1e-12, by solve with as many steps as the size and by solveWithRestart with two steps a
cycle and one or 40 cycles; a zero right-hand side, which returns it; and refusals: no step allowed ("maxIter must be
greater than zero") and one step for a system of 12 ("could not converge"). Random dense systems don't break down before
their size, so none reaches the breakdown nanoql completes where QuantLib drops the column
(Divergences/GmresBreakdownTests.cs); no restart count is zero, which nanoql refuses. The wheel's solve returns the
solution alone; the residuals are vectored through the C++ harness
(tools/vectors/gen/cxx/math/matrixutilities/gmres.py). The wheel's class is a SWIG wrapper of the C++ class, with no
logic of its own, calling the Python products. Read back by
tests/NanoQL.Tests/Vectors/Numerics/MatrixUtilities/GMRESVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bi = _load("bicgstab", "math/matrixutilities/bicgstab.py")


def solver(rows, kind, max_iter, rel_tol):
    m = bi.proxy(rows, kind)
    return (ql.GMRES(bi.product(rows), max_iter, rel_tol) if m is None
            else ql.GMRES(bi.product(rows), max_iter, rel_tol, m))


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    # a system of size 1 breaks down at the first step, which crashes QuantLib's process
    for name, rows, b, x0 in [s for s in bi.systems() if len(s[1]) > 1]:
        n = len(rows)
        args = [ql.Array(b)] + ([] if x0 is None else [ql.Array(x0)])
        for kind in bi.PRECONDITIONERS:
            for rel_tol in (1e-8, 1e-12):
                s = solver(rows, kind, n, rel_tol)
                out.append({"name": f"{name}, {kind}, {rel_tol!r}",
                            "inputs": {"settings": st, "A": rows, "b": b, "x0": x0, "preconditioner": kind,
                                       "maxIter": n, "relTol": rel_tol},
                            "expected": {"x": bi.solution(lambda: s.solve(*args))}})
        for restart in (1, 40):
            s = solver(rows, "none", 2, 1e-8)
            out.append({"name": f"{name}, two steps, restart {restart}",
                        "inputs": {"settings": st, "A": rows, "b": b, "x0": x0, "preconditioner": "none",
                                   "maxIter": 2, "relTol": 1e-8, "restart": restart},
                        "expected": {"x": bi.solution(lambda: s.solveWithRestart(restart, *args))}})
    rows = [[2.0, 1.0, 0.5], [0.3, 3.0, -1.0], [1.0, 0.2, 4.0]]
    out.append({"name": "zero right-hand side",
                "inputs": {"settings": st, "A": rows, "b": [0.0, 0.0, 0.0], "x0": None, "preconditioner": "none",
                           "maxIter": 3, "relTol": 1e-8},
                "expected": {"x": bi.solution(lambda: solver(rows, "none", 3, 1e-8).solve(ql.Array(3, 0.0)))}})
    out.append({"name": "no step allowed",
                "inputs": {"settings": st, "A": rows, "b": [1.0, 2.0, 3.0], "x0": None, "preconditioner": "none",
                           "maxIter": 0, "relTol": 1e-8},
                "expected": {"x": bi.solution(lambda: solver(rows, "none", 0, 1e-8).solve(ql.Array([1.0, 2.0, 3.0])))}})
    big = [r for r in bi.systems() if r[0] == "dense 12"][0]
    out.append({"name": "one step for 12",
                "inputs": {"settings": st, "A": big[1], "b": big[2], "x0": None, "preconditioner": "none",
                           "maxIter": 1, "relTol": 1e-8},
                "expected": {"x": bi.solution(lambda: solver(big[1], "none", 1, 1e-8).solve(ql.Array(big[2])))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/gmres.hpp", cases)
