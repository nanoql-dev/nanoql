# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmHullWhiteOp (ql/methods/finitedifferences/operators/fdmhullwhiteop.hpp, .cpp): the
Hull-White model's one-factor operator, its drift and diffusion and its short-rate discounting, on the evaluation date
15 January 2025.

Two Hull-White models (a = 0.05, sigma = 0.01; and a = 0.2, sigma = 0.015) on a flat 2.7% Actual/365 (Fixed) curve, on
three meshers: the swaption engine's, the factor's Ornstein-Uhlenbeck quantiles to 5 years (25 points), a uniform one,
and the second direction of a uniform by concentrating mesher. For each, at three (t1, t2): size, then on seeded vectors
apply, apply_mixed (zero), apply_direction in each direction, solve_splitting in each direction at s = -0.3 and 0.7, and
preconditioner at s = 0.4.

The specs are tools/vectors/gen/methods/finitedifferences/operators/fdmg2op.py's. The wheel's calls, by QuantLib-SWIG
1.44's fdm.i: FdmHullWhiteOp(mesher, model, direction), the C++ constructor; setTime, size, apply, apply_mixed,
apply_direction, solve_splitting and preconditioner the C++ members, the Arrays built from lists. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmHullWhiteOpVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


g2op = _load("fdmg2op", "methods/finitedifferences/operators/fdmg2op.py")


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmhullwhiteop.hpp",
           lambda: g2op.cases(g2op.HULL_WHITES, 20261023))
