# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for ProjectedCostFunction (ql/math/optimization/projectedcostfunction.hpp, .cpp), which no wheel class
exposes, each output computed by the C++ harness (docs/porting-playbook.md §9.4): 10 projections of the harness's
named cost functions (tools/cxx/src/math/optimization/specs.hpp; rosenbrock, polynomialFit, beale and linear over one,
five and six variables), with QuantLib's default flags (all free), all free given explicitly, and mixes with at least
one free parameter, each built by both of QuantLib's constructors, from the values and the flags and from a
Projection on them, whose state it copies.

- "projected" cases, one per projection and constructor: value and values at four vectors of free parameters drawn
  at random (the seed), a NaN among them, which map the free parameters over the fixed values and call the cost
  function; project at three vectors of every parameter, include at three of the free ones; and
  finiteDifferenceEpsilon, CostFunction's, which the class doesn't override.
- "refusal" cases: each check in QuantLib's order: the projection's (flags of another size than the values, no free
  parameter), value's and values' (free parameters of another size, mapFreeParameters' text), project's and
  include's (vectors of another size), by each constructor.

A refused value is {"throws": true}. Each case's inputs are "constructor" ("values" or "projection"), "costFunction"
(a named cost function's spec), "parameterValues" and "fixParameters" (an empty list for QuantLib's default), and the
vectors each member reads. tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/ProjectedCostFunctionCxxVectorTests.cs
reads the file back, with the same cost functions (OptimizationSpecs.cs)."""
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

CONSTRUCTORS = ["values", "projection"]
VECTORS = 4
FULL_VECTORS = 3

# (cost function, parameter values, flags)
PROJECTIONS = [
    ("rosenbrock", [1.2, -0.7], []),
    ("rosenbrock", [1.2, -0.7], [False, True]),
    ("rosenbrock", [1.2, -0.7], [True, False]),
    ("polynomialFit", [0.5, -1.0, 2.0], [False, False, False]),
    ("polynomialFit", [0.5, -1.0, 2.0], [True, False, True]),
    ("polynomialFit", [0.5, -1.0, 2.0], [False, True, True]),
    ("beale", [3.0, 0.5], [False, True]),
    ("linear", [1.0, 2.0, 3.0, 4.0, 5.0], [False, True, False, True, False]),
    ("linear", [0.1, 0.2, 0.3, 0.4, 0.5, 0.6], [True, True, True, True, True, False]),
    ("linear", [2.0], [False]),
]
# (name, cost function, parameter values, flags, member, its vector)
REFUSALS = [
    ("flags of another size", "polynomialFit", [0.5, -1.0, 2.0], [False, True], "value", [0.1]),
    ("every parameter fixed", "rosenbrock", [1.2, -0.7], [True, True], "value", []),
    ("value of too many free parameters", "polynomialFit", [0.5, -1.0, 2.0], [True, False, True], "value", [0.1, 0.2]),
    ("value of none", "rosenbrock", [1.2, -0.7], [], "value", []),
    ("values of too few free parameters", "linear", [1.0, 2.0, 3.0], [False, False, True], "values", [0.1]),
    ("project of too few parameters", "beale", [3.0, 0.5], [False, True], "project", [3.0]),
    ("project of the free parameters only", "polynomialFit", [0.5, -1.0, 2.0], [True, False, True], "project", [0.1]),
    ("include of every parameter", "rosenbrock", [1.2, -0.7], [True, False], "include", [1.0, 2.0]),
    ("include of none", "linear", [1.0, 2.0], [False, False], "include", []),
]


def projected_case(H, rnd, st, k, constructor, function, values, flags):
    spec = {"type": function}
    n = len(values)
    free = n if not flags else flags.count(False)
    frees = [[rnd.uniform(-2.0, 2.0) for _ in range(free)] for _ in range(VECTORS)]
    frees[-1][0] = math.nan
    fulls = [[rnd.uniform(-2.0, 2.0) for _ in range(n)] for _ in range(FULL_VECTORS)]
    includes = [[rnd.uniform(-2.0, 2.0) for _ in range(free)] for _ in range(FULL_VECTORS)]

    def call(member, x):
        return v.attempt(lambda: H.call("ProjectedCostFunction::" + member, constructor, spec, values, flags, x))

    return {"name": f"{function} {values} flags {flags} by {constructor} / {k}",
            "inputs": {"settings": st, "kind": "projected", "constructor": constructor, "costFunction": spec,
                       "parameterValues": values, "fixParameters": flags, "freeParameters": v.enc(frees),
                       "parameters": fulls, "projectedParameters": includes},
            "expected": {"value": [call("value", x) for x in frees],
                         "values": [call("values", x) for x in frees],
                         "project": [call("project", x) for x in fulls],
                         "include": [call("include", x) for x in includes],
                         "finiteDifferenceEpsilon": v.attempt(lambda: H.call(
                             "ProjectedCostFunction::finiteDifferenceEpsilon", constructor, spec, values, flags))}}


def refusal_case(H, st, constructor, name, function, values, flags, member, x):
    spec = {"type": function}
    return {"name": f"refusal {name} by {constructor}",
            "inputs": {"settings": st, "kind": "refusal", "constructor": constructor, "costFunction": spec,
                       "parameterValues": values, "fixParameters": flags, "member": member, "x": x},
            "expected": {"result": v.attempt(lambda: H.call("ProjectedCostFunction::" + member, constructor, spec,
                                                            values, flags, x))}}


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 19, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []
        for k, (function, values, flags) in enumerate(PROJECTIONS):
            for constructor in CONSTRUCTORS:
                out.append(projected_case(H, rnd, st, k, constructor, function, values, flags))
        for constructor in CONSTRUCTORS:
            for row in REFUSALS:
                out.append(refusal_case(H, st, constructor, *row))
        for c in out:
            if c["inputs"]["kind"] == "refusal":
                assert c["expected"]["result"] == v.THROWS, c["name"]
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/projectedcostfunction.hpp", cases)
