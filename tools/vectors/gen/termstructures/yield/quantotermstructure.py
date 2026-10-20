# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's QuantoTermStructure (ql/termstructures/yield/quantotermstructure.hpp), on the evaluation
date 15 October 2026: the quanto-adjusted dividend curve q + r - r_f + rho sigma_S sigma_X.

Six curves, each from a dividend curve, domestic and foreign risk-free curves (flat forwards and linear zero
curves), the underlying's and the exchange rate's Black volatilities (constant, a variance curve and a variance
surface read at the strike and the exchange rate's level), and a correlation (negative, zero, positive): the reference
date, calendar, settlement days and day counter, which are the dividend curve's, the maximum date, the earliest of the
five structures', and the discount, a continuous zero rate and a simple forward rate at eight dates. All five
structures share one day counter: QuantLib reads every structure at the time the dividend curve's day counter
measures, so curves on different day counters are left out, where nanoql refuses them
(tests/NanoQL.Tests/Divergences/QuantoTermStructureDayCounterTests.cs).

The specs are tools/vectors/gen/termstructures/volatility/equityfx/blackvoltermstructure.py's and
tools/vectors/gen/processes/blackscholesprocess.py's. The wheel's calls, by QuantLib-SWIG 1.44's
termstructures.i: QuantoTermStructure(underlyingDividendTS, riskFreeTS, foreignRiskFreeTS, underlyingBlackVolTS,
strike, exchRateBlackVolTS, exchRateATMlevel, underlyingExchRateCorrelation), the C++ constructor on handles, and the
yield curve's members. Read back by tests/NanoQL.Tests/Vectors/TermStructures/QuantoTermStructureVectorTests.cs."""
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


bvts = _load("blackvoltermstructure", "termstructures/volatility/equityfx/blackvoltermstructure.py")
bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
R = bvts.EVALUATION_DATE
A360 = bvts.A360
FX_CURVE = bvts.vol_spec_curve(R, [182, 365, 730, 3650], [0.10, 0.11, 0.115, 0.12])
CASES = [
    ("flat, constant volatilities",
     bsp.flat_spec(R, 0.02), bsp.flat_spec(R, 0.04), bsp.flat_spec(R, 0.01),
     bvts.vol_spec_constant(R, 0.25), 100.0, bvts.vol_spec_constant(R, 0.12), 1.1, -0.3),
    ("zero curves, a variance curve for the exchange rate",
     bsp.ZERO_Q, bsp.ZERO_R, bsp.zero_spec(R, [0, 365, 1825, 3650], [0.005, 0.008, 0.012, 0.015]),
     bvts.vol_spec_constant(R, 0.30), 95.0, FX_CURVE, 1.2, 0.4),
    ("zero curves, a surface for the underlying",
     bsp.ZERO_Q, bsp.ZERO_R, bsp.flat_spec(R, 0.0),
     bvts.vol_spec_surface(R, [91, 365, 730, 1826], [70.0, 85.0, 100.0, 115.0, 130.0],
                           [[0.32, 0.30, 0.28, 0.27], [0.26, 0.25, 0.24, 0.235], [0.22, 0.215, 0.21, 0.205],
                            [0.21, 0.205, 0.20, 0.198], [0.23, 0.22, 0.215, 0.21]]),
     110.0, FX_CURVE, 0.95, 0.65),
    ("no correlation", bsp.flat_spec(R, 0.03), bsp.flat_spec(R, 0.05), bsp.flat_spec(R, 0.02),
     bvts.vol_spec_constant(R, 0.4), 100.0, bvts.vol_spec_constant(R, 0.2), 1.0, 0.0),
    ("Actual/360 throughout", bsp.flat_spec(R, 0.0, A360), bsp.flat_spec(R, 0.0, A360),
     bsp.flat_spec(R, 0.0, A360), bvts.vol_spec_constant(R, 0.2, A360), 100.0, bvts.vol_spec_constant(R, 0.1, A360),
     1.0, 0.3),
    ("a short exchange-rate curve sets the maximum date",
     bsp.flat_spec(R, 0.02), bsp.flat_spec(R, 0.04), bsp.flat_spec(R, 0.01), bvts.vol_spec_constant(R, 0.25), 100.0,
     bvts.vol_spec_curve(R, [182, 730], [0.10, 0.12]), 1.1, -0.5),
]
DATE_OFFSETS = [0, 1, 30, 182, 365, 700, 1825, 3650]


def cases():
    out = []
    st = v.settings(R)
    for name, q, r, rf, svol, strike, xvol, level, rho in CASES:
        c = ql.QuantoTermStructure(ql.YieldTermStructureHandle(bsp.curve(q)), ql.YieldTermStructureHandle(bsp.curve(r)),
                                   ql.YieldTermStructureHandle(bsp.curve(rf)),
                                   ql.BlackVolTermStructureHandle(bvts.black_vol(svol)), strike,
                                   ql.BlackVolTermStructureHandle(bvts.black_vol(xvol)), level, rho)
        c.enableExtrapolation()
        dates = [R + d for d in DATE_OFFSETS]
        out.append({
            "name": name,
            "inputs": {"settings": st, "dividend": q, "riskFree": r, "foreignRiskFree": rf,
                       "underlyingVolatility": svol, "strike": strike, "exchangeRateVolatility": xvol,
                       "exchangeRateLevel": level, "correlation": rho,
                       "dates": [d.serialNumber() for d in dates]},
            "expected": v.enc({
                "referenceDate": c.referenceDate(),
                "calendarEmpty": c.calendar().empty(),
                "dayCounter": c.dayCounter().name(),
                "maxDate": c.maxDate(),
                "discount": [c.discount(d) for d in dates],
                "zeroRate": [c.zeroRate(d, c.dayCounter(), ql.Continuous).rate() for d in dates],
                "forwardRate": [c.forwardRate(d, d + 91, c.dayCounter(), ql.Simple).rate() for d in dates]})})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/quantotermstructure.hpp", cases)
