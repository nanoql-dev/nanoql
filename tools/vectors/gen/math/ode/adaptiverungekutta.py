# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for AdaptiveRungeKutta (ql/math/ode/adaptiverungekutta.hpp) over real values, through the wheel's
RungeKutta, QuantLib-Python's %template(RungeKutta) AdaptiveRungeKutta<Real> (ode.i): the solution f(x2) of
f'(x) = F(x, f(x)) with f(x1) = y1, for ten scalar equations and four vector ones (EQUATIONS below), each to three
points, under four settings (eps, h1, hmin), the defaults (1e-6, 1e-4, 0) by the default constructor among them; then
integrations backward and of length zero, and the ones QuantLib fails: a step at or below hmin, too many steps (10000)
and a step size underflowing at a singularity. A NaN or infinite F leaves the step's error out of the maximum,
QuantLib's std::max, so the integration runs to the end.

The wheel's __call__(fct, y1, x1, x2) wraps a Python callable f(x, y): a float y1 calls the scalar form, the
C++ operator() on an OdeFct1d, and a list the vector form, on an OdeFct, after its own check that the function returns
a list of y1's length (ode.i:79-81), which QuantLib doesn't make. Each case's input "equation" names its function,
written the same way in tests/NanoQL.Tests/Vectors/Numerics/Ode/OdeEquations.cs, which
tests/NanoQL.Tests/Vectors/Numerics/Ode/AdaptiveRungeKuttaVectorTests.cs uses to read the file back. A failure is
{"throws": true}."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

# The scalar equations F(x, y), and the vector ones F(x, [y0, y1, ...]).
EQUATIONS = {
    "exp": lambda x, y: y,
    "decay": lambda x, y: -0.5 * y,
    "logistic": lambda x, y: y * (1.0 - y),
    "gaussian": lambda x, y: -2.0 * x * y,
    "linear": lambda x, y: x + y,
    "riccati": lambda x, y: y * y,
    "stiff": lambda x, y: -1000.0 * y + 1000.0,
    "singular": lambda x, y: 1.0 / (1.0 - x),
    "nan": lambda x, y: math.nan,
    "infinity": lambda x, y: math.inf,
    "oscillator": lambda x, y: [y[1], -y[0]],
    "predatorPrey": lambda x, y: [y[0] - y[0] * y[1], y[0] * y[1] - y[1]],
    "chain": lambda x, y: [y[1], y[2], -y[0]],
    "expVector": lambda x, y: [y[0]],
}

# (eps, h1, hmin), None for the default constructor
SETTINGS = [None, (1e-12, 1e-4, 0.0), (1e-8, 1e-5, 0.0), (1e-4, 1e-2, 0.0)]

SCALAR = [
    ("exp", 1.0, 0.0, [0.25, 1.0, 5.0]),
    ("decay", 2.0, 0.0, [0.5, 3.0, 10.0]),
    ("logistic", 0.1, 0.0, [1.0, 4.0, 12.0]),
    ("gaussian", 1.0, -1.0, [0.0, 1.0, 2.5]),
    ("linear", 0.0, 0.0, [0.1, 1.0, 2.0]),
    ("riccati", 1.0, 0.0, [0.5, 0.9, 0.99]),
    ("stiff", 0.0, 0.0, [0.001, 0.01, 0.5]),
    ("singular", 0.0, 0.0, [0.5, 0.9, 0.999]),
    ("nan", 1.0, 0.0, [0.5, 1.0, 2.0]),
    ("infinity", 1.0, 0.0, [0.5, 1.0, 2.0]),
]
VECTOR = [
    ("oscillator", [0.0, 1.0], 0.0, [1.0, 3.0, 5.0]),
    ("predatorPrey", [1.5, 0.5], 0.0, [0.5, 2.0, 6.0]),
    ("chain", [1.0, 0.0, -1.0], 0.0, [0.5, 1.0, 4.0]),
    ("expVector", [1.0], 0.0, [0.25, 1.0, 5.0]),
]
# (label, equation, y1, x1, x2, settings)
SPECIAL = [
    ("backward", "exp", 1.0, 1.0, 0.0, None),
    ("backward", "gaussian", 1.0, 2.0, -1.0, (1e-10, 1e-4, 0.0)),
    ("backward", "oscillator", [0.0, 1.0], 5.0, 0.0, (1e-10, 1e-3, 0.0)),
    ("length zero", "exp", 1.0, 1.0, 1.0, None),
    ("length zero", "chain", [1.0, 2.0, 3.0], -2.0, -2.0, None),
    ("step at hmin", "exp", 1.0, 0.0, 5.0, (1e-10, 1e-4, 0.5)),
    ("step at hmin", "oscillator", [0.0, 1.0], 0.0, 5.0, (1e-10, 1e-4, 0.1)),
    ("too many steps", "exp", 1.0, 0.0, 1000.0, (1e-12, 1e-4, 0.0)),
    ("too many steps", "stiff", 0.0, 0.0, 100.0, None),
    ("step size underflow", "riccati", 1.0, 0.0, 2.0, None),
    ("step size underflow", "singular", 0.0, 0.0, 2.0, None),
    ("tiny eps", "exp", 1.0, 0.0, 1.0, (1e-20, 1e-4, 0.0)),
    ("negative first step", "exp", 1.0, 0.0, 1.0, (1e-8, -1e-4, 0.0)),
]


def integrator(settings):
    return ql.RungeKutta() if settings is None else ql.RungeKutta(*settings)


def case(label, equation, y1, x1, x2, settings):
    st = v.settings(ql.Date(15, ql.October, 2026))
    f = EQUATIONS[equation]
    rk = integrator(settings)
    return {"name": f"{label} / {equation} from {x1!r} to {x2!r} / {'default' if settings is None else settings}",
            "inputs": {"settings": st, "equation": equation, "y1": v.enc(y1), "x1": x1, "x2": x2,
                       "integrator": None if settings is None else list(settings)},
            "expected": {"y": v.attempt(lambda: list(rk(f, y1, x1, x2)) if isinstance(y1, list)
                                        else rk(f, y1, x1, x2))}}


def cases():
    out = []
    for equation, y1, x1, points in SCALAR + VECTOR:
        for settings in SETTINGS:
            for x2 in points:
                out.append(case("integration", equation, y1, x1, x2, settings))
    out += [case(*row) for row in SPECIAL]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/ode/adaptiverungekutta.hpp", cases)
