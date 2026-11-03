# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmLocalVolFwdOp (ql/methods/finitedifferences/operators/fdmlocalvolfwdop.hpp, .cpp) and
Predefined1dMesher (ql/methods/finitedifferences/meshers/predefined1dmesher.hpp), on the evaluation date 15 October
2026: the local-volatility forward (Fokker-Planck) operator in the log spot after setTime(t1, t2), over four local
volatilities (a constant one, a fixed surface extrapolating by its interpolator, and Dupire's over a constant and over a
surface held flat outside its strikes), on flat and zero curves, on a Black-Scholes mesher of 21 points, on predefined
meshers (uneven, and the calculator's ln(spot) points' kind of uniform), and on a two-dimensional mesher with the
underlying in direction 1; and the predefined meshers' locations and distances.

For each and for (t1, t2) of (0, 0.25), (0.5, 0.75) and (0.9, 1): the operator outputs
tools/vectors/gen/methods/finitedifferences/operators/fdmblackscholesfwdop.py lists. Every structure is dated 15 October
2026 on Act/365: on another clock nanoql reads the structures at the instants the operator's times denote, where
QuantLib reads them at those times (tests/NanoQL.Tests/Divergences/LocalVolMismatchedTimesTests.cs). The fixed surface
extrapolates by its interpolator on both sides, which nanoql's fixes don't reach.

