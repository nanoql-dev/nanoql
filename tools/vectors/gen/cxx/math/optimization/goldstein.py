# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Goldstein and Price's line search (ql/math/optimization/goldstein.hpp, .cpp), through the minimizations
of the three line-search-based methods that run on it (conjugate gradient, steepest descent and BFGS), each output
computed by the C++ harness, since the wheel binds no cost function (docs/porting-playbook.md §9.4).

QuantLib's test-suite functions (tools/cxx/src/math/optimization/specs.hpp): the extended Rosenbrock in 2 and 5
variables and a weighted quadratic, with their analytic gradients, and the harness's rosenbrock, by central differences;
each method under QuantLib's default line search (eps 1e-8, alpha 0.05, beta 0.65, extrapolation 1.5) and two others,
under end criteria the method meets or a limit of 1,000 iterations, and under a limit of 5. Each case records the point
the line search last accepted and the Problem after minimize (function value, gradient norm, the evaluation counts) and
the end criterion; QuantLib's own current value, the point before where it returns on the function tolerance or the
iteration limit, is left out (tests/NanoQL.Tests/Divergences/LineSearchReturnTests.cs). The harness unit is
tools/cxx/src/math/optimization/goldstein.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/GoldsteinLineSearchCxxVectorTests.cs."""
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
PROBLEMS = [
    ("extended Rosenbrock 2", {"type": "extendedRosenbrock"}, [-1.2, 1.0]),
    ("extended Rosenbrock 5", {"type": "extendedRosenbrock"}, [-1.0] * 5),
    ("weighted quadratic", {"type": "weightedQuadratic", "center": [3.0, -2.0, 0.5], "weight": [1.0, 4.0, 0.25]},
     [0.0, 0.0, 0.0]),
    ("rosenbrock", {"type": "rosenbrock"}, [-1.2, 1.0]),
]
SEARCHES = [("default", (1e-8, 0.05, 0.65, 1.5)), ("wide", (1e-6, 0.1, 0.9, 2.0)), ("narrow", (1e-8, 0.01, 0.5, 1.2))]
METHODS = ["ConjugateGradient", "SteepestDescent", "BFGS"]
MET = {"maxIterations": 1000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
       "gradientNormEpsilon": 1e-8}
LIMITED = {**MET, "maxIterations": 5, "maxStationaryStateIterations": 2}


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for method in METHODS:
            for label, function, start in PROBLEMS:
                for search, (eps, alpha, beta, extrapolation) in SEARCHES:
                    for tag, criteria in (("met", MET), ("limited", LIMITED)):
                        r = H.call("GoldsteinLineSearch::minimize", method, function, NONE, start, criteria, eps,
                                   alpha, beta, extrapolation)
                        out.append({"name": f"{method} {label} {search} {tag}",
                                    "inputs": {"settings": st, "method": method, "costFunction": function,
                                               "constraint": NONE, "initialValue": start, "endCriteria": criteria,
                                               "eps": eps, "alpha": alpha, "beta": beta,
                                               "extrapolation": extrapolation},
                                    "expected": {k: v.enc(r[k]) for k in FIELDS}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/goldstein.hpp", cases)
