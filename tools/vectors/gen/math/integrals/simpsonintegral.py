# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for SimpsonIntegral (ql/math/integrals/simpsonintegral.hpp, through ql/math/integrals/integral.hpp, .cpp and
the trapezoid's Default refinement of ql/math/integrals/trapezoidintegral.hpp): the integral and the number of
evaluations (numberOfEvaluations() after the call) of seven of eight named integrands (the eighth, x, is
Gauss-Lobatto's) over intervals forward, reversed and empty, at accuracies 1e-6 and 1e-10 and up to 100 refinements; six
or seven refinements allowed, too few for the six QuantLib always takes, which it refuses ("max number of iterations
reached"); and the refusal of an accuracy not above machine epsilon. The integrands, named here and in the readers, are
shared with tools/vectors/gen/math/integrals/gausslobattointegral.py and trapezoidintegral.py. The wheel's class is a
SWIG wrapper of the C++ class, with no logic of its own, calling the Python integrand. Read back by
tests/NanoQL.Tests/Vectors/Numerics/Integrals/SimpsonIntegralVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

INTEGRANDS = {
    "exp(x)": math.exp,
    "sin(x)": math.sin,
    "x x": lambda x: x * x,
    "x x x x": lambda x: x * x * x * x,
    "1/(1+x x)": lambda x: 1.0 / (1.0 + x * x),
    "sqrt(x)": math.sqrt,
    "exp(-x x/2)": lambda x: math.exp(-x * x / 2.0),
    "x": lambda x: x,
}
# each integrand's intervals: forward, reversed and empty
INTERVALS = {
    "exp(x)": [(0.0, 1.0), (1.0, 0.0), (-2.0, 3.0), (0.5, 0.5)],
    "sin(x)": [(0.0, math.pi), (math.pi, 0.0), (-1.0, 4.0)],
    "x x": [(0.0, 1.0), (-1.0, 2.0), (2.0, -1.0)],
    "x x x x": [(-1.0, 1.0), (0.0, 3.0)],
    "1/(1+x x)": [(-5.0, 5.0), (0.0, 1.0)],
    "sqrt(x)": [(0.0, 1.0), (0.0, 4.0)],
    "exp(-x x/2)": [(-10.0, 10.0), (0.0, 1.0)],
}


def specs():
    return [(name, a, b) for name in INTERVALS for a, b in INTERVALS[name]]


def run(integrator, name, a, b):
    """The integral and the count, or the refusal."""
    def call():
        value = integrator(INTEGRANDS[name], a, b)
        return [value, integrator.numberOfEvaluations()]
    return v.attempt(call)


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for accuracy, max_iterations in ((1e-6, 100), (1e-10, 100), (1e-6, 6), (1e-6, 7)):
        rows = specs()
        integrator = ql.SimpsonIntegral(accuracy, max_iterations)
        out.append({"name": f"accuracy {accuracy!r}, {max_iterations} refinements",
                    "inputs": {"settings": st, "accuracy": accuracy, "maxIterations": max_iterations,
                               "integrals": [{"f": name, "a": a, "b": b} for name, a, b in rows]},
                    "expected": {"result": [run(integrator, name, a, b) for name, a, b in rows]}})
    out.append({"name": "an accuracy not above machine epsilon",
                "inputs": {"settings": st, "accuracy": 1e-17, "maxIterations": 100, "integrals": []},
                "expected": {"construction": v.attempt(lambda: ql.SimpsonIntegral(1e-17, 100) and 0)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/simpsonintegral.hpp", cases)
