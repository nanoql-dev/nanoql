# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmHestonFwdOp (ql/methods/finitedifferences/operators/fdmhestonfwdop.hpp, .cpp): the
Heston model's forward (Fokker-Planck) operator, on the evaluation date 15 October 2026.

tools/vectors/gen/processes/hestonprocess.py's three parameter sets on flat curves, each under the three
transformations of the variance (plain, power and log), and the first on zero curves under the plain one; then on the
first set, leverage functions (a constant local volatility, and
tools/vectors/gen/pricingengines/vanilla/fdhestonvanillaengine.py's fixed local volatility surface) under the plain
and log transformations. The mesher has 11 log-spot points concentrated at the spot and 7 variances: uniform from
0.002 to 0.5 for the plain and power transformations, uniform in the log variance from log(0.001) to log(0.6) for the
log one. For each, at three (t1, t2): size, then on seeded vectors apply, apply_mixed, apply_direction in each
direction, solve_splitting in each direction at s = -0.3 and 0.7, preconditioner at s = 0.4; and at the second
(t1, t2), to_sparse_matrix (QuantLib's toMatrix, the sum of toMatrixDecomp's three parts).

Left out: every structure is on the risk-free curve's day counter and reference date (on others QuantLib reads them at
the risk-free curve's times, tests/NanoQL.Tests/Divergences/HestonSlvMismatchedTimesTests.cs). The operator's mixing
factor, which the wheel doesn't take, is the FD SLV model's (hestonslvfdmmodel.py).

The specs are a "process" hestonprocess.py's, a "mesher" tools/vectors/gen/methods/finitedifferences/meshers/
fdmmeshercomposite.py's composite, a "type" (QuantLib's TransformationType: 0 plain, 1 power, 2 log) and a "leverage"
fdhestonvanillaengine.py's or null. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmHestonFwdOp(mesher,
hestonProcess, type, leverageFct), the C++ constructor; the members fdmhestonop.py's generator reads. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmHestonFwdOpVectorTests.cs."""
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


hop = _load("fdmhestonop", "methods/finitedifferences/operators/fdmhestonop.py")
mc, fhv = hop.mc, hop.fhv
hp, bsp = fhv.hp, fhv.bsp
R = fhv.R
PLAIN, POWER, LOG = int(ql.FdmSquareRootFwdOp.Plain), int(ql.FdmSquareRootFwdOp.Power), int(ql.FdmSquareRootFwdOp.Log)
TYPES = [("plain", PLAIN), ("power", POWER), ("log", LOG)]


def fwd_mesher(process_spec, transform):
    x0 = math.log(process_spec["s0"])
    spot = {"type": "Concentrating", "start": x0 - 1.5, "end": x0 + 1.5, "size": 11, "cPoint": [x0, 0.1],
            "requireCPoint": True}
    if transform == LOG:
        variance = {"type": "Uniform", "start": math.log(0.001), "end": math.log(0.6), "size": 7}
    else:
        variance = {"type": "Uniform", "start": 0.002, "end": 0.5, "size": 7}
    return mc.composite_spec(spot, variance)


def cases():
    st = v.settings(R)
    rng = random.Random(20261025)
    out = []
    variants = []
    for params in hp.PARAMETERS:
        p = fhv.heston_spec(params)
        variants += [(f"{params[0]}, flat curves, {tname}", p, t, None) for tname, t in TYPES]
    first = fhv.heston_spec(hp.PARAMETERS[0])
    variants.append((hp.PARAMETERS[0][0] + ", zero curves, plain",
                     fhv.heston_spec(hp.PARAMETERS[0], bsp.ZERO_R, bsp.ZERO_Q), PLAIN, None))
    variants += [("constant leverage, plain", first, PLAIN, fhv.CONSTANT_LEVERAGE),
                 ("surface leverage, plain", first, PLAIN, fhv.SURFACE_LEVERAGE),
                 ("surface leverage, log", first, LOG, fhv.SURFACE_LEVERAGE)]
    for name, pspec, transform, leverage in variants:
        mspec = fwd_mesher(pspec, transform)
        mesher, _keep = mc.composite(mspec)
        n = mesher.layout().size()
        rs = [hop.seeded(rng, n), hop.seeded(rng, n)]
        op = ql.FdmHestonFwdOp(mesher, hp.process(pspec), transform, fhv.leverage(leverage))
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "mesher": mspec, "type": transform,
                               "leverage": leverage, "r": rs, "times": hop.TIMES, "splitting": hop.SPLITTING,
                               "preconditioner": hop.PRECONDITIONER},
                    "expected": hop.op_outputs(op, rs)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmhestonfwdop.hpp", cases)
