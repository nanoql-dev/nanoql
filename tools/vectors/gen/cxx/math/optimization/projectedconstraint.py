# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for ProjectedConstraint (ql/math/optimization/projectedconstraint.hpp), which no wheel class exposes, each
output computed by the C++ harness (docs/porting-playbook.md §9.4): test, upperBound and lowerBound for the five
constraints of math/optimization/constraint.py's harness counterpart (NoConstraint, PositiveConstraint,
BoundaryConstraint(-1, 2), CompositeConstraint of PositiveConstraint and BoundaryConstraint(-1, 2), and
NonhomogeneousBoundaryConstraint([-1, 0, 0.5], [1, 2, 3])), each projected by three projections of three parameters
(the first fixed, the last two fixed, none fixed by QuantLib's default flags), built by both constructors (form
"values": the constraint, the values and the flags; form "projection": the constraint and a Projection of them), at
eight free-parameter vectors: six drawn around the constraint's region, and two on and outside its bounds, a NaN among
them; then the refusals, which the projection raises: free parameters of another size, flags of another size, and no
free parameter.

A constraint is a spec (tools/cxx/src/math/optimization/specs.hpp). Each case's inputs hold the form, the constraint,
the projection's values and flags, and the free-parameter vectors. tests/NanoQL.Tests/Vectors/Cxx/Numerics/
Optimization/ProjectedConstraintCxxVectorTests.cs reads the file back."""
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

BOX = {"type": "BoundaryConstraint", "low": -1.0, "high": 2.0}
POSITIVE = {"type": "PositiveConstraint"}
CONSTRAINTS = [
    ("NoConstraint", {"type": "NoConstraint"}),
    ("PositiveConstraint", POSITIVE),
    ("BoundaryConstraint", BOX),
    ("CompositeConstraint", {"type": "CompositeConstraint", "c1": POSITIVE, "c2": BOX}),
    ("NonhomogeneousBoundaryConstraint",
     {"type": "NonhomogeneousBoundaryConstraint", "low": [-1.0, 0.0, 0.5], "high": [1.0, 2.0, 3.0]}),
]
# The projections: (name, the parameters' values, the flags), each value inside every constraint's region.
PROJECTIONS = [
    ("first fixed", [0.5, 1.0, 1.5], [True, False, False]),
    ("last two fixed", [0.25, 1.5, 2.0], [False, True, True]),
    ("none fixed, default flags", [0.75, 0.5, 1.0], []),
]
FORMS = ["values", "projection"]
VECTORS = 8


def free_vectors(rnd, free):
    edges = [[2.0] * free, [-1.5] + [0.5] * (free - 1)]
    edges[1][0] = math.nan if free > 1 else -1.5
    return [[rnd.uniform(-1.5, 2.5) for _ in range(free)] for _ in range(VECTORS - len(edges))] + edges


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        for cname, spec in CONSTRAINTS:
            for pname, values, flags in PROJECTIONS:
                free = len(values) if not flags else flags.count(False)
                params = free_vectors(rnd, free)
                for form in FORMS:
                    args = [form, spec, values, flags]
                    out.append({"name": f"{cname} / {pname} / {form}",
                                "inputs": {"settings": st, "form": form, "constraint": spec,
                                           "parameterValues": values, "fixParameters": flags,
                                           "params": v.enc(params)},
                                "expected": {
                                    "test": [call("ProjectedConstraint::test", *args, p) for p in params],
                                    "upperBound": [call("ProjectedConstraint::upperBound", *args, p) for p in params],
                                    "lowerBound": [call("ProjectedConstraint::lowerBound", *args, p) for p in params]}})

        refusals = [
            ("free parameters of another size", "ProjectedConstraint::test",
             ["values", BOX, [0.5, 1.0, 1.5], [True, False, False], [0.5]]),
            ("free parameters of another size, upper bound", "ProjectedConstraint::upperBound",
             ["projection", BOX, [0.5, 1.0, 1.5], [True, False, False], [0.5, 0.5, 0.5]]),
            ("flags of another size", "ProjectedConstraint::lowerBound",
             ["values", BOX, [0.5, 1.0, 1.5], [True, False], [0.5, 0.5]]),
            ("no free parameter", "ProjectedConstraint::test",
             ["projection", POSITIVE, [0.5, 1.0], [True, True], []]),
        ]
        for name, method, params in refusals:
            out.append({"name": "refused / " + name,
                        "inputs": {"settings": st, "method": method, "params": v.enc(params)},
                        "expected": {"result": call(method, *params)}})
        for c in out[-len(refusals):]:
            assert c["expected"]["result"] == v.THROWS, c["name"]
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/projectedconstraint.hpp", cases)
