# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of Constraint (ql/math/optimization/constraint.hpp, .cpp) the wheel doesn't expose, each
output computed by the C++ harness (docs/porting-playbook.md §9.4): test, upperBound, lowerBound and update (the
parameters it moves in place and the step it returns) for NoConstraint, PositiveConstraint, BoundaryConstraint(-1, 2),
CompositeConstraint of PositiveConstraint and BoundaryConstraint(-1, 2), and NonhomogeneousBoundaryConstraint([-1, 0,
0.5], [1, 2, 3]), each at twenty parameter vectors with a direction and a step: fourteen drawn inside the constraint,
then six on and outside the bounds, a NaN element among them; from a start outside a constraint no halving of the step
gets inside, which update refuses.
Then the refusals: bounds of different sizes, parameters of another size than the bounds, a composite's bound of
another size than the parameters, and a direction of another size.

A constraint is a spec (tools/cxx/src/math/optimization/specs.hpp).
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/ConstraintCxxVectorTests.cs reads the file back."""
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
NONHOMOGENEOUS = {"type": "NonhomogeneousBoundaryConstraint", "low": [-1.0, 0.0, 0.5], "high": [1.0, 2.0, 3.0]}
# Each constraint, with the box its random parameters are drawn from: the region it allows, or [-2, 3] for none.
CONSTRAINTS = [
    ("NoConstraint", {"type": "NoConstraint"}, [(-2.0, 3.0)] * 2),
    ("PositiveConstraint", POSITIVE, [(0.0, 3.0)] * 2),
    ("BoundaryConstraint", BOX, [(-1.0, 2.0)] * 2),
    ("CompositeConstraint", {"type": "CompositeConstraint", "c1": POSITIVE, "c2": BOX}, [(0.0, 2.0)] * 2),
    ("NonhomogeneousBoundaryConstraint", NONHOMOGENEOUS, [(-1.0, 1.0), (0.0, 2.0), (0.5, 3.0)]),
]
VECTORS = 20


def parameter_vectors(rnd, region):
    """Twenty vectors: random ones in the region, then the edges, on the bounds and outside them, a NaN among them,
    each with a direction and a step."""
    n = len(region)
    edges = [[-1.0] * n, [2.0] * n, [0.0] * n, [1.0] + [0.5] * (n - 1), [math.nan] + [0.5] * (n - 1),
             [-1.5] * n]
    params = [[rnd.uniform(lo, hi) for lo, hi in region] for _ in range(VECTORS - len(edges))] + edges
    directions = [[rnd.uniform(-1.0, 1.0) for _ in range(n)] for _ in range(VECTORS)]
    betas = [rnd.uniform(0.1, 3.0) for _ in range(VECTORS)]
    return params, directions, betas


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        for name, spec, region in CONSTRAINTS:
            params, directions, betas = parameter_vectors(rnd, region)
            updates = [v.attempt(lambda p=p, d=d, b=b: H.call("Constraint::update", spec, p, d, b))
                       for p, d, b in zip(params, directions, betas)]
            out.append({"name": name,
                        "inputs": {"settings": st, "constraint": spec, "params": v.enc(params),
                                   "directions": directions, "betas": betas},
                        "expected": {"test": [call("Constraint::test", spec, p) for p in params],
                                     "upperBound": [call("Constraint::upperBound", spec, p) for p in params],
                                     "lowerBound": [call("Constraint::lowerBound", spec, p) for p in params],
                                     "updatedParams": [u if "throws" in u else u["params"] for u in updates],
                                     "diff": [u if "throws" in u else u["diff"] for u in updates]}})

        two = {"type": "NonhomogeneousBoundaryConstraint", "low": [0.0, 0.0], "high": [1.0, 1.0]}
        uneven = {"type": "NonhomogeneousBoundaryConstraint", "low": [0.0, 0.0], "high": [1.0]}
        composite = {"type": "CompositeConstraint", "c1": two, "c2": {"type": "NoConstraint"}}
        refusals = [
            ("bounds of different sizes", "Constraint::test", [uneven, [0.5, 0.5]]),
            ("parameters of another size than the bounds", "Constraint::test", [two, [0.5, 0.5, 0.5]]),
            ("a composite's upper bound of another size", "Constraint::upperBound", [composite, [0.5, 0.5, 0.5]]),
            ("a composite's lower bound of another size", "Constraint::lowerBound", [composite, [0.5, 0.5, 0.5]]),
            ("a direction of another size", "Constraint::update", [BOX, [0.5, 0.5], [1.0], 0.5]),
        ]
        for name, method, params in refusals:
            out.append({"name": name, "inputs": {"settings": st, "method": method, "params": v.enc(params)},
                        "expected": {"result": call(method, *params)}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/constraint.hpp", cases)
