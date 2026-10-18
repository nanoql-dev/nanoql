# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FiniteDifferenceNewtonSafe (ql/math/solvers1d/finitedifferencenewtonsafe.hpp), through the
two solve forms of its base, Solver1D (ql/math/solver1d.hpp), each output computed by the C++ harness, since the wheel
doesn't expose the solver.

- 108 cases: 12 functions (the six polynomials of tools/vectors/gen/math/solvers1d/brent.py; a triple root, (x - 1)^3;
  1 / x - 2; sqrt(x) - 1.5; x / (1 + x x) - 0.3, with two roots; a quintic with three; and a cubic flat about its
  root, 0.001 (x - 7) + (x - 7)^3) x 3 brackets each, with a guess inside x 3 accuracies (1e-6, 1e-10 and 1e-14).
- 7 cases of the step form, which brackets the root from a guess and a step first: a guess at the root itself, which
  returns at once; bounds enforced on the bracketing; and too few evaluations to bracket it, and a guess from which
  x / (1 + x x) - 0.3 can't be bracketed, its two roots lying on one side and the function tending to -0.3 on both,
  which QuantLib refuses.
- 9 refusals: a zero and a negative accuracy, an empty and a reversed range, a range that brackets no root, a guess
  outside it on either side, a range past an enforced bound, and too few evaluations for the solver.

Each case's outputs are the root, or QuantLib's refusal recorded as its throw; and for a root, the number of calls of
the function, counted by the function itself, since the solver's own count skips the evaluation it makes when its
bisection lands close to the previous root, and the argument of the function's last call, which isn't the root the
solver returns: it returns its last Newton or bisection step without evaluating there. Each case's inputs hold its
settings, "solver" ({"maxEvaluations", "lowerBound", "upperBound"}, null for each default), "function" (its name, the
harness's math/solvers1d/finitedifferencenewtonsafe.cpp lists them), "form" ("bracket" with "xMin" and "xMax", or
"step" with "step"), "accuracy" and "guess". Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Solvers1D/FiniteDifferenceNewtonSafeCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

# Each function's three brackets, (xMin, xMax, guess), every one holding a root.
BRACKETS = {
    "p1": [(0.0, 3.0, 2.0), (0.5, 1.2, 0.6), (-0.5, 1.5, 1.4)],
    "p2": [(0.0, 3.0, 2.0), (0.5, 1.2, 0.6), (-0.5, 1.5, 1.4)],
    "p3": [(2.0, 3.0, 2.5), (1.0, 5.0, 4.0), (2.09, 2.1, 2.095)],
    "p4": [(0.0, 1.0, 0.5), (3.0, 6.0, 5.5), (-3.0, -1.0, -1.5)],
    "p5": [(1.0, 2.0, 1.5), (0.0, 4.0, 3.0), (1.7, 1.9, 1.75)],
    "p6": [(0.0, 1.0, 0.5), (1.0, 2.0, 1.5), (-1.0, 0.0, -0.5)],
    "cube": [(0.0, 2.0, 1.5), (0.5, 3.0, 2.5), (-1.0, 1.2, 0.0)],
    "reciprocal": [(0.25, 1.0, 0.75), (0.1, 5.0, 2.0), (0.45, 0.6, 0.55)],
    "root": [(1.0, 4.0, 3.0), (0.0, 9.0, 1.0), (2.0, 2.5, 2.4)],
    "lorentz": [(0.0, 1.0, 0.5), (1.0, 10.0, 2.0), (0.2, 0.5, 0.25)],
    "quintic": [(0.0, 0.5, 0.25), (1.0, 1.5, 1.2), (-2.0, -1.0, -1.5)],
    "flat": [(6.0, 8.0, 7.5), (0.0, 7.5, 3.0), (6.9, 7.2, 7.1)],
}
ACCURACIES = [1e-6, 1e-10, 1e-14]
DEFAULT = {"maxEvaluations": None, "lowerBound": None, "upperBound": None}


def solver(max_evaluations=None, lower=None, upper=None):
    return {"maxEvaluations": max_evaluations, "lowerBound": lower, "upperBound": upper}


