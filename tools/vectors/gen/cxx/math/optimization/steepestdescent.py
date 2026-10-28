# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the steepest-descent method (ql/math/optimization/steepestdescent.hpp, .cpp) on its default Armijo line
search, each output computed by the C++ harness, since the wheel binds no cost function for it to minimize
(docs/porting-playbook.md §9.4).

QuantLib's test-suite functions (tools/cxx/src/math/optimization/specs.hpp): the extended Rosenbrock in 2 and 5
variables and a weighted quadratic, with their analytic gradients; the same quadratic without one, so the
central-difference gradient; and the harness's rosenbrock, whose value is its values' root mean square; each from two
starts, under end criteria the method meets or a limit of 1,000 iterations, and under a limit of 5; the quadratic in a
box. Each case records the point the line search last accepted and the Problem after minimize (function value, gradient
norm, the evaluation counts) and the end criterion; QuantLib's own current value, the point before where it returns on
the function tolerance or the iteration limit, is left out (the S-sr plan's SSR6 (xvii);
tests/NanoQL.Tests/Divergences/LineSearchReturnTests.cs). The harness unit is
tools/cxx/src/math/optimization/steepestdescent.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/SteepestDescentCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

FIELDS = ("lastAcceptedPoint", "functionValue", "gradientNormValue", "functionEvaluation", "gradientEvaluation",
          "endCriteriaType")
NONE = {"type": "NoConstraint"}
QUADRATIC = {"center": [3.0, -2.0, 0.5], "weight": [1.0, 4.0, 0.25]}
PROBLEMS = [
    ("extended Rosenbrock 2", {"type": "extendedRosenbrock"}, [[-1.2, 1.0], [0.5, 0.5]]),
    ("extended Rosenbrock 5", {"type": "extendedRosenbrock"}, [[-1.0] * 5, [0.0, 0.5, 1.0, 1.5, 2.0]]),
    ("weighted quadratic", {"type": "weightedQuadratic", **QUADRATIC}, [[0.0, 0.0, 0.0], [5.0, 5.0, 5.0]]),
    ("weighted quadratic by differences", {"type": "weightedQuadraticValueOnly", **QUADRATIC},
     [[0.0, 0.0, 0.0], [5.0, 5.0, 5.0]]),
    ("rosenbrock", {"type": "rosenbrock"}, [[-1.2, 1.0], [2.0, 2.0]]),
]
MET = {"maxIterations": 1000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
       "gradientNormEpsilon": 1e-8}
LIMITED = {**MET, "maxIterations": 5, "maxStationaryStateIterations": 2}


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def case(name, function, constraint, start, criteria):
            r = H.call("SteepestDescent::minimize", function, constraint, start, criteria)
            return {"name": name,
                    "inputs": {"settings": st, "costFunction": function, "constraint": constraint,
                               "initialValue": start, "endCriteria": criteria},
                    "expected": {k: v.enc(r[k]) for k in FIELDS}}

        out = []
        for label, function, starts in PROBLEMS:
            for i, start in enumerate(starts):
                for tag, criteria in (("met", MET), ("limited", LIMITED)):
                    out.append(case(f"{label} start {i} {tag}", function, NONE, start, criteria))
        box = {"type": "BoundaryConstraint", "low": -1.0, "high": 1.0}
        out.append(case("weighted quadratic in a box", {"type": "weightedQuadratic", **QUADRATIC}, box,
                        [0.0, 0.0, 0.0], MET))
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/steepestdescent.hpp", cases)
