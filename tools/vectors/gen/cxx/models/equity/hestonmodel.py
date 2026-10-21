# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HestonModel::FellerConstraint (ql/models/equity/hestonmodel.hpp), which the wheel doesn't
bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4): Constraint's test, upperBound,
lowerBound and update (the parameters it moves in place and the step it returns) at twenty parameter vectors of a
Heston model's five (theta, kappa, sigma, rho, v0), each with a direction and a step: fourteen drawn across the
constraint's boundary, sigma^2 = 2 kappa theta, then six edges: on the boundary, a null and a negative sigma, a
negative theta, a NaN sigma, and three parameters, the fewest the constraint reads. From a start outside the
constraint no halving of the step gets inside, which update refuses. Fewer than three parameters QuantLib reads past,
which nanoql refuses (tests/NanoQL.Tests/Divergences/FellerConstraintSizeTests.cs); no vector holds them.

The constraint is a spec (tools/cxx/src/math/optimization/specs.hpp), the cases those of
tools/vectors/gen/cxx/math/optimization/constraint.py. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/Equity/HestonModelCxxVectorTests.cs."""
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

FELLER = {"type": "HestonModel::FellerConstraint"}
# The box the random parameters are drawn from: theta, kappa, sigma, rho, v0.
REGION = [(0.01, 0.2), (0.1, 4.0), (0.0, 1.2), (-0.9, 0.9), (0.01, 0.2)]
VECTORS = 20


def parameter_vectors(rnd):
    edges = [[0.02, 1.0, 0.2, -0.5, 0.04], [0.04, 1.5, 0.0, -0.5, 0.04], [0.04, 1.5, -0.1, -0.5, 0.04],
             [-0.04, 1.5, 0.3, -0.5, 0.04], [0.04, 1.5, math.nan, -0.5, 0.04], [0.04, 1.5, 0.3]]
    params = [[rnd.uniform(lo, hi) for lo, hi in REGION] for _ in range(VECTORS - len(edges))] + edges
    directions = [[rnd.uniform(-0.5, 0.5) for _ in p] for p in params]
    betas = [rnd.uniform(0.1, 3.0) for _ in params]
    return params, directions, betas


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        params, directions, betas = parameter_vectors(rnd)
        updates = [v.attempt(lambda p=p, d=d, b=b: H.call("Constraint::update", FELLER, p, d, b))
                   for p, d, b in zip(params, directions, betas)]
        return [{"name": "HestonModel::FellerConstraint",
                 "inputs": {"settings": st, "constraint": FELLER, "params": v.enc(params), "directions": directions,
                            "betas": betas},
                 "expected": {"test": [call("Constraint::test", FELLER, p) for p in params],
                              "upperBound": [call("Constraint::upperBound", FELLER, p) for p in params],
                              "lowerBound": [call("Constraint::lowerBound", FELLER, p) for p in params],
                              "updatedParams": [u if "throws" in u else u["params"] for u in updates],
                              "diff": [u if "throws" in u else u["diff"] for u in updates]}}]


if __name__ == "__main__":
    v.main(__file__, "ql/models/equity/hestonmodel.hpp", cases)
