# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticTwoAssetBarrierEngine (ql/pricingengines/barrier/analytictwoassetbarrierengine.hpp,
.cpp), on the evaluation date 15 October 2026: European options on a first asset knocked in or out by a second at a
barrier, by Haug's closed form.

Each of the four barrier types at two barriers on each side of the second asset's spot (85 and 95%, 105 and 115% of
it), over six months and two years, on pairs of Black-Scholes-Merton processes sharing a flat or a zero risk-free curve,
the first asset at 100 with a dividend yield of 0 or 3% and a constant volatility or a variance curve, the second at
5,000 with its own, at correlations of -0.5, 0 and 0.5, each a call and a put on the first at three strikes (90, 100
and 110): the NPV, the engine's one result. Then an expired option and the refusals: a cash-or-nothing payoff and the
second spot beyond the barrier ("barrier touched").

QuantLib's engine reads Drezner's bivariate normal, nanoql's West's, so every value carries §3.3's override: Drezner's
accuracy, 6e-7 times the bivariate terms' coefficients, S1 e^((b1-r)T)(1 + (H/S2)^(2(mu2+rho sigma1 sigma2)/sigma2^2)) +
X e^(-rT)(1 + (H/S2)^(2 mu2/sigma2^2)), relative to the value.

The outputs nanoql fixes are left out: the two processes share one risk-free curve, on which QuantLib's b2 = r1 - q2 is
r2 - q2 (tests/NanoQL.Tests/Divergences/TwoAssetBarrierTwoCurvesTests.cs), and one day counter and reference date
(BarrierClosedFormMismatchedTimesTests.cs); a knock-in is written only on a first asset without dividends, where
QuantLib's vanilla is right (TwoAssetBarrierDividendTests.cs); and every volatility is strike-independent, where
QuantLib's read of the second at the first's strike is nanoql's at the barrier (TwoAssetBarrierSmileTests.cs).

A case holds the two process specs (tools/vectors/gen/processes/blackscholesprocess.py's), the correlation, the
exercise and barrier (tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's, the rebate unread), its
outputs one per payoff. The wheel's calls, by QuantLib-SWIG 1.44's options.i: TwoAssetBarrierOption(barrierType,
barrier, payoff, exercise), AnalyticTwoAssetBarrierEngine(process1, process2, QuoteHandle(SimpleQuote(rho))) and the
option's NPV. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/Barrier/AnalyticTwoAssetBarrierEngineVectorTests.cs."""
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


abe = _load("analyticbarrierengine", "pricingengines/barrier/analyticbarrierengine.py")
bsp, bc = abe.bsp, abe.bc
R = abe.R
CALL, PUT = abe.CALL, abe.PUT
DRZ = 6e-7
REASON = "QuantLib's engine reads Dr78, nanoql's We04DP; 6e-7 times the bivariate terms' coefficients"
CURVES = [("flat", bsp.FLAT_R, bsp.FLAT_Q), ("zero curves", bsp.ZERO_R, bsp.ZERO_Q)]
VOLS = [("constant", bsp.CONSTANT), ("variance curve", bsp.CURVE)]


def coefficients(p1, p2, barrier, strike, rho, last):
    t = p1.riskFreeRate().timeFromReference(last)
    s1, s2 = p1.x0(), p2.x0()
    r = p1.riskFreeRate().zeroRate(t, ql.Continuous, ql.NoFrequency).rate()
    b1 = r - p1.dividendYield().zeroRate(t, ql.Continuous, ql.NoFrequency).rate()
    b2 = r - p2.dividendYield().zeroRate(t, ql.Continuous, ql.NoFrequency).rate()
    sigma1 = p1.blackVolatility().blackVol(t, strike)
    sigma2 = p2.blackVolatility().blackVol(t, strike)
    mu2 = b2 - sigma2 * sigma2 / 2.0
    log_hs = math.log(barrier / s2)
    return (s1 * math.exp((b1 - r) * t) * (1 + math.exp(2 * (mu2 + rho * sigma1 * sigma2) * log_hs / (sigma2 * sigma2)))
            + strike * math.exp(-r * t) * (1 + math.exp(2 * mu2 * log_hs / (sigma2 * sigma2))))


def cases():
    st = v.settings(R)
    out = []

    def add(name, p1spec, p2spec, rho, ex, barrier, specs, overridden=True):
        p1, p2 = bsp.process(p1spec), bsp.process(p2spec)
        engine = ql.AnalyticTwoAssetBarrierEngine(p1, p2, ql.QuoteHandle(ql.SimpleQuote(rho)))
        kept, values = [], []
        for spec in specs:
            o = ql.TwoAssetBarrierOption(barrier["barrierType"], barrier["barrier"], bc.payoff(spec), abe.exercise(ex))
            o.setPricingEngine(engine)
            value = v.attempt(o.NPV)
            if value == "NaN":
                continue
            if overridden and isinstance(value, float):
                c = coefficients(p1, p2, barrier["barrier"], spec["strike"], rho, ql.Date(ex["date"]))
                value = {"value": value, "tolerance": DRZ * c / max(abs(value), 1.0), "reason": REASON}
            kept.append(spec)
            values.append(value)
        assert kept, name
        out.append({"name": name,
                    "inputs": {"settings": st, "process1": p1spec, "process2": p2spec, "correlation": rho,
                               "exercise": ex, "barrier": barrier, "payoffs": kept},
                    "expected": {"NPV": values}})

    for cname, risk_free, dividend in CURVES:
        for vname, vol in VOLS:
            p2spec = bsp.process_spec("BlackScholesMertonProcess", 5000.0, risk_free, bsp.bvts.vol_spec_constant(R, 0.3),
                                      dividend)
            for q1 in (0.0, 0.03):
                p1spec = bsp.process_spec("BlackScholesMertonProcess", 100.0, risk_free, vol, bsp.flat_spec(R, q1))
                for days in (182, 730):
                    ex = abe.exercise_spec("European", days)
                    for tname, t in abe.TYPES:
                        if q1 != 0.0 and t in (abe.DOWN_IN, abe.UP_IN):
                            continue
                        for level in abe.barriers(5000.0, t):
                            for rho in (-0.5, 0.0, 0.5):
                                add(f"{cname}, {vname}, q1 {q1:g}, {days} days, {tname} at {level:g}, rho {rho:g}",
                                    p1spec, p2spec, rho, ex, abe.barrier_spec(t, level, 0.0), abe.payoffs(100.0))
    p1spec = bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q)
    p2spec = bsp.process_spec("BlackScholesMertonProcess", 5000.0, bsp.FLAT_R, bsp.bvts.vol_spec_constant(R, 0.3),
                              bsp.FLAT_Q)
    ex = abe.exercise_spec("European", 365)
    add("expired", p1spec, p2spec, 0.5, abe.exercise_spec("European", -1), abe.barrier_spec(abe.DOWN_OUT, 4500.0, 0.0),
        abe.payoffs(100.0), overridden=False)
    add("a cash-or-nothing payoff", p1spec, p2spec, 0.5, ex, abe.barrier_spec(abe.DOWN_OUT, 4500.0, 0.0),
        [bc.payoff_spec("CashOrNothing", CALL, 100.0)], overridden=False)
    for tname, t in abe.TYPES:
        level = 5250.0 if t in (abe.DOWN_IN, abe.DOWN_OUT) else 4750.0
        add(f"{tname}, the second spot beyond the barrier", p1spec, p2spec, 0.5, ex, abe.barrier_spec(t, level, 0.0),
            abe.payoffs(100.0)[:1], overridden=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/analytictwoassetbarrierengine.hpp", cases)
