# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmHestonOp (ql/methods/finitedifferences/operators/fdmhestonop.hpp, .cpp): the Heston
model's two-factor operator, its equity part's drift, diffusion and leverage, its variance part, and the correlation
term, on the evaluation date 15 October 2026.

tools/vectors/gen/processes/hestonprocess.py's three parameter sets on flat curves and the first on zero curves, on a
mesher of 15 log-spot points concentrated at the spot and the 9 variances FdmHestonVarianceMesher places for a year
(given to the operator as a predefined mesher); then on the first set, a uniform variance mesher from 0, a quanto
helper, and leverage functions (a constant local volatility, and
tools/vectors/gen/pricingengines/vanilla/fdhestonvanillaengine.py's fixed local volatility surface). For each, at
three (t1, t2): size, then on seeded vectors apply, apply_mixed, apply_direction in each direction, solve_splitting in
each direction at s = -0.3 and 0.7, preconditioner at s = 0.4; and at the second (t1, t2), to_sparse_matrix (QuantLib's
toMatrix, the sum of toMatrixDecomp's three parts: its row_idx, col_idx and data, the stored entries in order).

Left out (the S-hes plan's §2.10): every structure is on the risk-free curve's day counter and reference date (on
others QuantLib reads them at the risk-free curve's times, tests/NanoQL.Tests/Divergences/
FdHestonMismatchedTimesTests.cs). The operator's mixing factor, which the wheel doesn't take, is the engines'
(fdhestonvanillaengine.py).

The specs are a "process" hestonprocess.py's, a "mesher" tools/vectors/gen/methods/finitedifferences/meshers/
fdmmeshercomposite.py's composite, a "quanto" helper tools/vectors/gen/methods/finitedifferences/meshers/
fdmblackscholesmesher.py's or null, and a "leverage" fdhestonvanillaengine.py's or null. The wheel's calls, by
QuantLib-SWIG 1.44's fdm.i: FdmHestonOp(mesher, hestonProcess, quantoHelper, leverageFct), the C++ constructor;
setTime, size, apply, apply_mixed, apply_direction, solve_splitting, preconditioner and to_sparse_matrix the C++
members, the Arrays built from lists. Read back by tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmHestonOpVectorTests.cs."""
import importlib.util
import math
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


mc = _load("fdmmeshercomposite", "methods/finitedifferences/meshers/fdmmeshercomposite.py")
fhv = _load("fdhestonvanillaengine", "pricingengines/vanilla/fdhestonvanillaengine.py")
hp, bsm, bsp = fhv.hp, fhv.bsm, fhv.bsp
R = fhv.R
TIMES = [[0.0, 0.1], [0.5, 0.75], [0.9, 1.0]]
SPLITTING = [-0.3, 0.7]
PRECONDITIONER = 0.4
MATRIX_TIMES = TIMES[1]


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def seeded(rng, n):
    return [rng.uniform(-1.0, 1.0) for _ in range(n)]


def op_outputs(op, rs, dims=2, matrix=True):
    out = {"size": op.size(), "apply": [], "applyMixed": [], "applyDirection": [], "solveSplitting": [],
           "preconditioner": []}
    if matrix:
        out.update({"matrixRows": [], "matrixColumns": [], "matrixValues": []})
    for t1, t2 in TIMES:
        op.setTime(t1, t2)
        ra = [ql.Array(r) for r in rs]
        out["apply"].append([arr(op.apply(r)) for r in ra])
        out["applyMixed"].append([arr(op.apply_mixed(r)) for r in ra])
        out["applyDirection"].append([arr(op.apply_direction(d, ra[0])) for d in range(dims)])
        out["solveSplitting"].append([[arr(op.solve_splitting(d, ra[0], s)) for s in SPLITTING] for d in range(dims)])
        out["preconditioner"].append(arr(op.preconditioner(ra[0], PRECONDITIONER)))
        if matrix and [t1, t2] == MATRIX_TIMES:
            m = op.to_sparse_matrix()
            out["matrixRows"].append([int(i) for i in m.row_idx])
            out["matrixColumns"].append([int(j) for j in m.col_idx])
            out["matrixValues"].append(v.enc(list(m.data)))
    return out


def heston_mesher(process_spec, x_size=15, v_size=9, uniform_variance=False):
    """The log-spot mesher concentrated at the spot, and the variance mesher QuantLib's engine builds for a year, as
    predefined locations, or a uniform one from 0."""
    x0 = math.log(process_spec["s0"])
    spot = {"type": "Concentrating", "start": x0 - 1.5, "end": x0 + 1.5, "size": x_size, "cPoint": [x0, 0.1],
            "requireCPoint": True}
    if uniform_variance:
        variance = {"type": "Uniform", "start": 0.0, "end": 0.5, "size": v_size}
    else:
        locations = ql.FdmHestonVarianceMesher(v_size, hp.process(process_spec), 1.0).locations()
        variance = {"type": "Predefined", "x": [x for x in locations]}
    return mc.composite_spec(spot, variance)


def cases():
    st = v.settings(R)
    rng = random.Random(20261025)
    out = []
    sets = [(params[0] + ", flat curves", fhv.heston_spec(params)) for params in hp.PARAMETERS]
    sets.append((hp.PARAMETERS[0][0] + ", zero curves", fhv.heston_spec(hp.PARAMETERS[0], bsp.ZERO_R, bsp.ZERO_Q)))
    first = sets[0][1]
    variants = [(name, p, heston_mesher(p), None, None) for name, p in sets]
    variants += [("uniform variance mesher", first, heston_mesher(first, 12, 8, True), None, None),
                 ("quanto", first, heston_mesher(first), bsm.FLAT_QUANTO, None),
                 ("constant leverage", first, heston_mesher(first), None, fhv.CONSTANT_LEVERAGE),
                 ("surface leverage", first, heston_mesher(first), None, fhv.SURFACE_LEVERAGE),
                 ("quanto and surface leverage", first, heston_mesher(first), bsm.FLAT_QUANTO, fhv.SURFACE_LEVERAGE)]
    for name, pspec, mspec, quanto, leverage in variants:
        mesher, _keep = mc.composite(mspec)
        n = mesher.layout().size()
        rs = [seeded(rng, n), seeded(rng, n)]
        op = ql.FdmHestonOp(mesher, hp.process(pspec), bsm.quanto(quanto), fhv.leverage(leverage))
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "mesher": mspec, "quanto": quanto,
                               "leverage": leverage, "r": rs, "times": TIMES, "splitting": SPLITTING,
                               "preconditioner": PRECONDITIONER},
                    "expected": op_outputs(op, rs)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmhestonop.hpp", cases)
