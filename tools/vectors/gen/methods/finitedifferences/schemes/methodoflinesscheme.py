# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MethodOfLinesScheme (ql/methods/finitedifferences/schemes/methodoflinesscheme.hpp, .cpp), on
the evaluation date 15 October 2026: the method of lines, through the MethodOfLines description (eps 0.001, relative
initial step 0.01) and one at eps 1e-4 and 0.05. Each rollback runs through QuantLib's FdmBackwardSolver, as QuantLib's
engines run the schemes, on the Black-Scholes operator: a put on a flat Merton process, with and without an American
condition; a call on a Merton process with zero curves and a variance surface, damped and with an American condition;
the local volatility; a quanto helper; and the underlying in the second direction of a two-dimensional mesher; and a
rollback towards negative time, refused ("a step towards negative time given").

The specs are tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's rollbacks, the initial values
(the payoff's cell averages) an input. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmBackwardSolver and its
rollback, FdmSchemeDesc's constructor and named factories, as that file reads them. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Schemes/MethodOfLinesSchemeVectorTests.cs."""
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
    return fbs.scheme_cases([("MethodOfLines", ql.FdmSchemeDesc.MethodOfLines()), ("method of lines at 1e-4, 0.05", ql.FdmSchemeDesc.MethodOfLines(1e-4, 0.05))])


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/schemes/methodoflinesscheme.hpp", cases)
