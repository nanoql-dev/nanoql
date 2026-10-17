# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the optimization Problem (ql/math/optimization/problem.hpp), each output computed by the C++ harness:
what Simplex (ql/math/optimization/simplex.hpp, .cpp) leaves in the Problem it minimizes, which the wheel's
Optimizer.solve doesn't return, and the Problem's own counting members (docs/porting-playbook.md §9.4).

Simplex minimizes the harness's named least-squares cost functions rosenbrock, polynomialFit and beale
(tools/cxx/src/math/optimization/specs.hpp), whose value is CostFunction's default root mean square, under
NoConstraint, from five starts each, with lambda 0.1 under end criteria it meets and with lambda 0.5 under an
iteration limit it reaches (30 iterations): the current value, the function value, the gradient norm (which Simplex
leaves null), the evaluation counts and the end criterion. Then six runs under BoundaryConstraint(-1, 0.75) of the
scalar cost functions quadratic2 and booth, tools/vectors/gen/math/optimization/simplex.py's, from starts on the
box's bound, which end at Simplex's constrained exit (StationaryFunctionValue). Then Problem::value, values, gradient and
valueAndGradient on rosenbrock at two points, each after reset, with the counts it leaves; and reset, after a value, a
gradient and the three setters.

tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/ProblemCxxVectorTests.cs reads the file back, with the same
cost functions (OptimizationSpecs.cs)."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

STARTS = {
    "rosenbrock": [[-1.2, 1.0], [0.0, 0.0], [2.0, 2.0], [-0.5, 0.5], [1.5, -1.0]],
    "polynomialFit": [[0.0, 0.0, 0.0], [1.0, 1.0, 1.0], [-2.0, 3.0, 0.5], [10.0, -10.0, 10.0], [0.5, -0.5, 0.25]],
    "beale": [[1.0, 1.0], [0.0, 0.0], [2.0, 0.2], [3.5, 0.6], [-1.0, 1.5]],
}
MET = {"maxIterations": 1000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
       "gradientNormEpsilon": 1e-8}
LIMITED = {**MET, "maxIterations": 30, "maxStationaryStateIterations": 10}
RUNS = [(0.1, MET), (0.5, LIMITED)]
NONE = {"type": "NoConstraint"}
# Constrained runs that end at Simplex's constrained exit ("can't extrapolate given the constraints"): a start on the
# box's bound degenerates the simplex, whose vertex moved out of the box is updated back onto the start. The first two
# are tools/vectors/gen/math/optimization/constraint.py's "BoundaryConstraint 1 quadratic2" and "BoundaryConstraint 5
# booth", which record only the minimum.
BOX = {"type": "BoundaryConstraint", "low": -1.0, "high": 0.75}
CONSTRAINED = [("quadratic2", [0.25, 0.75], 0.5), ("booth", [0.5, 0.5], 0.1), ("quadratic2", [0.5, 0.75], 0.1),
               ("quadratic2", [0.5, 0.75], 1.0), ("booth", [0.75, 0.5], 0.1), ("booth", [0.5, 0.75], 1.0)]
POINTS = [[1.0, 2.0], [-0.75, 0.3]]

STATE = ("currentValue", "functionValue", "gradientNormValue", "functionEvaluation", "gradientEvaluation")


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for function, starts in STARTS.items():
            spec = {"type": function}
            for i, start in enumerate(starts):
                for lam, criteria in RUNS:
                    r = H.call("Simplex::minimize", spec, NONE, start, criteria, lam)
                    out.append({"name": f"Simplex {function} start {i} lambda {lam!r}",
                                "inputs": {"settings": st, "costFunction": spec, "constraint": NONE,
                                           "initialValue": start, "endCriteria": criteria, "lambda": lam},
                                "expected": {k: v.enc(r[k]) for k in STATE + ("endCriteriaType",)}})
        for function, start, lam in CONSTRAINED:
            spec = {"type": function}
            r = H.call("Simplex::minimize", spec, BOX, start, MET, lam)
            out.append({"name": f"Simplex {function} in a box from {start} lambda {lam!r}",
                        "inputs": {"settings": st, "costFunction": spec, "constraint": BOX, "initialValue": start,
                                   "endCriteria": MET, "lambda": lam},
                        "expected": {k: v.enc(r[k]) for k in STATE + ("endCriteriaType",)}})

        spec = {"type": "rosenbrock"}
        for i, x in enumerate(POINTS):
            expected = {}
            for member in ("value", "values", "gradient", "valueAndGradient"):
                r = H.call(f"Problem::{member}", spec, x)
                result = r["result"]
                if member == "valueAndGradient":
                    expected["valueAndGradient"] = v.enc(result["value"])
                    expected["valueAndGradientGradient"] = v.enc(result["gradient"])
                else:
                    expected[member] = v.enc(result)
                expected[member + "Evaluations"] = [r["functionEvaluation"], r["gradientEvaluation"]]
            out.append({"name": f"Problem members at point {i}",
                        "inputs": {"settings": st, "costFunction": spec, "x": x},
                        "expected": expected})

        current, function_value, gradient_norm = [3.0, 4.0], 1.5, 2.5
        r = H.call("Problem::reset", spec, POINTS[0], current, function_value, gradient_norm)
        out.append({"name": "Problem reset after the setters",
                    "inputs": {"settings": st, "costFunction": spec, "x": POINTS[0], "currentValue": current,
                               "functionValue": function_value, "gradientNormValue": gradient_norm},
                    "expected": {**{"before" + k[0].upper() + k[1:]: v.enc(r["before"][k]) for k in STATE},
                                 **{"after" + k[0].upper() + k[1:]: v.enc(r["after"][k]) for k in STATE}}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/problem.hpp", cases)
