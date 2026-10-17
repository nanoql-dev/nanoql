# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the safe Newton 1-D solver (ql/math/solvers1d/newtonsafe.hpp) and the bracketing of its base,
Solver1D (ql/math/solver1d.hpp): the cases of tools/vectors/gen/math/solvers1d/brent.py, its six polynomials with
their derivatives below, each case recording the root (or the throw), the number of calls of the function and of its
derivative, and the argument of the function's last call.

The derivatives are written the same way in tests/NanoQL.Tests/Vectors/Numerics/Solvers1D/Solver1DPolynomials.cs;
tests/NanoQL.Tests/Vectors/Numerics/Solvers1D/NewtonSafeVectorTests.cs reads the file back through NewtonSafe's
delegate overloads. The wheel's NewtonSafe is QuantLib-SWIG's DeclareSolver(NewtonSafe), as for Brent: its solve
wraps the Python object in a UnaryFunction, whose operator() calls it and whose derivative() calls its derivative
method, and forwards to Solver1D::solve."""
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

DERIVATIVES = {
    "p1": lambda x: 2.0 * x,
    "p2": lambda x: -2.0 * x,
    "p3": lambda x: 3.0 * x * x - 2.0,
    "p4": lambda x: (x + 2.0) * (x - 4.0) + (x - 0.3) * (x - 4.0) + (x - 0.3) * (x + 2.0),
    "p5": lambda x: 4.0 * x * x * x,
    "p6": lambda x: 1.5 * x * x - 2.0 * x,
}


class Counted(brent.Counted):
    """The function with its derivative, counting the calls of both."""

    def __init__(self, f, df):
        super().__init__(f)
        self.df = df
        self.derivative_calls = 0

    def derivative(self, x):
        self.derivative_calls += 1
        return self.df(x)


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    named = [(f"{p} {i}", spec) for p, specs in brent.SOLVES for i, spec in enumerate(specs)] + brent.EDGES
    out = []
    for name, spec in named:
        f = Counted(brent.POLYNOMIALS[spec["function"]], DERIVATIVES[spec["function"]])
        solver = brent.configured(ql.NewtonSafe(), spec)
        root = v.attempt(lambda: brent.solve(solver, f, spec))
        out.append({"name": name, "inputs": brent.inputs(st, spec),
                    "expected": {"root": root, "calls": f.calls, "derivativeCalls": f.derivative_calls,
                                 "lastArgument": v.enc(f.last)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/solvers1d/newtonsafe.hpp", cases)
