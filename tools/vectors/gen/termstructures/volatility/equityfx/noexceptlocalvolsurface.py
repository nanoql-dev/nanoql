# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NoExceptLocalVolSurface (ql/termstructures/volatility/equityfx/noexceptlocalvolsurface.hpp),
on the evaluation date 15 October 2026: Dupire's local volatility over four Black surfaces, two of them with calendar or
butterfly arbitrage where the local variance turns negative or the total variance falls, with overwrites of 0.2 and
0.35: its strike range and maximum date, and the local volatility at a grid of times and strikes, extrapolating, the
overwrite wherever LocalVolSurface refuses. Left out: the points near which QuantLib's Black variance, extrapolated in
strike, is negative, where nanoql's BlackVarianceSurface floors it at zero and its local volatility comes out 0
(S-eq's divergence, tests/NanoQL.Tests/Divergences/BlackVarianceSurfaceFloorTests.cs); the generator prints how many.

Every structure is dated 15 October 2026 on Actual/365 (Fixed): the curves' clock, which S-eq's LocalVolSurface reads
at the instants its times denote, is the surface's. The process specs are
tools/vectors/gen/processes/blackscholesprocess.py's. The wheel's call, by QuantLib-SWIG 1.44:
NoExceptLocalVolSurface(blackTS, riskFreeTS, dividendTS, underlying, illegalLocalVolOverwrite), the number form, and
localVol. Read back by tests/NanoQL.Tests/Vectors/TermStructures/NoExceptLocalVolSurfaceVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
bvts = bsp.bvts
R = bsp.R

CALENDAR_ARBITRAGE = bvts.vol_spec_surface(R, [91, 182, 365, 730], [70.0, 85.0, 100.0, 115.0, 130.0],
                                           [[0.40, 0.25, 0.30, 0.20], [0.35, 0.22, 0.27, 0.18],
                                            [0.30, 0.20, 0.25, 0.17], [0.32, 0.21, 0.26, 0.18],
                                            [0.36, 0.24, 0.29, 0.20]])
BUTTERFLY = bvts.vol_spec_surface(R, [91, 365, 730], [70.0, 90.0, 100.0, 110.0, 130.0],
                                  [[0.20, 0.21, 0.22], [0.45, 0.42, 0.40], [0.15, 0.16, 0.17], [0.45, 0.42, 0.40],
                                   [0.20, 0.21, 0.22]])
SURFACES = [
    ("a smooth surface, 0.2", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.SURFACE,
                                               bsp.FLAT_Q), 0.2),
    ("a smooth surface on zero curves, 0.35", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R,
                                                               bsp.SURFACE, bsp.ZERO_Q), 0.35),
    ("calendar arbitrage, 0.2", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, CALENDAR_ARBITRAGE,
                                                 bsp.FLAT_Q), 0.2),
    ("butterfly arbitrage, 0.35", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, BUTTERFLY,
                                                   bsp.FLAT_Q), 0.35),
]
TIMES = [0.0, 0.1, 0.25, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0]
STRIKES = [60.0, 75.0, 85.0, 95.0, 100.0, 105.0, 115.0, 125.0, 140.0]


def floored(black, t, k):
    """Whether QuantLib's Black variance is negative at or near a point, where nanoql's surface floors it."""
    return any(black.blackVariance(tt, kk, True) < 0.0
               for tt in (max(t - 1e-3, 0.0), t, t + 1e-3) for kk in (k * 0.999, k, k * 1.001))


def cases():
    out = []
    st = v.settings(R)
    for name, spec, overwrite in SURFACES:
        black = bvts.black_vol(spec["volatility"])
        black.enableExtrapolation()
        s = ql.NoExceptLocalVolSurface(ql.BlackVolTermStructureHandle(black),
                                       ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])),
                                       ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])), spec["x0"],
                                       overwrite)
        points = [[t, k] for t in TIMES for k in STRIKES if not floored(black, t, k)]
        print(f"{name}: {len(TIMES) * len(STRIKES) - len(points)} points left out")
        out.append({"name": name,
                    "inputs": {"settings": st, "process": spec, "overwrite": overwrite, "points": points},
                    "expected": v.enc({
                        "maxDate": s.maxDate(),
                        "minStrike": s.minStrike(),
                        "maxStrike": s.maxStrike(),
                        "localVol": [v.attempt(lambda t=t, k=k: s.localVol(t, k, True)) for t, k in points]})})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/noexceptlocalvolsurface.hpp", cases)
