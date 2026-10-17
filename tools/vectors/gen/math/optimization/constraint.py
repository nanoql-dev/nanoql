# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the five constraints (ql/math/optimization/constraint.hpp, .cpp), through Simplex and the wheel's
Optimizer.solve: under each of NoConstraint, PositiveConstraint, BoundaryConstraint(-1, 0.75), CompositeConstraint
of PositiveConstraint and BoundaryConstraint(-2, 0.8), and NonhomogeneousBoundaryConstraint([0, -1], [0.8, 0]), ten
minimizations of tools/vectors/gen/math/optimization/simplex.py's quadratic2 and booth, whose minima the bounded
constraints cut off, from starts inside and on the boundary, and for each bounded constraint a start outside it, which
Simplex refuses. Each case records the minimum and the calls of the cost function, as simplex.py's do: the constraint
moves the simplex's first vertices (Constraint::update) and refuses the trial points outside it (Constraint::test).

tests/NanoQL.Tests/Vectors/Numerics/Optimization/ConstraintVectorTests.cs reads the file back. The wheel's
constraints are QuantLib-SWIG wrappers of the C++ classes of the same names, with no logic of their own; Optimizer
is simplex.py's."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _dir)
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import simplex  # noqa: E402
import vectors as v  # noqa: E402

# Each constraint: its spec (the input nanoql builds it from) and the wheel's object.
CONSTRAINTS = [
    ({"type": "NoConstraint"}, lambda: ql.NoConstraint()),
    ({"type": "PositiveConstraint"}, lambda: ql.PositiveConstraint()),
    ({"type": "BoundaryConstraint", "low": -1.0, "high": 0.75}, lambda: ql.BoundaryConstraint(-1.0, 0.75)),
    ({"type": "CompositeConstraint", "c1": {"type": "PositiveConstraint"},
      "c2": {"type": "BoundaryConstraint", "low": -2.0, "high": 0.8}},
     lambda: ql.CompositeConstraint(ql.PositiveConstraint(), ql.BoundaryConstraint(-2.0, 0.8))),
    ({"type": "NonhomogeneousBoundaryConstraint", "low": [0.0, -1.0], "high": [0.8, 0.0]},
     lambda: ql.NonhomogeneousBoundaryConstraint(ql.Array([0.0, -1.0]), ql.Array([0.8, 0.0]))),
]

# (cost function, start, lambda) for each constraint: feasible starts for every constraint, then one outside each
# bounded constraint (for NoConstraint, a tenth feasible start).
FEASIBLE = [
    ("quadratic2", [0.5, 0.5], 0.1), ("quadratic2", [0.25, 0.75], 0.5), ("quadratic2", [0.7, 0.1], 1.0),
    ("quadratic2", [0.75, 0.05], 0.1), ("quadratic2", [0.1, 0.7], 0.25), ("booth", [0.5, 0.5], 0.1),
    ("booth", [0.2, 0.4], 0.5), ("booth", [0.6, 0.05], 1.0), ("booth", [0.4, 0.6], 0.3),
]
OUTSIDE = {
    "NoConstraint": ("booth", [3.0, -2.0], 0.1),
    "PositiveConstraint": ("quadratic2", [0.5, -0.5], 0.1),
    "BoundaryConstraint": ("quadratic2", [0.5, 1.5], 0.1),
    "CompositeConstraint": ("booth", [0.9, 0.5], 0.1),
    "NonhomogeneousBoundaryConstraint": ("quadratic2", [0.5, 0.5], 0.1),
}
CRITERIA = (1000, 100, 1e-8, 1e-8, 1e-8)


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for spec, make in CONSTRAINTS:
        # NonhomogeneousBoundaryConstraint's x1 is in [-1, 0]: its feasible starts are the others' with x1 - 1.
        shift = 1.0 if spec["type"] == "NonhomogeneousBoundaryConstraint" else 0.0
        runs = [(f, [s[0], s[1] - shift], lam) for f, s, lam in FEASIBLE] + [OUTSIDE[spec["type"]]]
        for i, (name, start, lam) in enumerate(runs):
            x, calls = simplex.minimize(simplex.COSTS[name][0], make(), lam, CRITERIA, start)
            out.append({"name": f"{spec['type']} {i} {name}",
                        "inputs": {"settings": st, "constraint": spec, "costFunction": name, "initialValue": start,
                                   "lambda": lam, "endCriteria": simplex.criteria_input(CRITERIA)},
                        "expected": {"x": x, "calls": calls}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/constraint.hpp", cases)
