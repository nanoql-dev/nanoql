# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for TanhSinhIntegral (ql/math/integrals/tanhsinhintegral.hpp, through ql/math/integrals/integral.hpp, .cpp),
QuantLib's wrapper of Boost's tanh_sinh quadrature: the integral and the number of evaluations of
tools/vectors/gen/math/integrals/simpsonintegral.py's named integrands over its intervals, forward, reversed and
empty, and over half-infinite and infinite ones, forward and reversed, at QuantLib's default tolerance (√ε, 15
refinements, a minimum complement of 4·DBL_MIN), at 1e-12, at 1e-4, and at 1e-10 capped at three refinements, which
Boost counts as its seven seeded levels.

An infinite bound is written as vectors.enc writes it ("Infinity", "-Infinity"). The wheel's class is a SWIG wrapper of
the C++ class, TanhSinhIntegral(relTolerance, maxRefinements, minComplement), with no logic of its own, calling the
Python integrand. Boost's error estimate, which the wheel doesn't expose, is vectored through the C++ harness
(tools/vectors/gen/cxx/boost/math/quadrature/tanh_sinh.py). Read back by
tests/NanoQL.Tests/Vectors/Numerics/Integrals/TanhSinhIntegralVectorTests.cs."""
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
UNBOUNDED = [("1/(1+x x)", 0.0, INF), ("1/(1+x x)", -INF, INF), ("1/(1+x x)", INF, 0.0), ("1/(1+x x)", -INF, 2.0),
             ("exp(-x x/2)", -INF, 0.0), ("exp(-x x/2)", -INF, INF), ("exp(-x x/2)", 1.0, INF),
             ("exp(-x x/2)", INF, -INF)]
SETTINGS = [("defaults", None, None), ("1e-12", 1e-12, 15), ("1e-4", 1e-4, 15),
            ("1e-10, three refinements", 1e-10, 3)]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    rows = simpson.specs() + UNBOUNDED
    out = []
    for name, tolerance, refinements in SETTINGS:
        integrator = ql.TanhSinhIntegral() if tolerance is None else ql.TanhSinhIntegral(tolerance, refinements)
        out.append({"name": name,
                    "inputs": {"settings": st, "relTolerance": tolerance, "maxRefinements": refinements,
                               "integrals": [{"f": f, "a": v.enc(a), "b": v.enc(b)} for f, a, b in rows]},
                    "expected": {"result": [simpson.run(integrator, f, a, b) for f, a, b in rows]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/tanhsinhintegral.hpp", cases)