def bracket_case(name, f, accuracy, guess, x_min, x_max, s=DEFAULT):
    return name, {"solver": s, "function": f, "form": "bracket", "accuracy": accuracy, "guess": guess,
                  "xMin": x_min, "xMax": x_max}


def step_case(name, f, accuracy, guess, step, s=DEFAULT):
    return name, {"solver": s, "function": f, "form": "step", "accuracy": accuracy, "guess": guess, "step": step}


def rows():
    out = []
    for f, brackets in BRACKETS.items():
        for k, (x_min, x_max, guess) in enumerate(brackets):
            for accuracy in ACCURACIES:
                out.append(bracket_case(f"{f} bracket {k} accuracy {accuracy:g}", f, accuracy, guess, x_min, x_max))
    out += [
        step_case("step: p3 from 0", "p3", 1e-10, 0.0, 0.1),
        step_case("step: lorentz from 2", "lorentz", 1e-12, 2.0, 0.5),
        step_case("step: p1 from its root", "p1", 1e-8, 1.0, 0.5),
        step_case("step: flat from 0", "flat", 1e-10, 0.0, 0.5),
        step_case("step: p6 within enforced bounds", "p6", 1e-10, 0.3, 0.5, solver(lower=0.1, upper=0.9)),
        step_case("step: p5 with 3 evaluations", "p5", 1e-10, 10.0, 0.01, solver(max_evaluations=3)),
        step_case("step: lorentz from 5", "lorentz", 1e-12, 5.0, 1.0),
        bracket_case("refused: a zero accuracy", "p1", 0.0, 2.0, 0.0, 3.0),
        bracket_case("refused: a negative accuracy", "p1", -1e-8, 2.0, 0.0, 3.0),
        bracket_case("refused: an empty range", "p1", 1e-8, 2.0, 3.0, 3.0),
        bracket_case("refused: a reversed range", "p1", 1e-8, 2.0, 3.0, 0.0),
        bracket_case("refused: no root bracketed", "p1", 1e-8, 2.5, 2.0, 3.0),
        bracket_case("refused: a guess below the range", "p1", 1e-8, -1.0, 0.0, 3.0),
        bracket_case("refused: a guess above the range", "p1", 1e-8, 4.0, 0.0, 3.0),
        bracket_case("refused: a range below the enforced bound", "p1", 1e-8, 2.0, 0.0, 3.0, solver(lower=0.5)),
        bracket_case("refused: 3 evaluations", "p3", 1e-14, 2.5, 1.0, 5.0, solver(max_evaluations=3)),
    ]
    return out


def solve(H, spec):
    if spec["form"] == "bracket":
        return H.call("FiniteDifferenceNewtonSafe::solve(f,Real,Real,Real,Real)", spec["solver"], spec["function"],
                      spec["accuracy"], spec["guess"], spec["xMin"], spec["xMax"])
    return H.call("FiniteDifferenceNewtonSafe::solve(f,Real,Real,Real)", spec["solver"], spec["function"],
                  spec["accuracy"], spec["guess"], spec["step"])


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(v.enc(ql.Date(15, 1, 2024)))
        for name, spec in rows():
            try:
                expected = solve(H, spec)
            except RuntimeError:
                expected = {"root": dict(v.THROWS)}
            inputs = {"settings": st}
            inputs.update(spec)
            out.append({"name": name, "inputs": inputs, "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    for c in out:
        refused = c["name"].startswith("refused: ") or c["name"] in (
            "step: p5 with 3 evaluations", "step: lorentz from 5")
        assert (c["expected"]["root"] == v.THROWS) == refused, c["name"]
    # The solver returns a root it hasn't evaluated.
    assert sum(1 for x in by_name.values() if "lastArgument" in x and x["lastArgument"] != x["root"]) > 50
    assert by_name["step: p1 from its root"] == {"root": 1.0, "calls": 1, "lastArgument": 1.0}


if __name__ == "__main__":
    v.main(__file__, "ql/math/solvers1d/finitedifferencenewtonsafe.hpp", cases)
