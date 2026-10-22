# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ConjugateGradient (ql/math/optimization/conjugategradient.hpp, .cpp), with its default
ArmijoLineSearch (armijo.hpp, .cpp) and LineSearchBasedMethod's minimize (linesearchbasedmethod.hpp, .cpp), through
the wheel's Optimizer.solve: simplex.py's cost functions of one to three variables, each from its starts, under
NoConstraint, with end criteria the method meets by a tight and by a loose function tolerance. Each case records the
point the minimization ends at and the number of calls of the cost function.

The point is the one QuantLib's line search last accepted, read from the cost function's trace: the last point the
function is called at whose every later call is one of the gradient's central differences around it (the point
with one coordinate moved by the finite-difference step, 1e-8): the rule holds where every line search succeeds,
which the criteria's 1,000 iterations, the Armijo search's limit too, ensure. The method's iteration limit and a
failed search, where the trace can't tell the exit apart, are Divergences/LineSearchReturnTests.cs'. QuantLib's
minimize returns on the function tolerance or the iteration limit before it sets the problem's current value, so the
wheel returns the point before; the port sets it first (Divergences/LineSearchReturnTests.cs). Each case asserts the
trace's point is one the function was called at, and that the wheel's returned point is that point or one the function
was called at before it.

The calls are the Python function's: Optimizer.solve's PyCostFunction calls it in value(), and QuantLib's
CostFunction::gradient, by central differences, through value(). The cost functions are
tests/NanoQL.Tests/Vectors/Numerics/Optimization/SimplexCostFunctions.cs; tests/NanoQL.Tests/Vectors/Numerics/
Optimization/ConjugateGradientVectorTests.cs reads the file back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import QuantLib as ql  # noqa: E402

import simplex  # noqa: E402
import vectors as v  # noqa: E402

EPS = 1e-8  # CostFunction::finiteDifferenceEpsilon
# (maxIterations, maxStationaryStateIterations, rootEpsilon, functionEpsilon, gradientNormEpsilon)
CRITERIA = [(1000, 100, 1e-8, 1e-8, 1e-8), (1000, 100, 1e-8, 0.5, 1e-8)]


class Traced:
    """The cost function, recording every point it is called at."""

    def __init__(self, f):
        self.f = f
        self.points = []

    def __call__(self, x):
        self.points.append(tuple(x))
        return self.f(x)


def perturbation(p, q):
    """Whether q is p with one coordinate moved by at most twice the finite-difference step."""
    moved = [i for i in range(len(p)) if p[i] != q[i]]
    return len(moved) == 1 and abs(p[moved[0]] - q[moved[0]]) <= 2.5 * EPS * max(1.0, abs(p[moved[0]]))


def accepted(points):
    """The index of the last point whose later calls are all its gradient's central differences."""
    for k in range(len(points) - 1, -1, -1):
        if all(perturbation(points[k], q) for q in points[k + 1:]) and len(points) - k - 1 >= 2 * len(points[k]):
            return k
    raise AssertionError("no point the trace ends on")


def cases():
    st = v.settings(ql.Date(22, ql.October, 2026))
    out = []
    for name, (f, starts) in simplex.COSTS.items():
        for i, start in enumerate(starts):
            for c, criteria in enumerate(CRITERIA):
                traced = Traced(f)
                returned = list(ql.Optimizer().solve(traced, ql.NoConstraint(), ql.ConjugateGradient(),
                                                     ql.EndCriteria(*criteria), ql.Array(start)))
                k = accepted(traced.points)
                x = list(traced.points[k])
                assert tuple(returned) in traced.points[:k + 1], (name, i, c)
                out.append({"name": f"{name} start {i} criteria {c}",
                            "inputs": {"settings": st, "costFunction": name, "initialValue": start,
                                       "endCriteria": simplex.criteria_input(criteria)},
                            "expected": {"x": x, "calls": len(traced.points)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/conjugategradient.hpp", cases)
