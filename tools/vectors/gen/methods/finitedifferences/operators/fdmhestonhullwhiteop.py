# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmHestonHullWhiteOp (ql/methods/finitedifferences/operators/fdmhestonhullwhiteop.hpp,
.cpp): the Heston-Hull-White three-factor operator, its equity part's drift on the short rate, its variance part, the
Hull-White part and the two correlation terms, on the evaluation date 15 October 2026.

tools/vectors/gen/processes/hestonprocess.py's first two parameter sets on flat curves and the first on zero curves,
with two Hull-White processes on the risk-free curve (reversion 0.1 and volatility 0.01; 0.03 and 0.02) at the
equity-rate correlations -0.5 and 0.3, five of the combinations, on a mesher of 7 log-spot points concentrated at the
spot, the 4 variances FdmHestonVarianceMesher places for a year (given as a predefined mesher) and 4 uniform short-rate
states from -0.1 to 0.1. For each, at three (t1, t2): size, then on seeded vectors apply, apply_mixed, apply_direction
in each direction, solve_splitting in each direction at s = -0.3 and 0.7, preconditioner at s = 0.4; and at the second
(t1, t2), to_sparse_matrix (QuantLib's toMatrix, the sum of toMatrixDecomp's four parts: its row_idx, col_idx and data,
the stored entries in order). Then the constructor's refusal of correlations whose squares sum past 1.

Left out (the S-hes plan's §2.10): every structure is on the risk-free curve's day counter and reference date (on
others QuantLib reads the dividend curve at the risk-free curve's times,
tests/NanoQL.Tests/Divergences/HestonHullWhiteMismatchedTimesTests.cs).

The specs are a "process" hestonprocess.py's, a "hullWhite" {"a", "sigma"} on the process's risk-free curve, the
"correlation", and a "mesher" tools/vectors/gen/methods/finitedifferences/meshers/fdmmeshercomposite.py's composite.
The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmHestonHullWhiteOp(mesher, hestonProcess,
HullWhiteProcess(riskFree, a, sigma), equityShortRateCorrelation), the C++ constructor; setTime, size, apply,
apply_mixed, apply_direction, solve_splitting, preconditioner and to_sparse_matrix the C++ members, the Arrays built
from lists. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmHestonHullWhiteOpVectorTests.cs."""
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


fho = _load("fdmhestonop", "methods/finitedifferences/operators/fdmhestonop.py")
fhv, mc = fho.fhv, fho.mc
hp, bsp = fhv.hp, fhv.bsp
R = fhv.R
HULL_WHITE = [{"a": 0.1, "sigma": 0.01}, {"a": 0.03, "sigma": 0.02}]
CORRELATIONS = [-0.5, 0.3]


def hull_white(process, spec):
    return ql.HullWhiteProcess(process.riskFreeRate(), spec["a"], spec["sigma"])


def mesher_spec(process_spec, x_size=7, v_size=4, r_size=4):
    """The log-spot mesher concentrated at the spot, the variance mesher QuantLib's engine builds for a year as
    predefined locations, and uniform short-rate states."""
    x0 = math.log(process_spec["s0"])
    spot = {"type": "Concentrating", "start": x0 - 1.5, "end": x0 + 1.5, "size": x_size, "cPoint": [x0, 0.1],
            "requireCPoint": True}
    locations = ql.FdmHestonVarianceMesher(v_size, hp.process(process_spec), 1.0).locations()
    variance = {"type": "Predefined", "x": [x for x in locations]}
    rate = {"type": "Uniform", "start": -0.1, "end": 0.1, "size": r_size}
    return mc.composite_spec(spot, variance, rate)


def cases():
    st = v.settings(R)
    rng = random.Random(20261025)
    out = []
    sets = [(params[0] + ", flat curves", fhv.heston_spec(params)) for params in hp.PARAMETERS[:2]]
    sets.append((hp.PARAMETERS[0][0] + ", zero curves", fhv.heston_spec(hp.PARAMETERS[0], bsp.ZERO_R, bsp.ZERO_Q)))
    combinations = [(0, 0, 0), (0, 1, 1), (1, 0, 1), (1, 1, 0), (2, 1, 0)]
    for (name, pspec), hw, corr in ((sets[i], HULL_WHITE[j], CORRELATIONS[k]) for i, j, k in combinations):
        mspec = mesher_spec(pspec)
        mesher, _keep = mc.composite(mspec)
        n = mesher.layout().size()
        rs = [fho.seeded(rng, n), fho.seeded(rng, n)]
        p = hp.process(pspec)
        op = ql.FdmHestonHullWhiteOp(mesher, p, hull_white(p, hw), corr)
        out.append({"name": f"{name}, a {hw['a']}, sigma {hw['sigma']}, correlation {corr}",
                    "inputs": {"settings": st, "process": pspec, "hullWhite": hw, "correlation": corr,
                               "mesher": mspec, "r": rs, "times": fho.TIMES, "splitting": fho.SPLITTING,
                               "preconditioner": fho.PRECONDITIONER},
                    "expected": fho.op_outputs(op, rs, dims=3)})
    pspec = sets[0][1]
    mspec = mesher_spec(pspec)
    mesher, _keep = mc.composite(mspec)
    p = hp.process(pspec)
    out.append({"name": "correlations whose squares sum past 1",
                "inputs": {"settings": st, "process": pspec, "hullWhite": HULL_WHITE[0], "correlation": 0.8,
                           "mesher": mspec},
                "expected": {"size": v.attempt(lambda: ql.FdmHestonHullWhiteOp(
                    mesher, p, hull_white(p, HULL_WHITE[0]), 0.8).size())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmhestonhullwhiteop.hpp", cases)
