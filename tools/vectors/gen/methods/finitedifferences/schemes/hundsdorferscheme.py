# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HundsdorferScheme (ql/methods/finitedifferences/schemes/hundsdorferscheme.hpp, .cpp), on the
evaluation date 15 October 2026: the Hundsdorfer scheme, through the Hundsdorfer and ModifiedHundsdorfer descriptions
and one at theta 0.6, mu 0.4. Each rollback runs through QuantLib's FdmBackwardSolver, as QuantLib's engines run the
schemes, on the Black-Scholes operator: a put on a flat Merton process, with and without an American condition; a call
on a Merton process with zero curves and a variance surface, damped and with an American condition; the local
volatility; a quanto helper; and the underlying in the second direction of a two-dimensional mesher; and a rollback
towards negative time, refused ("a step towards negative time given").

The specs are tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's rollbacks, the initial values
(the payoff's cell averages) an input. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmBackwardSolver and its
rollback, FdmSchemeDesc's constructor and named factories, as that file reads them. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Schemes/HundsdorferSchemeVectorTests.cs."""
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
    return fbs.scheme_cases([("Hundsdorfer", ql.FdmSchemeDesc.Hundsdorfer()), ("ModifiedHundsdorfer", ql.FdmSchemeDesc.ModifiedHundsdorfer()), ("Hundsdorfer at theta 0.6, mu 0.4", ql.FdmSchemeDesc(ql.FdmSchemeDesc.HundsdorferType, 0.6, 0.4))])


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/schemes/hundsdorferscheme.hpp", cases)
