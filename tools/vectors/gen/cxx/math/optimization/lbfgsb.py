# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for L-BFGS-B (ql/math/optimization/lbfgsb.hpp, .cpp), each output computed by the C++ harness, since the
wheel binds no cost function for it to minimize (docs/porting-playbook.md §9.4).

QuantLib's test problems (test-suite/optimizers.cpp's testLBFGSB, testLBFGSBActiveBounds and testLBFGSBCoverage, its
functions in tools/cxx/src/math/optimization/specs.hpp): the extended Rosenbrock unconstrained in 2, 10 and 20 variables
(memory 10, and 3 below the dimension) and in a box; the weighted quadratic with its minimum inside the box, on its
bounds, at a corner, through two breakpoints, in one variable, with a coordinate pinned (lower equals upper), from a
start outside the box, and by central differences; each through the constraint's bounds and through the constructor's
explicit ones, under QuantLib's test's end criteria and under a limit of 3 iterations. Each case records the Problem
after minimize (current value, function value, gradient norm, the evaluation counts) and the end criterion; where the
end criterion is the stationary function value, the gradient norm is left out: on the relative reduction stop nanoql
reports the projected gradient at the point returned, QuantLib the previous iterate's
(tests/NanoQL.Tests/Divergences/LBFGSBTests.cs). The harness unit is tools/cxx/src/math/optimization/lbfgsb.cpp. Read
back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/LBFGSBCxxVectorTests.cs."""
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
STATIONARY_FUNCTION_VALUE = 3
EPS = 2.220446049250313e-16
NONE = {"type": "NoConstraint"}
CRITERIA = {"maxIterations": 1000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-12,
            "functionEpsilon": 1e-12, "gradientNormEpsilon": 1e-10}
LIMITED = {**CRITERIA, "maxIterations": 3, "maxStationaryStateIterations": 2}


def box(lo, hi):
    return {"type": "NonhomogeneousBoundaryConstraint", "low": lo, "high": hi}


def quadratic(center, weight, value_only=False):
    return {"type": "weightedQuadraticValueOnly" if value_only else "weightedQuadratic", "center": center,
            "weight": weight}


ROSENBROCK = {"type": "extendedRosenbrock"}
Q = quadratic([3.0, -2.0, 0.5], [1.0, 4.0, 0.25])
TIGHT = (10, 1e-10, 1e1 * EPS)
DEFAULT = (10, 1e-8, 1e7 * EPS)
PROBLEMS = [
    ("Rosenbrock 2", ROSENBROCK, None, [-1.0, -1.0], TIGHT),
    ("Rosenbrock 10", ROSENBROCK, None, [-1.0] * 10, TIGHT),
    ("Rosenbrock 20 memory 3", ROSENBROCK, None, [-1.0] * 20, (3, 1e-8, 1e1 * EPS)),
    ("Rosenbrock 2 defaults", ROSENBROCK, None, [-1.2, 1.0], DEFAULT),
    ("interior quadratic", Q, ([-10.0] * 3, [10.0] * 3), [0.0] * 3, TIGHT),
    ("active-bound quadratic", Q, ([0.0] * 3, [1.0] * 3), [0.5] * 3, DEFAULT),
    ("bounded Rosenbrock", ROSENBROCK, ([-2.0, -2.0], [0.5, 0.5]), [-1.0, -1.0], TIGHT),
    ("corner", quadratic([5.0] * 3, [1.0] * 3), ([0.0] * 3, [1.0] * 3), [0.5] * 3, TIGHT),
    ("two breakpoints", quadratic([10.0, 10.0], [1.0, 100.0]), ([0.0, 0.0], [1.0, 1.0]), [0.9, 0.1], TIGHT),
    ("one variable", quadratic([5.0], [1.0]), ([0.0], [1.0]), [0.5], TIGHT),
    ("pinned coordinate", Q, ([-10.0, -10.0, 0.25], [10.0, 10.0, 0.25]), [0.0] * 3, TIGHT),
    ("infeasible start", Q, ([0.0] * 3, [1.0] * 3), [5.0] * 3, TIGHT),
    ("differences", quadratic([3.0, -2.0, 0.7], [1.0, 4.0, 0.25], True), ([0.0] * 3, [1.0] * 3), [0.5] * 3,
     (10, 1e-6, 1e7 * EPS)),
]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for label, function, bounds, start, (memory, pg_tol, f_tol) in PROBLEMS:
            routes = [("constraint", box(*bounds) if bounds else NONE, None, None)]
            if bounds:
                routes.append(("explicit bounds", NONE, bounds[0], bounds[1]))
            for route, constraint, lower, upper in routes:
                for tag, criteria in (("", CRITERIA), (" limited", LIMITED)):
                    r = H.call("LBFGSB::minimize", function, constraint, start, criteria, memory, pg_tol, f_tol,
                               lower, upper)
                    expected = {k: v.enc(r[k]) for k in FIELDS}
                    if r["endCriteriaType"] == STATIONARY_FUNCTION_VALUE:
                        del expected["gradientNormValue"]
                    out.append({"name": f"{label} through the {route}{tag}",
                                "inputs": {"settings": st, "costFunction": function, "constraint": constraint,
                                           "initialValue": start, "endCriteria": criteria, "memory": memory,
                                           "pgTol": pg_tol, "fTol": f_tol, "lowerBound": lower,
                                           "upperBound": upper},
                                "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/lbfgsb.hpp", cases)
