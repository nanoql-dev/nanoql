# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Ridder's 1-D solver (ql/math/solvers1d/ridder.hpp) and the bracketing of its base, Solver1D
(ql/math/solver1d.hpp): the cases of tools/vectors/gen/math/solvers1d/brent.py, its six polynomials each solved four
ways and the refusals and edges of both solve forms, each case recording the root (or the throw), the number of calls
of the function and the argument of its last call.

The polynomials are written the same way in tests/NanoQL.Tests/Vectors/Numerics/Solvers1D/Solver1DPolynomials.cs;
tests/NanoQL.Tests/Vectors/Numerics/Solvers1D/RidderVectorTests.cs reads the file back through Ridder's delegate
overloads. The wheel's Ridder is QuantLib-SWIG's DeclareSolver(Ridder), as for Brent: its solve wraps the Python
function in a UnaryFunction, whose operator() calls it, and forwards to Solver1D::solve."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _dir)
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import brent  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    named = [(f"{p} {i}", spec) for p, specs in brent.SOLVES for i, spec in enumerate(specs)] + brent.EDGES
    out = []
    for name, spec in named:
        f = brent.Counted(brent.POLYNOMIALS[spec["function"]])
        solver = brent.configured(ql.Ridder(), spec)
        root = v.attempt(lambda: brent.solve(solver, f, spec))
        out.append({"name": name, "inputs": brent.inputs(st, spec),
                    "expected": {"root": root, "calls": f.calls, "lastArgument": v.enc(f.last)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/solvers1d/ridder.hpp", cases)