The specs are an operator {"mesher", "spot", "riskFree", "dividend", "localVol", "direction"}, the mesher
fdmmeshercomposite.py's (a predefined 1-D mesher {"type": "Predefined", "x"}), the curves blackscholesprocess.py's, the
local volatility {"type": "Constant", "volatility"}, {"type": "Fixed", "surface"} (fixedlocalvolsurface.py's spec) or
{"type": "Dupire", "volatility"} (a Black structure spec). The wheel's calls, by QuantLib-SWIG 1.44:
FdmLocalVolFwdOp(mesher, spot, rTS, qTS, localVol, direction), Predefined1dMesher(x), LocalConstantVol(referenceDate,
volatility, dayCounter) and LocalVolSurface(blackTS, riskFreeTS, dividendTS, underlying). Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmLocalVolFwdOpVectorTests.cs."""
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


fwd = _load("fdmblackscholesfwdop", "methods/finitedifferences/operators/fdmblackscholesfwdop.py")
flv = _load("fixedlocalvolsurface", "termstructures/volatility/equityfx/fixedlocalvolsurface.py")
mc, bsm, bsp = fwd.mc, fwd.bsm, fwd.bsp
bvts = bsp.bvts
R = bsm.R
WIDE = [1.0, 50.0, 80.0, 100.0, 125.0, 200.0, 10000.0]
FIXED = flv.surface_spec("times", [0.25, 0.5, 1.0, 2.0], WIDE,
                         [[0.40, 0.38, 0.37, 0.36], [0.30, 0.29, 0.28, 0.275], [0.25, 0.245, 0.24, 0.235],
                          [0.22, 0.215, 0.21, 0.208], [0.23, 0.225, 0.22, 0.218], [0.27, 0.26, 0.255, 0.25],
                          [0.33, 0.32, 0.31, 0.30]], lower=flv.INTERP, upper=flv.INTERP)
HELD = bvts.vol_spec_surface(R, [91, 365, 730], [70.0, 85.0, 100.0, 115.0, 130.0],
                             [[0.32, 0.30, 0.28], [0.26, 0.25, 0.24], [0.22, 0.215, 0.21], [0.21, 0.205, 0.20],
                              [0.23, 0.22, 0.215]],
                             lower=ql.BlackVarianceSurface.ConstantExtrapolation,
                             upper=ql.BlackVarianceSurface.ConstantExtrapolation)
LOCAL_VOLS = [
    ("constant 25%", {"type": "Constant", "volatility": 0.25}),
    ("a fixed surface", {"type": "Fixed", "surface": FIXED}),
    ("Dupire over a constant", {"type": "Dupire", "volatility": bvts.vol_spec_constant(R, 0.22)}),
    ("Dupire over a held surface", {"type": "Dupire", "volatility": HELD}),
]
UNEVEN = {"type": "Predefined", "x": [math.log(100.0) + d for d in (-1.2, -0.7, -0.4, -0.2, -0.1, -0.03, 0.0, 0.05,
                                                                    0.12, 0.25, 0.45, 0.8, 1.3)]}
EVEN = {"type": "Predefined", "x": [math.log(100.0) - 1.0 + 0.1 * i for i in range(21)]}


def op_spec(mesher, rf, q, local_vol, direction=0):
    return {"mesher": mesher, "spot": 100.0, "riskFree": rf, "dividend": q, "localVol": local_vol,
            "direction": direction}


def local_vol(spec, r, q):
    t = spec["type"]
    if t == "Constant":
        return ql.LocalConstantVol(R, spec["volatility"], ql.Actual365Fixed())
    if t == "Fixed":
        return flv.fixed_local_vol(spec["surface"])
    return ql.LocalVolSurface(ql.BlackVolTermStructureHandle(bvts.black_vol(spec["volatility"])),
                              ql.YieldTermStructureHandle(r), ql.YieldTermStructureHandle(q), 100.0)


def lv_op(spec):
    mesher, keep = mc.composite(spec["mesher"])
    r, q = bsp.curve(spec["riskFree"]), bsp.curve(spec["dividend"])
    lv = local_vol(spec["localVol"], r, q)
    op = ql.FdmLocalVolFwdOp(mesher, ql.SimpleQuote(spec["spot"]), r, q, lv, spec["direction"])
    return op, mesher, (keep, r, q, lv)


def specs():
    out = []
    p = bsp.PROCESSES[0][1]
    bs21 = mc.composite_spec(bsm.bs_spec(21, p, 1.0, 100.0))
    for lname, lspec in LOCAL_VOLS:
        out.append((f"{lname}, flat curves", op_spec(bs21, bsp.FLAT_R, bsp.FLAT_Q, lspec)))
        out.append((f"{lname}, zero curves", op_spec(bs21, bsp.ZERO_R, bsp.ZERO_Q, lspec)))
    out += [("constant 25%, an uneven predefined mesher",
             op_spec(mc.composite_spec(UNEVEN), bsp.FLAT_R, bsp.FLAT_Q, LOCAL_VOLS[0][1])),
            ("a fixed surface, an even predefined mesher",
             op_spec(mc.composite_spec(EVEN), bsp.ZERO_R, bsp.ZERO_Q, LOCAL_VOLS[1][1])),
            ("two dimensions", op_spec(mc.composite_spec(mc.U4, bsm.bs_spec(9, p, 1.0, 100.0)), bsp.FLAT_R,
                                       bsp.FLAT_Q, LOCAL_VOLS[3][1], direction=1))]
    return out


def mesher_outputs(spec):
    m = ql.Predefined1dMesher(spec["x"])
    n = m.size()
    return {"locations": v.enc(list(m.locations())), "dplus": v.enc([m.dplus(i) for i in range(n)]),
            "dminus": v.enc([m.dminus(i) for i in range(n)])}


def cases():
    st = v.settings(R)
    rng = random.Random(20261024)
    out = []
    for name, spec in specs():
        op, mesher, keep = lv_op(spec)
        r = [rng.uniform(-1.0, 1.0) for _ in range(mesher.layout().size())]
        out.append({"name": name, "inputs": {"settings": st, "op": spec, "r": r,
                                             "times": [list(t) for t in fwd.TIMES], "s": fwd.S},
                    "expected": fwd.op_outputs(op, spec, r)})
    for name, spec in (("predefined, uneven", UNEVEN), ("predefined, even", EVEN),
                       ("predefined, one point", {"type": "Predefined", "x": [4.6]})):
        out.append({"name": name, "inputs": {"settings": st, "mesher": spec}, "expected": mesher_outputs(spec)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmlocalvolfwdop.hpp", cases)
