# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for simulated annealing (ql/math/optimization/simulatedannealing.hpp), SimulatedAnnealing over QuantLib's
Mersenne Twister, each output computed by the C++ harness on a new optimizer over a generator from the seed, since the
wheel binds neither the class nor a cost function for it (docs/porting-playbook.md §9.4).

Both temperature schemes, the constant factor (the temperature reduced by 1 - epsilon every m moves) and the constant
budget (the initial temperature times (1 - k/K)^alpha after k moves, 0 past K), on the extended Rosenbrock in 2
variables, Griewangk's function in 3, and a weighted quadratic in 3, the last also in a box; two seeds each; under end
criteria the simplex's size meets and under a limit of 50 iterations. Each case records the Problem after minimize (the
best point and value, the gradient norm, the evaluation counts) and the end criterion. No vertex here is NaN, where
nanoql guards the initial vertices' values (the S-core plan's CR7 (xix)). The harness unit is
tools/cxx/src/math/optimization/simulatedannealing.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/SimulatedAnnealingCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

FIELDS = ("currentValue", "functionValue", "gradientNormValue", "functionEvaluation", "gradientEvaluation",
          "endCriteriaType")
NONE = {"type": "NoConstraint"}
QUADRATIC = {"type": "weightedQuadratic", "center": [3.0, -2.0, 0.5], "weight": [1.0, 4.0, 0.25]}
PROBLEMS = [
    ("Rosenbrock", {"type": "extendedRosenbrock"}, NONE, [-1.2, 1.0]),
    ("Griewangk", {"type": "griewangk"}, NONE, [100.0, -50.0, 25.0]),
    ("quadratic", QUADRATIC, NONE, [0.0, 0.0, 0.0]),
    ("quadratic in a box", QUADRATIC, {"type": "BoundaryConstraint", "low": -1.0, "high": 1.0}, [0.0, 0.0, 0.0]),
]
SCHEMES = [
    ("constant factor", {"type": "ConstantFactor", "lambda": 0.5, "T0": 10.0, "epsilon": 0.05, "m": 20}),
    ("constant budget", {"type": "ConstantBudget", "lambda": 0.5, "T0": 10.0, "K": 2000, "alpha": 4.0}),
]
MET = {"maxIterations": 5000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
       "gradientNormEpsilon": None}
LIMITED = {**MET, "maxIterations": 50, "maxStationaryStateIterations": 10}


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for label, function, constraint, start in PROBLEMS:
            for scheme_label, scheme in SCHEMES:
                for seed in (1, 42):
                    for tag, crit in (("met", MET), ("limited", LIMITED)):
                        r = H.call("SimulatedAnnealing::minimize", function, constraint, start, crit, scheme, seed)
                        out.append({"name": f"{label}, {scheme_label}, seed {seed}, {tag}",
                                    "inputs": {"settings": st, "costFunction": function, "constraint": constraint,
                                               "initialValue": start, "endCriteria": crit, "scheme": scheme,
                                               "seed": seed},
                                    "expected": {k: v.enc(r[k]) for k in FIELDS}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/simulatedannealing.hpp", cases)
