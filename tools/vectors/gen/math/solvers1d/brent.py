# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Brent's 1-D solver (ql/math/solvers1d/brent.hpp) and the bracketing of its base, Solver1D
(ql/math/solver1d.hpp): six polynomials, each solved four ways (from a guess and a step at two accuracies, within a
bracket, and from a guess with a lower or an upper bound set that the bracketing meets), then the refusals and edges
of both solve forms: an accuracy of 0, negative, NaN and below machine epsilon; an empty range, a range that brackets
no root, a guess outside the range, a range outside the bounds set; a root at the guess, at an end of the range or at
an end the bracketing reaches; equal function values at the bracketing's ends, once and at every step (its
alternating branches); brackets where Brent bisects, rejecting the interpolation or finding the bounds decreasing too
slowly for it; and the bracketing and the solver
running out of function evaluations, the maximum set to 0, 3, 4 and 6, and by a bound that keeps the bracketing from
the root. Each case records the root (or the throw), the number of calls of the function, and the argument of its last
call, which is the root's own when the solver returns from its loop.

The polynomials, POLYNOMIALS below, are written the same way in
tests/NanoQL.Tests/Vectors/Numerics/Solvers1D/Solver1DPolynomials.cs, so the arithmetic is the same;
tests/NanoQL.Tests/Vectors/Numerics/Solvers1D/BrentVectorTests.cs reads the file back through Brent's delegate
overloads. The wheel's Brent is QuantLib-SWIG's DeclareSolver(Brent): setMaxEvaluations, setLowerBound and
setUpperBound are Solver1D's, and solve(function, accuracy, guess, step) and solve(function, accuracy, guess, xMin,
xMax) wrap the Python function in a UnaryFunction, whose operator() calls it, and forward to Solver1D::solve, with no
logic of their own."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

NAN = float("nan")

# The six polynomials, each operation written out so the C# test computes the same values.
POLYNOMIALS = {
    "p1": lambda x: x * x - 1.0,
    "p2": lambda x: 1.0 - x * x,
    "p3": lambda x: x * x * x - 2.0 * x - 5.0,
    "p4": lambda x: (x - 0.3) * (x + 2.0) * (x - 4.0),
    "p5": lambda x: x * x * x * x - 10.0,
    "p6": lambda x: 0.5 * x * x * x - x * x + 0.25,
}


def step(function, accuracy, guess, step_, **bounds):
    return {"function": function, "form": "step", "accuracy": accuracy, "guess": guess, "step": step_, **bounds}


def bracket(function, accuracy, guess, x_min, x_max, **bounds):
    return {"function": function, "form": "bracket", "accuracy": accuracy, "guess": guess, "xMin": x_min,
            "xMax": x_max, **bounds}


# Per polynomial: two guesses and steps, a bracket, and a bound the bracketing meets.
SOLVES = [
    ("p1", [step("p1", 1e-10, 0.5, 0.1), step("p1", 1e-6, -3.0, 0.5), bracket("p1", 1e-8, 0.5, 0.0, 2.0),
            step("p1", 1e-8, 3.0, 2.5, lowerBound=0.5)]),
    ("p2", [step("p2", 1e-10, 1.5, 0.1), step("p2", 1e-6, -0.25, 0.3), bracket("p2", 1e-8, 1.5, 0.5, 3.0),
            step("p2", 1e-8, 0.0, 2.0, lowerBound=-1.5)]),
    ("p3", [step("p3", 1e-10, 2.0, 0.1), step("p3", 1e-6, -1.0, 1.0), bracket("p3", 1e-12, 2.5, 2.0, 3.0),
            step("p3", 1e-8, 5.0, 4.0, lowerBound=2.05)]),
    ("p4", [step("p4", 1e-10, 0.0, 0.1), step("p4", 1e-6, 5.0, 0.25), bracket("p4", 1e-8, -1.0, -3.0, 0.0),
            step("p4", 1e-8, 2.0, 3.0, upperBound=4.5)]),
    ("p5", [step("p5", 1e-10, 1.0, 0.1), step("p5", 1e-6, -0.5, 0.2), bracket("p5", 1e-8, 1.5, 0.0, 3.0),
            step("p5", 1e-8, 3.0, 2.0, lowerBound=1.5)]),
    ("p6", [step("p6", 1e-10, 0.5, 0.1), step("p6", 1e-6, 3.0, 0.5), bracket("p6", 1e-8, 1.5, 1.0, 2.0),
            step("p6", 1e-8, -2.0, 1.0, upperBound=-0.2)]),
]

