# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for GMRES's result (ql/math/matrixutilities/gmres.hpp, .cpp), its relative residuals, which the wheel's
solve and solveWithRestart don't return, with the solution, each computed by the C++ harness: the cases of
tools/vectors/gen/math/matrixutilities/gmres.py, its seeded random systems of sizes 2 to 12 (none breaking down),
preconditioners, tolerances, restarted solves, zero right-hand side and refusals, the systems written into the inputs
as there; a case's restart is null for solve. No restart count is zero, which nanoql refuses
(Divergences/GmresBreakdownTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/MatrixUtilities/GMRESCxxVectorTests.cs."""
import importlib.util
import os
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

        def solve(rows, b, x0, max_iter, rel_tol, kind, restart=None):
            if restart is None:
                return v.attempt(lambda: H.call("GMRES::solve", rows, b, x0, max_iter, rel_tol, kind))
            return v.attempt(lambda: H.call("GMRES::solveWithRestart", restart, rows, b, x0, max_iter, rel_tol, kind))

        def case(name, rows, b, x0, kind, max_iter, rel_tol, restart=None):
            return {"name": name,
                    "inputs": {"settings": st, "A": rows, "b": b, "x0": x0, "preconditioner": kind,
                               "maxIter": max_iter, "relTol": rel_tol, "restart": restart},
                    "expected": {"result": solve(rows, b, x0, max_iter, rel_tol, kind, restart)}}

        out = []
        # a system of size 1 breaks down at the first step, which crashes QuantLib's process
        for name, rows, b, x0 in [s for s in bi.systems() if len(s[1]) > 1]:
            n = len(rows)
            for kind in bi.PRECONDITIONERS:
                for rel_tol in (1e-8, 1e-12):
                    out.append(case(f"{name}, {kind}, {rel_tol!r}", rows, b, x0, kind, n, rel_tol))
            for restart in (1, 40):
                out.append(case(f"{name}, two steps, restart {restart}", rows, b, x0, "none", 2, 1e-8, restart))
        rows = [[2.0, 1.0, 0.5], [0.3, 3.0, -1.0], [1.0, 0.2, 4.0]]
        out.append(case("zero right-hand side", rows, [0.0, 0.0, 0.0], None, "none", 3, 1e-8))
        out.append(case("no step allowed", rows, [1.0, 2.0, 3.0], None, "none", 0, 1e-8))
        big = [r for r in bi.systems() if r[0] == "dense 12"][0]
        out.append(case("one step for 12", big[1], big[2], None, "none", 1, 1e-8))
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/gmres.hpp", cases)
