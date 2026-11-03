# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmHullWhiteSolver (ql/methods/finitedifferences/solvers/fdmhullwhitesolver.hpp, .cpp): a
swaption's value rolled back on the Hull-White operator and read at points of the factor's line, on the evaluation date
15 January 2025.

The finite-difference Hull-White swaption engine's set-up (fdhullwhiteswaptionengine.cpp), built as
tools/vectors/gen/methods/finitedifferences/solvers/fdmg2solver.py builds the G2 engine's, on 25 points with 10 time
steps: its three swaptions on tools/vectors/gen/methods/finitedifferences/operators/fdmg2op.py's two Hull-White
models, under the Douglas, Hundsdorfer, Craig-Sneyd, modified Craig-Sneyd, modified Hundsdorfer, Crank-Nicolson,
implicit Euler and TR-BDF2 schemes (the one-direction step solving by splitting), one with two damping steps. Each case's
output is valueAt at seven points of the factor.

Generated and left out: TR-BDF2 on a time-dependent operator, as fdmg2solver.py's
(tests/NanoQL.Tests/Divergences/TrBdf2TimeDependentTests.cs), the Hull-White operator's short-rate term moving with
time.

The specs are fdmg2solver.py's. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: fdmg2solver.py's set-up with
FdmAffineHullWhiteModelSwapInnerValue, and FdmHullWhiteSolver(model, solverDesc, schemeDesc), the C++ constructor;
valueAt the C++ member. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Solvers/FdmHullWhiteSolverVectorTests.cs."""
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


g2s = _load("fdmg2solver", "methods/finitedifferences/solvers/fdmg2solver.py")
SCHEMES = ["Douglas", "Hundsdorfer", "CraigSneyd", "ModifiedCraigSneyd", "ModifiedHundsdorfer", "CrankNicolson",
           "ImplicitEuler", "TrBDF2"]
LEFT_OUT_SCHEMES = ["TrBDF2"]
POINTS = [-0.03, -0.01, -0.002, 0.0, 0.004, 0.012, 0.03]


def cases():
    out = []
    for k, model in enumerate(g2s.g2op.HULL_WHITES):
        for sname, swap, exercise in g2s.swaptions():
            for sch in SCHEMES if k == 0 else SCHEMES[:1]:
                for damping in (0, 2) if sch == "Douglas" else (0,):
                    with v.case_settings(g2s.TODAY) as st:
                        spec = g2s.setup_spec(model, swap, exercise, (25,), damping=damping)
                        m, mesher, desc, keep = g2s.built(spec)
                        solver = ql.FdmHullWhiteSolver(m, desc, g2s.fbs.scheme(g2s.scheme(sch)))
                        values = [solver.valueAt(x) for x in POINTS]
                        if sch in LEFT_OUT_SCHEMES:
                            continue
                        out.append({"name": f"model {k}, {sname}, {sch}{', 2 damping steps' if damping else ''}",
                                    "inputs": {"settings": st, "setup": spec, "scheme": g2s.scheme(sch),
                                               "points": POINTS},
                                    "expected": {"valueAt": values}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdmhullwhitesolver.hpp", cases)