EDGES = [
    ("accuracy 0", step("p1", 0.0, 0.5, 0.1)),
    ("accuracy negative", bracket("p1", -1e-8, 0.5, 0.0, 2.0)),
    ("accuracy NaN", step("p1", NAN, 0.5, 0.1)),
    ("accuracy below machine epsilon", step("p3", 1e-20, 2.0, 0.1)),
    ("empty range", bracket("p1", 1e-8, 1.0, 2.0, 0.0)),
    ("range bracketing no root", bracket("p1", 1e-8, 2.5, 2.0, 3.0)),
    ("guess below the range", bracket("p1", 1e-8, -0.5, 0.0, 2.0)),
    ("guess above the range", bracket("p1", 1e-8, 2.5, 0.0, 2.0)),
    ("range below the lower bound", bracket("p1", 1e-8, 0.75, 0.0, 2.0, lowerBound=0.5)),
    ("range above the upper bound", bracket("p1", 1e-8, 0.75, 0.0, 2.0, upperBound=1.5)),
    ("root at the range's lower end", bracket("p1", 1e-8, 1.5, 1.0, 2.0)),
    ("root at the range's upper end", bracket("p1", 1e-8, 0.5, 0.0, 1.0)),
    ("root at the guess", step("p1", 1e-8, 1.0, 0.1)),
    ("root at the bracketing's lower end", step("p1", 1e-8, 1.5, 0.5)),
    ("root at the bracketing's upper end", step("p1", 1e-8, 0.5, 0.5)),
    ("equal values at the bracketing's ends", step("p1", 1e-8, -0.5, 1.0)),
    ("equal values at every bracketing step", step("p1", 1e-8, 0.0, 0.0)),
    ("interpolation rejected for bisection", bracket("p1", 1e-8, -2.625, -3.0, 0.0)),
    ("bounds decreasing too slowly for interpolation", bracket("p1", 1e-8, 1.375, -0.5, 2.0)),
    ("bound keeping the bracketing from the root", step("p1", 1e-8, 3.0, 0.5, lowerBound=2.5)),
    ("bracketing out of evaluations", step("p3", 1e-8, -1.0, 0.01, maxEvaluations=3)),
    ("solver out of evaluations", step("p3", 1e-15, 2.0, 0.1, maxEvaluations=6)),
    ("maximum evaluations 0", step("p1", 1e-8, 0.5, 0.1, maxEvaluations=0)),
    ("bracketed solver out of evaluations", bracket("p4", 1e-14, -1.0, -3.0, 0.0, maxEvaluations=4)),
]


class Counted:
    """The function the solver calls, counting its calls and keeping the last argument."""

    def __init__(self, f):
        self.f = f
        self.calls = 0
        self.last = None

    def __call__(self, x):
        self.calls += 1
        self.last = x
        return self.f(x)


def configured(solver, spec):
    """The solver with the spec's maximum evaluations and bounds set, as nanoql's setters return them."""
    if spec.get("maxEvaluations") is not None:
        solver.setMaxEvaluations(spec["maxEvaluations"])
    if spec.get("lowerBound") is not None:
        solver.setLowerBound(spec["lowerBound"])
    if spec.get("upperBound") is not None:
        solver.setUpperBound(spec["upperBound"])
    return solver


def solve(solver, f, spec):
    if spec["form"] == "step":
        return solver.solve(f, spec["accuracy"], spec["guess"], spec["step"])
    return solver.solve(f, spec["accuracy"], spec["guess"], spec["xMin"], spec["xMax"])


def inputs(st, spec):
    out = {"settings": st}
    for k in ("function", "form", "accuracy", "guess", "step", "xMin", "xMax"):
        if k in spec:
            out[k] = v.enc(spec[k])
    for k in ("maxEvaluations", "lowerBound", "upperBound"):
        out[k] = spec.get(k)
    return out


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    named = [(f"{p} {i}", spec) for p, specs in SOLVES for i, spec in enumerate(specs)] + EDGES
    out = []
    for name, spec in named:
        f = Counted(POLYNOMIALS[spec["function"]])
        solver = configured(ql.Brent(), spec)
        root = v.attempt(lambda: solve(solver, f, spec))
        out.append({"name": name, "inputs": inputs(st, spec),
                    "expected": {"root": root, "calls": f.calls, "lastArgument": v.enc(f.last)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/solvers1d/brent.hpp", cases)
