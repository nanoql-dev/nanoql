# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GridModelLocalVolSurface (ql/termstructures/volatility/equityfx/gridmodellocalvolsurface.hpp,
.cpp), on the evaluation date 15 October 2026: four grids (three and four dates, four and five strikes per date, each
strike extrapolation on either side), each at its initial parameters (all 1) and at two parameter vectors set through
setParams: the parameters, the maximum date and time, the strike range, and the local volatility at a grid of times
and strikes, extrapolating. The parameter vectors' refusals: one too few and one too many.

Left out: as in tools/vectors/gen/termstructures/volatility/equityfx/fixedlocalvolsurface.py, at a node time (or one
the grid clamps onto) a strike outside the node's strikes on a side with the constant extrapolation. The grid's
calibration isn't run here: QuantLib's cases calibrate it, and its outputs pass through the optimiser. The wheel's
calls, by QuantLib-SWIG 1.44: GridModelLocalVolSurface(referenceDate, dates, strikes, dayCounter, lower, upper),
params, setParams and localVol. nanoql's WithParams returns a copy, which the test reads. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/GridModelLocalVolSurfaceVectorTests.cs."""
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


flv = _load("fixedlocalvolsurface", "termstructures/volatility/equityfx/fixedlocalvolsurface.py")
bvts = flv.bvts
R = flv.R
A365 = bvts.A365
CONST, INTERP = flv.CONST, flv.INTERP

GRIDS = [
    ("three dates, four strikes, constant", [91, 365, 730], [[80.0, 95.0, 105.0, 120.0]] * 3, CONST, CONST),
    ("three dates, widening strikes, constant below", [91, 365, 730],
     [[85.0, 95.0, 105.0, 115.0], [75.0, 92.0, 108.0, 125.0], [65.0, 90.0, 110.0, 135.0]], CONST, INTERP),
    ("four dates, five strikes, the interpolator's", [30, 182, 365, 1095],
     [[90.0, 95.0, 100.0, 105.0, 110.0], [80.0, 90.0, 100.0, 110.0, 120.0], [70.0, 85.0, 100.0, 115.0, 130.0],
      [55.0, 75.0, 100.0, 130.0, 160.0]], INTERP, INTERP),
    ("four dates, five strikes, constant above", [30, 182, 365, 1095],
     [[90.0, 95.0, 100.0, 105.0, 110.0], [80.0, 90.0, 100.0, 110.0, 120.0], [70.0, 85.0, 100.0, 115.0, 130.0],
      [55.0, 75.0, 100.0, 130.0, 160.0]], INTERP, CONST),
]
TIMES = [0.0, 0.05, 0.0821917808219178, 0.25, 0.4986301369863014, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0]
STRIKES = [50.0, 70.0, 85.0, 95.0, 100.0, 107.5, 120.0, 140.0, 180.0]


def parameter_sets(n):
    return [[0.2 + 0.01 * ((7 * i) % 11) for i in range(n)], [0.35 - 0.005 * i for i in range(n)]]


def cases():
    out = []
    st = v.settings(R)
    dc = bvts.day_counter(A365)
    for name, offsets, strikes, lower, upper in GRIDS:
        dates = [R + d for d in offsets]
        s = ql.GridModelLocalVolSurface(R, dates, strikes, dc, lower, upper)
        times = [dc.yearFraction(R, d) for d in dates]
        spec = {"form": "perTimeStrikes", "strikes": strikes, "lower": lower, "upper": upper}
        n = len(s.params())
        sets = [None] + parameter_sets(n)
        for j, params in enumerate(sets):
            if params is not None:
                s.setParams(params)
            points = [[t, k] for t in TIMES for k in STRIKES if not flv.diverges(spec, times, t, k)]
            out.append({"name": f"{name}, {'initial parameters' if params is None else f'parameter set {j}'}",
                        "inputs": {"settings": st, "dates": v.enc(dates), "strikes": strikes, "dayCounter": A365,
                                   "lower": lower, "upper": upper, "params": params, "points": points},
                        "expected": v.enc({
                            "params": list(s.params()),
                            "maxDate": s.maxDate(),
                            "maxTime": s.maxTime(),
                            "minStrike": s.minStrike(),
                            "maxStrike": s.maxStrike(),
                            "localVol": [v.attempt(lambda t=t, k=k: s.localVol(t, k, True)) for t, k in points]})})
    name, offsets, strikes, lower, upper = GRIDS[0]
    dates = [R + d for d in offsets]
    for label, size in (("one parameter too few", 11), ("one parameter too many", 13)):
        s = ql.GridModelLocalVolSurface(R, dates, strikes, dc, lower, upper)
        out.append({"name": label,
                    "inputs": {"settings": st, "dates": v.enc(dates), "strikes": strikes, "dayCounter": A365,
                               "lower": lower, "upper": upper, "params": [0.25] * size},
                    "expected": {"setParams": v.attempt(lambda s=s, size=size: s.setParams([0.25] * size))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/gridmodellocalvolsurface.hpp", cases)
