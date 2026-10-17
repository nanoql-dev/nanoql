# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the Simplex optimization method (ql/math/optimization/simplex.hpp, .cpp), through the wheel's
Optimizer.solve: five scalar cost functions of one to three variables (COSTS below), each from six starts and with
three lambdas, under NoConstraint; the sixth start runs with an iteration limit the method reaches, the others with
end criteria it meets by the simplex's size. Each case records the minimum Simplex returns, the number of calls of
the cost function, which is the Problem's function-evaluation count, since Simplex evaluates the cost only through
Problem::value, and the method's lambda (the wheel's getLambda, Simplex::lambda).

The cost functions are written the same way in
tests/NanoQL.Tests/Vectors/Numerics/Optimization/SimplexCostFunctions.cs, which
tests/NanoQL.Tests/Vectors/Numerics/Optimization/SimplexVectorTests.cs uses to read the file back. The wheel's
Optimizer.solve(function, constraint, method, endCriteria, initialValue) is QuantLib-SWIG's: it wraps the Python
function in a PyCostFunction, whose value() calls it with the point as a tuple (its values() throws, and Simplex
never calls it), builds Problem(costFunction, constraint, initialValue), calls method.minimize(problem, endCriteria)
and returns problem.currentValue(). Simplex(lambda) and EndCriteria are the C++ classes, with no logic of their
own."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def quadratic2(x):
    return (x[0] - 1.0) * (x[0] - 1.0) + (x[1] + 0.5) * (x[1] + 0.5)


def rosenbrock(x):
    a = x[1] - x[0] * x[0]
    b = 1.0 - x[0]
    return 100.0 * a * a + b * b


def parabola(x):
    return x[0] * x[0] + x[0] + 1.0


def booth(x):
    a = x[0] + 2.0 * x[1] - 7.0
    b = 2.0 * x[0] + x[1] - 5.0
    return a * a + b * b


def quadratic3(x):
    return x[0] * x[0] + 2.0 * x[1] * x[1] + 3.0 * x[2] * x[2] + x[0] * x[1] - x[2]


# Each function with its six starts.
COSTS = {
    "quadratic2": (quadratic2, [[3.0, 2.0], [0.0, 0.0], [-2.5, 4.0], [1.0, -0.5], [10.0, -10.0], [0.3, 0.7]]),
    "rosenbrock": (rosenbrock, [[-1.2, 1.0], [0.0, 0.0], [2.0, 2.0], [-0.5, 0.5], [1.5, -1.0], [0.8, 0.6]]),
    "parabola": (parabola, [[-100.0], [0.0], [5.0], [-0.5], [1e-3], [42.0]]),
    "booth": (booth, [[0.0, 0.0], [1.0, 3.0], [-4.0, 6.0], [5.0, 5.0], [2.5, -1.5], [-10.0, 10.0]]),
    "quadratic3": (quadratic3, [[1.0, 1.0, 1.0], [0.0, 0.0, 0.0], [-2.0, 3.0, 0.5], [5.0, -5.0, 2.0],
                                [0.1, -0.2, 0.3], [4.0, 4.0, -4.0]]),
}
LAMBDAS = [0.1, 0.5, 1.0]
# (maxIterations, maxStationaryStateIterations, rootEpsilon, functionEpsilon, gradientNormEpsilon)
CRITERIA = (1000, 100, 1e-8, 1e-8, 1e-8)
LIMITED = (30, 10, 1e-8, 1e-8, 1e-8)


class Counted:
    """The cost function, counting its calls."""

    def __init__(self, f):
        self.f = f
        self.calls = 0

    def __call__(self, x):
        self.calls += 1
        return self.f(x)


def minimize(f, constraint, lam, criteria, start):
    """Optimizer.solve's minimum and the calls of the cost function, or a throw and the calls before it."""
    counted = Counted(f)
    x = v.attempt(lambda: list(ql.Optimizer().solve(counted, constraint, ql.Simplex(lam), ql.EndCriteria(*criteria),
                                                    ql.Array(start))))
    return x, counted.calls


def criteria_input(criteria):
    keys = ("maxIterations", "maxStationaryStateIterations", "rootEpsilon", "functionEpsilon", "gradientNormEpsilon")
    return dict(zip(keys, criteria))


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, (f, starts) in COSTS.items():
        for i, start in enumerate(starts):
            criteria = LIMITED if i == len(starts) - 1 else CRITERIA
            for lam in LAMBDAS:
                x, calls = minimize(f, ql.NoConstraint(), lam, criteria, start)
                out.append({"name": f"{name} start {i} lambda {lam!r}",
                            "inputs": {"settings": st, "costFunction": name, "initialValue": start, "lambda": lam,
                                       "endCriteria": criteria_input(criteria)},
                            "expected": {"x": x, "calls": calls, "lambda": ql.Simplex(lam).getLambda()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/simplex.hpp", cases)
