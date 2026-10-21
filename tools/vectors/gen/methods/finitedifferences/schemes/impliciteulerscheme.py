# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ImplicitEulerScheme (ql/methods/finitedifferences/schemes/impliciteulerscheme.hpp, .cpp), on
the evaluation date 15 October 2026: the implicit Euler scheme, through the ImplicitEuler description, whose damping
steps the solver adds to its own. Each rollback runs through QuantLib's FdmBackwardSolver, as QuantLib's engines run the
schemes, on the Black-Scholes operator: a put on a flat Merton process, with and without an American condition; a call
on a Merton process with zero curves and a variance surface, damped and with an American condition; the local
volatility; a quanto helper; and the underlying in the second direction of a two-dimensional mesher; and a rollback
towards negative time, refused ("a step towards negative time given"). Then the scheme on its own
(fdmbackwardsolver.py's direct cases): ten steps back from 1 on three of those operators, the values after each and
numberOfIterations after the last.

The specs are tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's rollbacks, the initial values
(the payoff's cell averages) an input. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmBackwardSolver and its
rollback, FdmSchemeDesc's constructor and named factories, as that file reads them; ImplicitEulerScheme(map), the C++
constructor, its setStep, step and numberOfIterations the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Schemes/ImplicitEulerSchemeVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


fbs = _load("fdmbackwardsolver", "methods/finitedifferences/solvers/fdmbackwardsolver.py")


def cases():
    return fbs.scheme_cases([("ImplicitEuler", ql.FdmSchemeDesc.ImplicitEuler())]) + fbs.direct_cases("ImplicitEuler", lambda op: ql.ImplicitEulerScheme(op))


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/schemes/impliciteulerscheme.hpp", cases)
