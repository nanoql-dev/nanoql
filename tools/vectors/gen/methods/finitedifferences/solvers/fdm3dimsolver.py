# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Fdm3DimSolver (ql/methods/finitedifferences/solvers/fdm3dimsolver.hpp, .cpp): a rollback on a
three-dimensional operator and its interpolation, a bicubic spline in each layer of the third coordinate and a
monotonic natural cubic spline across the layers, on the evaluation date 15 October 2026.

tools/vectors/gen/methods/finitedifferences/solvers/fdmhestonhullwhitesolver.py's set-ups on FdmHestonHullWhiteOp
(the operator built from each set-up's processes and correlation, on its mesher), each under its scheme: interpolateAt
at the eighteen points of that file, in the mesher's coordinates (the log spot, the variance and the short rate's
state), and thetaAt at those of them where the theta doesn't cancel, by that file's rule.

Left out: every structure is on the risk-free curve's day counter and reference date; and cash dividends
(fdmhestonhullwhitesolver.py's reason).

A case holds fdmhestonhullwhitesolver.py's set-up, scheme and points, in the mesher's coordinates. The wheel's calls,
by QuantLib-SWIG 1.44's fdm.i: FdmHestonHullWhiteOp(mesher, hestonProcess, hwProcess, correlation), the set-up's
FdmSolverDesc, and Fdm3DimSolver(solverDesc, schemeDesc, op); interpolateAt and thetaAt its C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Solvers/Fdm3DimSolverVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


fhhs = _load("fdmhestonhullwhitesolver", "methods/finitedifferences/solvers/fdmhestonhullwhitesolver.py")
fbs, mc = fhhs.fbs, fhhs.mc
R = fhhs.R


def cases():
    st = v.settings(R)
    out = []
    for name, spec, scheme in fhhs.variants():
        p, hw, desc, stops, keep = fhhs.built(spec)
        scheme_spec = fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Hundsdorfer())
        mesher = keep[-1]
        op = ql.FdmHestonHullWhiteOp(mesher, p, hw, spec["correlation"])
        solver = ql.Fdm3DimSolver(desc, fbs.scheme(scheme_spec), op)
        points = [[math.log(s), var, r] for s, var, r in fhhs.points_of(spec)]
        values = [v.enc(solver.interpolateAt(x, y, z)) for x, y, z in points]
        thetas = [solver.thetaAt(x, y, z) for x, y, z in points]
        kept = fhhs.theta_kept(values, thetas, stops, desc.getMaturity())
        out.append({"name": name,
                    "inputs": {"settings": st, "setup": spec, "scheme": scheme_spec, "points": points,
                               "thetaPoints": [points[i] for i in kept]},
                    "expected": {"interpolateAt": values, "thetaAt": [v.enc(thetas[i]) for i in kept]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdm3dimsolver.hpp", cases)
