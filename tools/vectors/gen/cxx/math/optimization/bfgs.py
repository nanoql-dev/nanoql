# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the BFGS method (ql/math/optimization/bfgs.hpp, .cpp) on its default Armijo line search, each output
computed by the C++ harness on a new BFGS (its inverse Hessian the identity at the start), since the wheel binds no cost
function for it to minimize (docs/porting-playbook.md §9.4).

QuantLib's test-suite functions (tools/cxx/src/math/optimization/specs.hpp): the extended Rosenbrock in 2, 10 and 20
variables, weighted quadratics of condition 1, 1e3 and 1e6, with their analytic gradients; a quadratic without one, so
the central-difference gradient; and the harness's rosenbrock and polynomialFit, whose values are their values' root
mean square; each from two starts, under end criteria the method meets or a limit of 1,000 iterations, and under a limit
of 5; a quadratic in a box. Each case records the point the line search last accepted and the Problem after minimize
(function value, gradient norm, the evaluation counts) and the end criterion; QuantLib's own current value, the point
before where it returns on the function tolerance or the iteration limit, is left out (the S-sr plan's SSR6 (xvii);
tests/NanoQL.Tests/Divergences/LineSearchReturnTests.cs). The harness unit is tools/cxx/src/math/optimization/bfgs.cpp.
Read back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/BFGSCxxVectorTests.cs."""
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


def conditioned(c):
    return {"type": "weightedQuadratic", "center": [1.0, 1.0, 1.0, 1.0],
            "weight": [c ** (i / 3.0) for i in range(4)]}


PROBLEMS = [
    ("extended Rosenbrock 2", {"type": "extendedRosenbrock"}, [[-1.2, 1.0], [-1.0, -1.0]]),
    ("extended Rosenbrock 10", {"type": "extendedRosenbrock"}, [[-1.0] * 10, [0.5] * 10]),
    ("extended Rosenbrock 20", {"type": "extendedRosenbrock"}, [[-1.0] * 20, [0.0] * 20]),
    ("weighted quadratic", {"type": "weightedQuadratic", **QUADRATIC}, [[0.0, 0.0, 0.0], [5.0, 5.0, 5.0]]),
    ("quadratic of condition 1e3", conditioned(1e3), [[0.0] * 4, [-3.0, 2.0, -1.0, 4.0]]),
    ("quadratic of condition 1e6", conditioned(1e6), [[0.0] * 4, [-3.0, 2.0, -1.0, 4.0]]),
    ("weighted quadratic by differences", {"type": "weightedQuadraticValueOnly", **QUADRATIC},
     [[0.0, 0.0, 0.0], [5.0, 5.0, 5.0]]),
    ("rosenbrock", {"type": "rosenbrock"}, [[-1.2, 1.0], [2.0, 2.0]]),
    ("polynomialFit", {"type": "polynomialFit"}, [[0.0, 0.0, 0.0], [1.0, 1.0, 1.0]]),
]
MET = {"maxIterations": 1000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
       "gradientNormEpsilon": 1e-8}
LIMITED = {**MET, "maxIterations": 5, "maxStationaryStateIterations": 2}


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def case(name, function, constraint, start, criteria):
            r = H.call("BFGS::minimize", function, constraint, start, criteria)
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
    v.main(__file__, "ql/math/optimization/bfgs.hpp", cases)
