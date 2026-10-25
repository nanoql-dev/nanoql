# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmCEVOp (ql/methods/finitedifferences/operators/fdmcevop.hpp, .cpp): the CEV process's
backward operator, its diffusion 1/2 alpha^2 f^(2 beta) and its discounting at the curve's forward rate, on the
evaluation date 15 October 2026.

Three CEV processes (testFdmCevOp's f0 2.1, alpha 0.75 at beta 0.45 and -0.5, and beta 1.45 on a forward of 100) on
flat curves at 15%, 3% and -1% Actual/365 (Fixed), on the engine's mesher (FdmCEV1dMesher, its spec
tools/vectors/gen/methods/finitedifferences/meshers/fdmcev1dmesher.py's) and on that mesher in the first direction of
two, a uniform one second. For each, at three (t1, t2): size, then on seeded vectors apply, apply_mixed (zero),
apply_direction in each direction, solve_splitting in each direction at s = -0.3 and 0.7, and preconditioner at s = 0.4,
tools/vectors/gen/methods/finitedifferences/operators/fdmg2op.py's outputs. The operator in a direction other than 0,
where QuantLib's differentiates along direction 0 and nanoql's along the direction given (the S-van plan's VR4 (xiv),
tests/NanoQL.Tests/Divergences/FdmCevOpDirectionTests.cs), is left out.

The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmCEVOp(mesher, rTS, f0, alpha, beta, direction), the C++
constructor, and its members, the Arrays built from lists. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmCEVOpVectorTests.cs."""
import importlib.util
import os
import random
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


cm = _load("fdmcev1dmesher", "methods/finitedifferences/meshers/fdmcev1dmesher.py")
g2op = _load("fdmg2op", "methods/finitedifferences/operators/fdmg2op.py")
bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
R = cm.R
U5 = {"type": "Uniform", "start": -1.0, "end": 1.0, "size": 5}
TIMES = [[0.0, 0.1], [0.5, 0.75], [0.9, 1.0]]

PROCESSES = [
    ("testFdmCevOp's, beta 0.45", 2.1, 0.75, 0.45, 0.15, cm.cev_spec(30, 2.1, 0.75, 0.45, 1.0, 1e-6, 1.0, (2.3, 0.1))),
    ("beta -0.5", 2.1, 0.75, -0.5, 0.03, cm.cev_spec(25, 2.1, 0.75, -0.5, 1.0, 1e-4, 1.0, (2.3, 0.1))),
    ("beta 1.45 on 100", 100.0, 0.3 * 100.0 ** -0.45, 1.45, -0.01, cm.cev_spec(25, 100.0, 0.3 * 100.0 ** -0.45, 1.45, 1.0)),
]


def op_outputs(op, rs, dims, times=TIMES):
    """fdmg2op.py's outputs at the given (t1, t2)."""
    out = {"size": op.size(), "apply": [], "applyMixed": [], "applyDirection": [], "solveSplitting": [],
           "preconditioner": []}
    for t1, t2 in times:
        op.setTime(t1, t2)
        ra = [ql.Array(r) for r in rs]
        out["apply"].append([g2op.arr(op.apply(r)) for r in ra])
        out["applyMixed"].append([g2op.arr(op.apply_mixed(r)) for r in ra])
        out["applyDirection"].append([g2op.arr(op.apply_direction(d, ra[0])) for d in range(dims)])
        out["solveSplitting"].append([[g2op.arr(op.solve_splitting(d, ra[0], s)) for s in g2op.SPLITTING]
                                      for d in range(dims)])
        out["preconditioner"].append(g2op.arr(op.preconditioner(ra[0], g2op.PRECONDITIONER)))
    return out


def cases():
    st = v.settings(R)
    rng = random.Random(20261024)
    out = []
    for name, f0, alpha, beta, rate, mspec in PROCESSES:
        curve_spec = bsp.flat_spec(R, rate)
        curve = bsp.curve(curve_spec)
        for mname, comp in (("the engine's mesher", {"meshers": [mspec]}),
                            ("first of two directions", {"meshers": [mspec, U5]})):
            mesher, keep = cm.composite(comp)
            n = mesher.layout().size()
            dims = len(comp["meshers"])
            rs = [g2op.seeded(rng, n), g2op.seeded(rng, n)]
            op = ql.FdmCEVOp(mesher, curve, f0, alpha, beta, 0)
            out.append({"name": f"{name}, {mname}",
                        "inputs": {"settings": st, "f0": f0, "alpha": alpha, "beta": beta, "rTS": curve_spec,
                                   "mesher": comp, "direction": 0, "r": rs, "times": TIMES,
                                   "splitting": g2op.SPLITTING, "preconditioner": g2op.PRECONDITIONER},
                        "expected": op_outputs(op, rs, dims)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmcevop.hpp", cases)
