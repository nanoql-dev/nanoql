# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FirstDerivativeOp (ql/methods/finitedifferences/operators/firstderivativeop.hpp, .cpp): the
first derivative's three-point central differences inside, upwinded at the first point and downwinded at the last, on
the four meshers of tools/vectors/gen/methods/finitedifferences/operators/triplebandlinearop.py (one, two and three
dimensions; uniform, concentrating and Black-Scholes meshers) in each direction. The operator is read through apply on
two seeded vectors of the layout's size, and solve_splitting(r, a, b) on the first at (a, b) = (-0.3, 1) and (0.7, 2).

The specs are triplebandlinearop.py's. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FirstDerivativeOp(direction,
mesher), the C++ constructor; apply and solve_splitting the C++ members, the Arrays built from lists. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FirstDerivativeOpVectorTests.cs."""
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


tb = _load("triplebandlinearop", "methods/finitedifferences/operators/triplebandlinearop.py")


def cases():
    return tb.derivative_cases("First")


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/firstderivativeop.hpp", cases)
