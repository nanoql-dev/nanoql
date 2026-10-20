# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for BiCGstab's result (ql/math/matrixutilities/bicgstab.hpp, .cpp), its iteration count and error, which the
wheel's solve doesn't return, with the solution, each computed by the C++ harness: the cases of
tools/vectors/gen/math/matrixutilities/bicgstab.py, its seeded random systems, preconditioners, tolerances, zero
right-hand side and refusals, the systems written into the inputs as there. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/MatrixUtilities/BiCGstabCxxVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bi = _load("bicgstab", "math/matrixutilities/bicgstab.py")


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def solve(rows, b, x0, max_iter, rel_tol, kind):
            return v.attempt(lambda: H.call("BiCGstab::solve", rows, b, x0, max_iter, rel_tol, kind))

        out = []
        for name, rows, b, x0 in bi.systems():
            n = len(rows)
            for kind in bi.PRECONDITIONERS:
                for rel_tol in (1e-8, 1e-12):
                    out.append({"name": f"{name}, {kind}, {rel_tol!r}",
                                "inputs": {"settings": st, "A": rows, "b": b, "x0": x0, "preconditioner": kind,
                                           "maxIter": 2 * n + 5, "relTol": rel_tol},
                                "expected": {"result": solve(rows, b, x0, 2 * n + 5, rel_tol, kind)}})
        rng = random.Random(7)
        rows = [[rng.uniform(-1.0, 1.0) + (3.0 if i == j else 0.0) for j in range(3)] for i in range(3)]
        big = [r for r in bi.systems() if r[0] == "dense 12"][0]
        for name, a, b, max_iter in (("zero right-hand side", rows, [0.0, 0.0, 0.0], 5),
                                     ("no iteration allowed", rows, [1.0, 2.0, 3.0], 0),
                                     ("one iteration for 12", big[1], big[2], 1)):
            out.append({"name": name,
                        "inputs": {"settings": st, "A": a, "b": b, "x0": None, "preconditioner": "none",
                                   "maxIter": max_iter, "relTol": 1e-8},
                        "expected": {"result": solve(a, b, None, max_iter, 1e-8, "none")}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/bicgstab.hpp", cases)
