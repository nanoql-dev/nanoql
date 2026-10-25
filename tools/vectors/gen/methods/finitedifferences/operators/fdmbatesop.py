# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmBatesOp (ql/methods/finitedifferences/operators/fdmbatesop.hpp, .cpp): the Heston
operator on the jump-compensated dividend curve, plus the jumps' integral by Gauss-Hermite quadrature over each
variance's slice, on the evaluation date 15 October 2026.

tools/vectors/gen/models/equity/batesmodel.py's three parameter sets on flat curves and the second on zero curves, on
tools/vectors/gen/methods/finitedifferences/operators/fdmhestonop.py's mesher (15 log-spots, 9 variances), with no
boundary conditions and QuantLib's engine's integration order 12; then on the second set: order 8, Dirichlet
boundaries on the log-spot's lower and upper sides (which the integral's jumps read past the grid), and a quanto
helper. For each, at fdmhestonop.py's three (t1, t2): size, then on seeded vectors apply, apply_mixed,
apply_direction in each direction, solve_splitting in each direction at s = -0.3 and 0.7, and preconditioner at
s = 0.4; and to_sparse_matrix, which QuantLib refuses ("not implemented": the integral has no sparse form).

Left out (the S-hes plan's §2.10): every structure is on the risk-free curve's day counter and reference date (on
others QuantLib reads them at the risk-free curve's times, tests/NanoQL.Tests/Divergences/
FdHestonMismatchedTimesTests.cs).

The specs are a "process" batesmodel.py's, a "mesher" fdmhestonop.py's, the "boundaries" [[valueOnBoundary,
direction, side]] (side as tools/vectors/gen/methods/finitedifferences/utilities/fdmdirichletboundary.py writes it),
the "integrationOrder", and a "quanto" helper or null. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i:
FdmBatesOp(mesher, batesProcess, bcSet, integroIntegrationOrder, quantoHelper), FdmDirichletBoundary(mesher, value,
direction, side) appended to an FdmBoundaryConditionSet; setTime, size, apply, apply_mixed, apply_direction,
solve_splitting, preconditioner and to_sparse_matrix the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmBatesOpVectorTests.cs."""
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


fho = _load("fdmhestonop", "methods/finitedifferences/operators/fdmhestonop.py")
fbe = _load("fdbatesvanillaengine", "pricingengines/vanilla/fdbatesvanillaengine.py")
bm, bp = fbe.bm, fbe.bp
mc, bsm, bsp = fho.mc, fho.bsm, fho.bsp
R = fho.R
UPPER, LOWER = int(ql.FdmDirichletBoundary.Upper), int(ql.FdmDirichletBoundary.Lower)


def bates_mesher(process_spec):
    heston = {k: x for k, x in process_spec.items() if k not in ("lambda", "nu", "delta")}
    heston["discretization"] = fho.fhv.QE
    return fho.heston_mesher(heston)


def boundary_set(mesher, specs):
    bc = ql.FdmBoundaryConditionSet()
    for value, direction, side in specs:
        bc.push_back(ql.FdmDirichletBoundary(mesher, value, direction, side))
    return bc


def cases():
    st = v.settings(R)
    rng = random.Random(20261026)
    out = []
    sets = [(params[0] + ", flat curves", fbe.bates_spec(params)) for params in bm.PARAMETERS]
    sets.append((bm.PARAMETERS[1][0] + ", zero curves", fbe.bates_spec(bm.PARAMETERS[1], bsp.ZERO_R, bsp.ZERO_Q)))
    second = sets[1][1]
    variants = [(name, p, [], 12, None) for name, p in sets]
    variants += [("order 8", second, [], 8, None),
                 ("Dirichlet boundaries", second, [[0.0, 0, LOWER], [50.0, 0, UPPER]], 12, None),
                 ("quanto", second, [], 12, bsm.FLAT_QUANTO)]
    for name, pspec, boundaries, order, quanto in variants:
        mspec = bates_mesher(pspec)
        mesher, _keep = mc.composite(mspec)
        n = mesher.layout().size()
        rs = [fho.seeded(rng, n), fho.seeded(rng, n)]
        bc = boundary_set(mesher, boundaries)
        op = ql.FdmBatesOp(mesher, bp.process(pspec), bc, order, bsm.quanto(quanto))
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "mesher": mspec, "boundaries": boundaries,
                               "integrationOrder": order, "quanto": quanto, "r": rs, "times": fho.TIMES,
                               "splitting": fho.SPLITTING, "preconditioner": fho.PRECONDITIONER},
                    "expected": {**fho.op_outputs(op, rs, matrix=False),
                                 "matrix": v.attempt(lambda: op.to_sparse_matrix())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmbatesop.hpp", cases)
