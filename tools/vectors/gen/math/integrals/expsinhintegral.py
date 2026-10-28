# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for ExpSinhIntegral (ql/math/integrals/expsinhintegral.hpp, through ql/math/integrals/integral.hpp, .cpp),
QuantLib's wrapper of Boost's exp_sinh quadrature: the integral and the number of evaluations of
tools/vectors/gen/math/integrals/simpsonintegral.py's named integrands over half-infinite intervals, forward and
reversed, and Boost's refusals of a finite and of an infinite interval; and the one-argument integrate over [0, inf). At
QuantLib's default tolerance (√ε, 9 refinements), at 1e-12, at 1e-4, and at 1e-10 capped at three refinements, which
Boost counts as its seven seeded levels.

An infinite bound is written as vectors.enc writes it ("Infinity", "-Infinity"). The wheel's class is a SWIG wrapper of
the C++ class, ExpSinhIntegral(relTolerance, maxRefinements), with no logic of its own, calling the Python integrand.
Read back by tests/NanoQL.Tests/Vectors/Numerics/Integrals/ExpSinhIntegralVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_simpsonintegral", os.path.join(v.GEN_ROOT, "math", "integrals", "simpsonintegral.py"))
simpson = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(simpson)

INF = math.inf
ROWS = [("exp(-x x/2)", 0.0, INF), ("exp(-x x/2)", 1.0, INF), ("exp(-x x/2)", -INF, 0.0), ("exp(-x x/2)", -INF, 2.0),
        ("exp(-x x/2)", INF, 0.0), ("exp(-x x/2)", 0.5, -INF), ("1/(1+x x)", 0.0, INF), ("1/(1+x x)", -INF, -1.0),
        ("exp(x)", -INF, 0.0), ("exp(x)", -INF, 1.5), ("exp(x)", 1.0, -INF),
        ("x x", 0.0, 1.0), ("exp(-x x/2)", -INF, INF)]
HALF_LINE = ["exp(-x x/2)", "1/(1+x x)"]
SETTINGS = [("defaults", None, None), ("1e-12", 1e-12, 9), ("1e-4", 1e-4, 9), ("1e-10, three refinements", 1e-10, 3)]


def half_line(integrator, name):
    """The one-argument integrate over [0, inf) and its count, or the refusal."""
    def call():
        value = integrator.integrate(simpson.INTEGRANDS[name])
        return [value, integrator.numberOfEvaluations()]
    return v.attempt(call)


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, tolerance, refinements in SETTINGS:
        integrator = ql.ExpSinhIntegral() if tolerance is None else ql.ExpSinhIntegral(tolerance, refinements)
        out.append({"name": name,
                    "inputs": {"settings": st, "relTolerance": tolerance, "maxRefinements": refinements,
                               "integrals": [{"f": f, "a": v.enc(a), "b": v.enc(b)} for f, a, b in ROWS],
                               "halfLine": HALF_LINE},
                    "expected": {"result": [simpson.run(integrator, f, a, b) for f, a, b in ROWS],
                                 "halfLineResult": [half_line(integrator, f) for f in HALF_LINE]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/expsinhintegral.hpp", cases)
