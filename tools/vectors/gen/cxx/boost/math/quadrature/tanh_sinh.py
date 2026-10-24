# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Boost.Math's tanh_sinh quadrature (boost/math/quadrature/tanh_sinh.hpp, Boost 1.90) as QuantLib's
TanhSinhIntegral calls it, each output computed by the C++ harness: the integral and Boost's error estimate, which the
wheel doesn't expose, of tools/vectors/gen/math/integrals/simpsonintegral.py's named integrands over its intervals,
forward and reversed, and over half-infinite and infinite ones (a bound at ±DBL_MAX, which Boost reads as infinite),
at four tolerances and refinement caps and two minimum complements; on the evaluation date 15 October 2026.

Equal bounds are left out: QuantLib's Integrator returns 0 before calling Boost, and Boost leaves the error unwritten.
Read back by tests/NanoQL.Tests/Vectors/Cxx/Boost/TanhSinhCxxVectorTests.cs, through TanhSinhIntegral's integral and
absolute error."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_simpsonintegral", os.path.join(v.GEN_ROOT, "math", "integrals", "simpsonintegral.py"))
simpson = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(simpson)

R = 46310
MAX = 1.7976931348623157e308
DBL_MIN = 2.2250738585072014e-308
UNBOUNDED = [("1/(1+x x)", 0.0, MAX), ("1/(1+x x)", -MAX, MAX), ("1/(1+x x)", MAX, 0.0), ("1/(1+x x)", -MAX, 2.0),
             ("exp(-x x/2)", -MAX, 0.0), ("exp(-x x/2)", -MAX, MAX), ("exp(-x x/2)", 1.0, MAX)]
SETTINGS = [("defaults", math.sqrt(2.220446049250313e-16), 15, 4 * DBL_MIN), ("1e-12", 1e-12, 15, 4 * DBL_MIN),
            ("1e-4", 1e-4, 15, 4 * DBL_MIN), ("1e-10, three refinements", 1e-10, 3, 4 * DBL_MIN),
            ("defaults, minimum complement 1e-30", math.sqrt(2.220446049250313e-16), 15, 1e-30)]


def cases():
    rows = [(f, a, b) for f, a, b in simpson.specs() if a != b] + UNBOUNDED
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for name, tolerance, refinements, complement in SETTINGS:
            out.append({"name": name,
                        "inputs": {"settings": st, "relTolerance": tolerance, "maxRefinements": refinements,
                                   "minComplement": complement,
                                   "integrals": [{"f": f, "a": a, "b": b} for f, a, b in rows]},
                        "expected": {"result": [v.attempt(lambda: H.call("tanh_sinh::integrate", f, a, b, tolerance,
                                                                         refinements, complement))
                                                for f, a, b in rows]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "boost/math/quadrature/tanh_sinh.hpp", cases)
