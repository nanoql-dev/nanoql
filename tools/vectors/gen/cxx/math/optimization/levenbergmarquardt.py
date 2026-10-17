# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the Levenberg-Marquardt method (ql/math/optimization/levenbergmarquardt.hpp, .cpp) and MINPACK's
lmdif (ql/math/optimization/lmdif.cpp), each output computed by the C++ harness, since the wheel's cost functions
have no values() for it to minimize (docs/porting-playbook.md §9.4).

The harness's named cost functions rosenbrock, polynomialFit and beale (tools/cxx/src/math/optimization/specs.hpp),
each from four starts, with and without the cost function's Jacobian, under end criteria the method meets and under
an iteration limit it reaches (three iterations); then constrained runs, where trial points outside the constraint get
lmdif's penalty, a run with a larger epsfcn, runs with zero tolerances (where MINPACK reports its tolerances too
small), runs of rankDeficient, whose Jacobian has rank 1, where lmpar refuses the Gauss-Newton step and iterates with no
Newton lower bound, runs from a start whose values overflow (the penalty for non-finite values, and the initial
Jacobian in place of a non-finite one), and linear, a function of any number of variables, solved, refused for no
variables and for fewer
functions than variables, and refused when maxIterations * (n + 1) wraps in maxfev's int; with the refusals of a
negative function epsilon, xtol and gtol. Each case records the Problem after minimize (current value, function value,
gradient norm, the evaluation counts) and the end criterion, or {"minimize": throws} where QuantLib throws.

tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/LevenbergMarquardtCxxVectorTests.cs reads the file back, with
the same cost functions (OptimizationSpecs.cs)."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

STARTS = {
    "rosenbrock": [[-1.2, 1.0], [0.0, 0.0], [2.0, 2.0], [-0.5, 0.5]],
    "polynomialFit": [[0.0, 0.0, 0.0], [1.0, 1.0, 1.0], [-2.0, 3.0, 0.5], [10.0, -10.0, 10.0]],
    "beale": [[1.0, 1.0], [0.0, 0.0], [2.0, 0.2], [3.5, 0.6]],
}
MET = {"maxIterations": 1000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
       "gradientNormEpsilon": 1e-8}
LIMITED = {**MET, "maxIterations": 3, "maxStationaryStateIterations": 2}
NONE = {"type": "NoConstraint"}

FIELDS = ("currentValue", "functionValue", "gradientNormValue", "functionEvaluation", "gradientEvaluation",
          "endCriteriaType")


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def case(name, function, constraint, start, criteria, jacobian, epsfcn=1e-8, xtol=1e-8, gtol=1e-8):
            spec = {"type": function}
            try:
                r = H.call("LevenbergMarquardt::minimize", spec, constraint, start, criteria, epsfcn, xtol, gtol,
                           jacobian)
                expected = {k: v.enc(r[k]) for k in FIELDS}
            except RuntimeError:
                expected = {"minimize": dict(v.THROWS)}
            return {"name": name,
                    "inputs": {"settings": st, "costFunction": spec, "constraint": constraint, "initialValue": start,
                               "endCriteria": criteria, "epsfcn": epsfcn, "xtol": xtol, "gtol": gtol,
                               "useCostFunctionsJacobian": jacobian},
                    "expected": expected}

        out = []
        for function, starts in STARTS.items():
            for i, start in enumerate(starts):
                for jacobian in (False, True):
                    for label, criteria in (("met", MET), ("limited", LIMITED)):
                        out.append(case(f"{function} start {i} {'jacobian' if jacobian else 'differences'} {label}",
                                        function, NONE, start, criteria, jacobian))

        box = {"type": "BoundaryConstraint", "low": -1.5, "high": 1.5}
        positive = {"type": "PositiveConstraint"}
        for jacobian in (False, True):
            tag = "jacobian" if jacobian else "differences"
            out.append(case(f"rosenbrock in a box {tag}", "rosenbrock", box, [-1.2, 1.0], MET, jacobian))
            out.append(case(f"beale positive {tag}", "beale", positive, [0.5, 0.5], MET, jacobian))
            out.append(case(f"rosenbrock from outside the constraint {tag}", "rosenbrock", positive, [-1.2, 1.0],
                            MET, jacobian))
        out.append(case("beale epsfcn 1e-4", "beale", NONE, [1.0, 1.0], MET, False, epsfcn=1e-4))
        zero = {**MET, "functionEpsilon": 0.0}
        for function, start, jacobian in (("rosenbrock", [-1.2, 1.0], False), ("beale", [1.0, 1.0], False),
                                          ("polynomialFit", [0.0, 0.0, 0.0], False),
                                          ("polynomialFit", [0.0, 0.0, 0.0], True)):
            out.append(case(f"{function} with zero tolerances {'jacobian' if jacobian else 'differences'}", function,
                            NONE, start, zero, jacobian, xtol=0.0, gtol=0.0))
        # rankDeficient's Jacobian has rank 1. From [0, 0] the first Gauss-Newton step overshoots and is refused; the
        # next, with the smaller step bound, is too long, so lmpar iterates with no Newton lower bound (parl 0).
        for jacobian in (False, True):
            out.append(case(f"rankDeficient start 0 {'jacobian' if jacobian else 'differences'}", "rankDeficient",
                            NONE, [0.0, 0.0], MET, jacobian))
        for jacobian in (False, True):
            out.append(case(f"rosenbrock whose values overflow {'jacobian' if jacobian else 'differences'}",
                            "rosenbrock", NONE, [1e200, 1e200], MET, jacobian))
        # maxIterations * (n + 1) is computed in Size and stored in an int: 2147483647 * 2 leaves -2.
        out.append(case("linear with maxfev wrapping", "linear", NONE, [3.0], {**MET, "maxIterations": 2147483647},
                        False))
        out.append(case("linear in one variable", "linear", NONE, [3.0], MET, False))
        out.append(case("linear in no variables", "linear", NONE, [], MET, False))
        out.append(case("linear in two variables", "linear", NONE, [1.0, 2.0], MET, False))
        out.append(case("negative function epsilon", "rosenbrock", NONE, [-1.2, 1.0], {**MET, "functionEpsilon": -1e-8},
                        False))
        out.append(case("negative xtol", "rosenbrock", NONE, [-1.2, 1.0], MET, False, xtol=-1e-8))
        out.append(case("negative gtol", "rosenbrock", NONE, [-1.2, 1.0], MET, False, gtol=-1e-8))
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/levenbergmarquardt.hpp", cases)
