# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Fdm2DimSolver (ql/methods/finitedifferences/solvers/fdm2dimsolver.hpp, .cpp): a
two-dimensional rollback and the bicubic spline it reads, with the theta from the step before the end, on the
evaluation date 15 January 2025.

tools/vectors/gen/methods/finitedifferences/solvers/fdmg2solver.py's set-ups on the first G2 model, its three
swaptions, the solver given the G2 operator (FdmG2Op in directions 0 and 1) under the Hundsdorfer, Douglas and
Craig-Sneyd schemes. Each case's outputs at nine points: interpolateAt, derivativeX, derivativeY, derivativeXX,
derivativeYY and derivativeXY; and thetaAt at those of them where the theta doesn't cancel ("thetaPoints").

Generated and left out: the theta at a point where it cancels, the value there more than 1e4 times the theta's change
over its snapshot interval, |v|/|theta·t| with t QuantLib's 0.99·min(1/365, the first stopping time)
(fdm2dimsolver.cpp): the theta is the difference of two rollbacks over that interval, which turns the values' agreement
within the tolerance into an error that much larger.

The specs are fdmg2solver.py's. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: fdmg2solver.py's set-up,
FdmG2Op(mesher, model, 0, 1) and Fdm2DimSolver(solverDesc, schemeDesc, op), the C++ constructors; interpolateAt,
thetaAt and the derivatives the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Solvers/Fdm2DimSolverVectorTests.cs."""
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
CANCELLATION = 1e4
OUTPUTS = ["interpolateAt", "thetaAt", "derivativeX", "derivativeY", "derivativeXX", "derivativeYY", "derivativeXY"]


def cases():
    out = []
    model = g2s.g2op.G2S[0]
    for sname, swap, exercise in g2s.swaptions():
        for sch in ("Hundsdorfer", "Douglas", "CraigSneyd"):
            with v.case_settings(g2s.TODAY) as st:
                spec = g2s.setup_spec(model, swap, exercise, (12, 10))
                m, mesher, desc, keep = g2s.built(spec)
                op = ql.FdmG2Op(mesher, m, 0, 1)
                solver = ql.Fdm2DimSolver(desc, g2s.fbs.scheme(g2s.scheme(sch)), op)
                expected = {o: [v.enc(getattr(solver, o)(x, y)) for x, y in g2s.POINTS_2D] for o in OUTPUTS}
                stops = list(keep[-1].stoppingTimes())
                t = 0.99 * min(1.0 / 365.0, stops[0] if stops else desc.getMaturity())
                kept = [i for i, (value, theta) in enumerate(zip(expected["interpolateAt"], expected["thetaAt"]))
                        if abs(value) <= CANCELLATION * abs(theta * t)]
                expected["thetaAt"] = [expected["thetaAt"][i] for i in kept]
                out.append({"name": f"{sname}, {sch}",
                            "inputs": {"settings": st, "setup": spec, "scheme": g2s.scheme(sch),
                                       "points": g2s.POINTS_2D, "thetaPoints": [g2s.POINTS_2D[i] for i in kept]},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdm2dimsolver.hpp", cases)
