# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LocalVolRNDCalculator (ql/methods/finitedifferences/utilities/localvolrndcalculator.hpp,
.cpp), on the evaluation date 15 October 2026: the forward (Fokker-Planck) equation's density of the log spot under
three local volatilities, a flat 25% and a smile as FixedLocalVolSurfaces on wide strikes and Dupire's surface over a
Black surface held flat outside its strikes (NoExceptLocalVolSurface, extrapolating), on flat and zero curves, at three
grids (101 x 51 by default, 81 x 41 with a Gaussian first step of 0.01, 61 x 21): the density and distribution at log
spots around the log forward at times inside the grid, the inverse at probabilities from 1e-3 to 0.999, the grid at
five of its times, and the steps at which it widened.

**The stability filter** (the S7 plan's S7R6, the S-van plan's VD17) on the inverse, which QuantLib solves with Brent:
each is solved again with its probability moved one ulp up and one down (math.nextafter), and a point is written only
where both solves agree within 1e-8 × max(1, |base|, |moved|), the solver's accuracy. The generator prints the points
dropped.

Left out (the S-van plan's §2.10): structures on another clock, which nanoql reads at the instants the local
volatility's times denote and QuantLib at those times (VR4 (xxiii); tests/NanoQL.Tests/Divergences/
LocalVolMismatchedTimesTests.cs): every structure here is dated 15 October 2026 on Actual/365 (Fixed). The fixed
surfaces extrapolate by their interpolator on both sides, past VR4 (xviii)'s reach, and the Black surface is held flat
in strike, past S-eq's variance floor. The specs are fixedlocalvolsurface.py's and blackscholesprocess.py's. The
wheel's calls, by QuantLib-SWIG 1.44: LocalVolRNDCalculator(spot, rTS, qTS, localVol, xGrid, tGrid, x0Density,
localVolProbEps, maxIter, gaussianStepSize) and its pdf, cdf, invcdf, mesher and rescaleTimeSteps. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/LocalVolRNDCalculatorVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
flv = _load("fixedlocalvolsurface", "termstructures/volatility/equityfx/fixedlocalvolsurface.py")
bvts = bsp.bvts
R = bsp.R
TOLERANCE = 1e-8
WIDE = [1.0, 20.0, 50.0, 80.0, 100.0, 125.0, 200.0, 500.0, 10000.0]
FLAT = flv.surface_spec("times", [0.25, 0.5, 1.0, 2.0], WIDE, [[0.25] * 4 for _ in WIDE], lower=flv.INTERP,
                        upper=flv.INTERP)
SMILE = flv.surface_spec("times", [0.25, 0.5, 1.0, 2.0], WIDE,
                         [[0.45, 0.43, 0.41, 0.40], [0.38, 0.36, 0.35, 0.34], [0.30, 0.29, 0.28, 0.275],
                          [0.26, 0.255, 0.25, 0.245], [0.24, 0.235, 0.23, 0.228], [0.23, 0.225, 0.222, 0.22],
                          [0.25, 0.245, 0.24, 0.235], [0.30, 0.29, 0.28, 0.27], [0.35, 0.34, 0.33, 0.32]],
                         lower=flv.INTERP, upper=flv.INTERP)
HELD = bvts.vol_spec_surface(R, [91, 365, 730], [70.0, 85.0, 100.0, 115.0, 130.0],
                             [[0.32, 0.30, 0.28], [0.26, 0.25, 0.24], [0.22, 0.215, 0.21], [0.21, 0.205, 0.20],
                              [0.23, 0.22, 0.215]],
                             lower=ql.BlackVarianceSurface.ConstantExtrapolation,
                             upper=ql.BlackVarianceSurface.ConstantExtrapolation)
CASES = [
    ("flat 25%, flat curves", {"type": "Fixed", "surface": FLAT}, bsp.flat_spec(R, 0.015), bsp.flat_spec(R, 0.025),
     [101, 51, 0.1, 1e-6, 10000, None]),
    ("a smile, zero curves, a Gaussian first step", {"type": "Fixed", "surface": SMILE}, bsp.ZERO_R, bsp.ZERO_Q,
     [81, 41, 0.1, 1e-6, 10000, 0.01]),
    ("Dupire over a held surface", {"type": "NoExcept", "volatility": HELD, "overwrite": 0.25}, bsp.FLAT_R, bsp.FLAT_Q,
     [61, 21, 0.1, 1e-6, 10000, None]),
]
DROPPED = []


def local_vol(spec, r, q):
    if spec["type"] == "Fixed":
        return flv.fixed_local_vol(spec["surface"])
    black = bvts.black_vol(spec["volatility"])
    s = ql.NoExceptLocalVolSurface(ql.BlackVolTermStructureHandle(black), ql.YieldTermStructureHandle(r),
                                   ql.YieldTermStructureHandle(q), 100.0, spec["overwrite"])
    s.enableExtrapolation()
    return s


def stable(calc, t, p, base):
    for moved in (math.nextafter(p, math.inf), math.nextafter(p, -math.inf)):
        try:
            m = calc.invcdf(moved, t)
        except RuntimeError:
            return False
        if abs(m - base) > TOLERANCE * max(1.0, abs(base), abs(m)):
            return False
    return True


def cases():
    out = []
    st = v.settings(R)
    for name, lspec, rspec, qspec, grid in CASES:
        r, q = bsp.curve(rspec), bsp.curve(qspec)
        lv = local_vol(lspec, r, q)
        xGrid, tGrid, x0Density, eps, maxIter, gaussian = grid
        args = [ql.SimpleQuote(100.0), r, q, lv, xGrid, tGrid, x0Density, eps, maxIter]
        if gaussian is not None:
            args.append(gaussian)
        calc = ql.LocalVolRNDCalculator(*args)
        horizon = lv.maxTime()
        times = [horizon * f for f in (0.002, 0.01, 0.1, 0.33, 0.5, 0.77, 1.0)]
        points = []
        for t in times:
            fwd = 100.0 * q.discount(t) / r.discount(t)
            sd = 0.3 * math.sqrt(t)
            points += [[t, math.log(fwd) + s * sd] for s in (-2.5, -1.0, -0.3, 0.0, 0.4, 1.2, 2.5)]
        inverse = []
        for t in times[2:]:
            for p in (1e-3, 0.05, 0.25, 0.5, 0.75, 0.95, 0.999):
                try:
                    base = calc.invcdf(p, t)
                except RuntimeError:
                    inverse.append([t, p, dict(v.THROWS)])
                    continue
                if stable(calc, t, p, base):
                    inverse.append([t, p, base])
                else:
                    DROPPED.append((name, t, p))
        mesher_times = [0.0] + [horizon / tGrid * i for i in (1, tGrid // 4, tGrid // 2, tGrid)]
        out.append({"name": name,
                    "inputs": {"settings": st, "localVol": lspec, "riskFree": rspec, "dividend": qspec,
                               "spot": 100.0, "grid": grid, "points": points,
                               "invcdfPoints": [[t, p] for t, p, _ in inverse], "mesherTimes": mesher_times},
                    "expected": v.enc({
                        "pdf": [v.attempt(lambda t=t, x=x: calc.pdf(x, t)) for t, x in points],
                        "cdf": [v.attempt(lambda t=t, x=x: calc.cdf(x, t)) for t, x in points],
                        "invcdf": [y for _, _, y in inverse],
                        "mesher": [list(calc.mesher(t).locations()) for t in mesher_times],
                        "rescaleTimeSteps": list(calc.rescaleTimeSteps())})})
    print(f"dropped by the stability filter: {DROPPED}")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/localvolrndcalculator.hpp", cases)
