# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GBSMRNDCalculator (ql/methods/finitedifferences/utilities/gbsmrndcalculator.hpp, .cpp): the
density, cumulative distribution and inverse of the spot by Breeden and Litzenberger under Black-Scholes-Merton
processes with a strike-dependent volatility, on flat and zero curves, with a constant volatility, a variance curve and
two variance surfaces (one bilinear, one bicubic with constant strike extrapolation), each surface extrapolating, at
times from a quarter to four years: the density and distribution at strikes from 0.5 to 2 times the forward, the inverse
at probabilities from 1e-3 to 0.999.

**The stability filter** (the S7 plan's S7R6, the S-van plan's VD17) on the inverse, which QuantLib solves with Brent:
each is solved again with its probability moved one ulp up and one down (math.nextafter), and a point is written only
where both solves give its strike within 1e-10 × max(1, |base|, |moved|), Brent's own accuracy. The generator prints the
points dropped.

Left out (the S-van plan's §2.10): structures on another clock, which nanoql reads at the instants the risk-free curve's
times denote and QuantLib at those times (VR4 (xxiii); tests/NanoQL.Tests/Divergences/LocalVolMismatchedTimesTests.cs):
every structure here is dated 15 October 2026 on Actual/365 (Fixed). The process and structure specs are
tools/vectors/gen/processes/blackscholesprocess.py's; "extrapolate" enables the surface's extrapolation. The wheel's
calls, by QuantLib-SWIG 1.44: GBSMRNDCalculator(process) and its pdf, cdf and invcdf. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/GBSMRNDCalculatorVectorTests.cs."""
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
bvts = bsp.bvts
R = bsp.R
TOLERANCE = 1e-10
BICUBIC = bvts.vol_spec_surface(R, [91, 182, 365, 730, 1826], [70.0, 90.0, 100.0, 110.0, 130.0],
                                [[0.34, 0.32, 0.31, 0.30, 0.29], [0.27, 0.26, 0.255, 0.25, 0.245],
                                 [0.24, 0.235, 0.23, 0.228, 0.226], [0.23, 0.225, 0.222, 0.22, 0.218],
                                 [0.26, 0.25, 0.245, 0.24, 0.235]],
                                lower=ql.BlackVarianceSurface.ConstantExtrapolation,
                                upper=ql.BlackVarianceSurface.ConstantExtrapolation, interpolation="bicubic")
PROCESSES = [
    ("flat curves, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT,
                                               bsp.FLAT_Q), False),
    ("zero curves, variance curve", bsp.process_spec("BlackScholesMertonProcess", 95.0, bsp.ZERO_R, bsp.CURVE,
                                                     bsp.ZERO_Q), False),
    ("zero curves, surface", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.SURFACE,
                                              bsp.ZERO_Q), True),
    ("flat curves, bicubic surface", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, BICUBIC,
                                                      bsp.FLAT_Q), True),
]
TIMES = [0.25, 1.0, 2.0, 4.0]
RELATIVE = [0.5, 0.7, 0.85, 0.95, 1.0, 1.05, 1.2, 1.5, 2.0]
PROBABILITIES = [1e-3, 0.01, 0.1, 0.25, 0.5, 0.75, 0.9, 0.99, 0.999]
DROPPED = []


def calculator(spec, extrapolate):
    vol = bvts.black_vol(spec["volatility"])
    if extrapolate:
        vol.enableExtrapolation()
    process = ql.BlackScholesMertonProcess(ql.QuoteHandle(ql.SimpleQuote(spec["x0"])),
                                           ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])),
                                           ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])),
                                           ql.BlackVolTermStructureHandle(vol))
    return process, ql.GBSMRNDCalculator(process)


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
    st = v.settings(R)
    out = []
    for name, spec, extrapolate in PROCESSES:
        process, calc = calculator(spec, extrapolate)
        points = []
        for t in TIMES:
            fwd = spec["x0"] * process.dividendYield().discount(t) / process.riskFreeRate().discount(t)
            points += [[t, fwd * r] for r in RELATIVE]
        inverse = []
        for t in TIMES:
            for p in PROBABILITIES:
                try:
                    base = calc.invcdf(p, t)
                except RuntimeError:
                    inverse.append([t, p, dict(v.THROWS)])
                    continue
                if stable(calc, t, p, base):
                    inverse.append([t, p, base])
                else:
                    DROPPED.append((name, t, p))
        out.append({"name": name,
                    "inputs": {"settings": st, "process": spec, "extrapolate": extrapolate, "points": points,
                               "invcdfPoints": [[t, p] for t, p, _ in inverse]},
                    "expected": {
                        "pdf": [v.attempt(lambda t=t, k=k: calc.pdf(k, t)) for t, k in points],
                        "cdf": [v.attempt(lambda t=t, k=k: calc.cdf(k, t)) for t, k in points],
                        "invcdf": [v.enc(y) for _, _, y in inverse]}})
    print(f"dropped by the stability filter: {DROPPED}")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/gbsmrndcalculator.hpp", cases)
