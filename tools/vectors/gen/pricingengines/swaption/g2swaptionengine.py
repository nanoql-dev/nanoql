# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's G2SwaptionEngine (ql/pricingengines/swaption/g2swaptionengine.hpp) and the G2 model's
swaption pricing it calls (ql/models/shortrate/twofactormodels/g2.cpp's swaption, its SwaptionPricingFunction and
SegmentIntegral): 30 European swaptions of tools/vectors/gen/instruments/swaption.py's kind, on the evaluation date 15
January 2025, each priced on two G2 models with two integrations, and 2 refusals.

The swaptions are on Euribor 6M and 3M swaps, 6M to 5Y options on 2Y to 10Y swaps, payers and receivers in, at and out
of the money, nominals of 1 to 10 million, fixed legs of 1Y and 6M, a spread on the floating leg (whose correction the
engine takes from the swap's leg BPS on the model's curve), an overnight-indexed swap, and an expired swaption (worth
0). Every fixed leg is on Actual/365 (Fixed), the curve's day counter: on another, QuantLib's pricing function takes
each coupon as the rate times the difference of its payment times on the curve's day counter, where nanoql's takes the
fixed coupons' accrual periods; those swaptions, on Thirty/360 and Actual/360 fixed legs, are generated and left out
(tests/NanoQL.Tests/Divergences/G2AnalyticSwaptionCouponTests.cs).

Each swaption's output is its NPV ("NPV", an amount at the nominal) on each engine: G2 (a = 0.07, sigma = 0.011,
b = 0.3, eta = 0.008, rho = -0.6) and G2 (a = 0.1, sigma = 0.01, b = 0.1, eta = 0.01, rho = -0.75), each on the discount
curve (a flat 2.7%), with a range of 6 standard deviations and 16 intervals and of 4 and 64. The refusals: a
cash-settled swaption ("cash-settled swaptions not priced with G2 engine") and one on the par yield curve. Each case's
inputs hold the swaption (swaption.py's spec), the discount curve and the engines. The wheel's G2SwaptionEngine is the
proxy of the C++ constructor (QuantLib-SWIG 1.44, SWIG/swaption.i). Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/G2SwaptionEngineVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_swaption", os.path.join(v.GEN_ROOT, "instruments", "swaption.py"))
swp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(swp)
fvf, irx = swp.fvf, swp.irx

TODAY = swp.TODAY
M, Y = ql.Months, ql.Years
DISCOUNT = swp.DISCOUNT
ENGINES = [
    {"a": 0.07, "sigma": 0.011, "b": 0.3, "eta": 0.008, "rho": -0.6, "range": 6.0, "intervals": 16},
    {"a": 0.07, "sigma": 0.011, "b": 0.3, "eta": 0.008, "rho": -0.6, "range": 4.0, "intervals": 64},
    {"a": 0.1, "sigma": 0.01, "b": 0.1, "eta": 0.01, "rho": -0.75, "range": 6.0, "intervals": 16},
    {"a": 0.1, "sigma": 0.01, "b": 0.1, "eta": 0.01, "rho": -0.75, "range": 4.0, "intervals": 64},
]


def swaptions():
    """(name, spec, kept) for the 30 swaptions, the 6 generated and left out and the 2 refusals."""
    out = []
    atm = 0.031
    k = 0
    for exercise_tenor, length in (((1, Y), (5, Y)), ((2, Y), (3, Y)), ((6, M), (2, Y)), ((5, Y), (10, Y))):
        exercise, start, maturity = swp.dates_of(ql.Period(*exercise_tenor), ql.Period(*length))
        for swap_type in (swp.PAYER, swp.RECEIVER):
            for strike in (0.8 * atm, atm, 1.2 * atm):
                nominal = [1.0, 1e6, 1e7][k % 3]
                swap = swp.vanilla(swap_type, nominal, strike, start, maturity, fixed_dc=swp.A365)
                tag = f"{exercise_tenor[0]}{'MY'[exercise_tenor[1] == Y]} on {length[0]}Y / {swap_type} / {strike:.5f}"
                out.append((tag, swp.swaption_spec(swap, swp.european(exercise)), True))
                k += 1
    # 24 so far: fixed legs of 6M, a spread, the 3M index, an overnight-indexed swap, a long option, the expired one.
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    out.append(("6M fixed leg", swp.swaption_spec(swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity,
                                                              fixed_tenor=ql.Period(6, M), fixed_dc=swp.A365),
                                                  swp.european(exercise)), True))
    out.append(("floating spread 0.5%", swp.swaption_spec(swp.vanilla(swp.RECEIVER, 1e6, 0.03, start, maturity,
                                                                      spread=0.005, fixed_dc=swp.A365),
                                                          swp.european(exercise)), True))
    out.append(("on Euribor 3M", swp.swaption_spec(swp.vanilla(swp.PAYER, 1e6, 0.04, start, maturity, index=swp.E3M,
                                                               fixed_dc=swp.A365), swp.european(exercise)), True))
    ois = swp.fvf.ois("nominal", swp.PAYER, [1e6], fvf.sched(start, start + ql.Period(3, Y), ql.Period(1, Y),
                                                             swp.TARGET, ql.ModifiedFollowing),
                      0.03, swp.A365, swp.ESTR)
    out.append(("overnight-indexed", swp.swaption_spec(ois, swp.european(exercise)), True))
    e10, s10, m10 = swp.dates_of(ql.Period(10, Y), ql.Period(10, Y))
    out.append(("10Y on 10Y", swp.swaption_spec(swp.vanilla(swp.RECEIVER, 1e6, 0.03, s10, m10, fixed_dc=swp.A365),
                                                swp.european(e10)), True))
    past = swp.TARGET_CAL.advance(TODAY, -1, M)
    out.append(("expired", swp.swaption_spec(
        swp.vanilla(swp.PAYER, 1e6, 0.03, swp.TARGET_CAL.advance(past, 2, ql.Days),
                    swp.TARGET_CAL.advance(past, 2, ql.Days) + ql.Period(5, Y), fixed_dc=swp.A365),
        swp.european(past)), True))
    # the fixed legs off the curve's day counter, generated and left out
    for name, dc in (("Thirty/360", swp.THIRTY360_BB), ("Actual/360", swp.A360)):
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"left out: {name} fixed leg / {swap_type}", swp.swaption_spec(
                swp.vanilla(swap_type, 1e6, 0.03, start, maturity, fixed_dc=dc), swp.european(exercise)), False))
    out.append(("left out: Thirty/360 6M fixed leg", swp.swaption_spec(
        swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity, fixed_tenor=ql.Period(6, M)), swp.european(exercise)),
        False))
    out.append(("left out: Actual/360 overnight-indexed", swp.swaption_spec(
        swp.fvf.ois("nominal", swp.PAYER, [1e6], fvf.sched(start, start + ql.Period(3, Y), ql.Period(1, Y),
                                                           swp.TARGET, ql.ModifiedFollowing),
                    0.03, swp.A360, swp.ESTR), swp.european(exercise)), False))
    plain = swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity, fixed_dc=swp.A365)
    out.append(("refused: cash-settled", swp.swaption_spec(plain, swp.european(exercise), swp.CASH), True))
    out.append(("refused: par yield curve", swp.swaption_spec(plain, swp.european(exercise), swp.CASH, swp.PAR_YIELD),
                True))
    assert sum(1 for row in out if row[2]) == 32, len(out)
    return out


def engine_of(e):
    model = ql.G2(irx.handle(DISCOUNT), e["a"], e["sigma"], e["b"], e["eta"], e["rho"])
    return ql.G2SwaptionEngine(model, e["range"], e["intervals"])


def cases():
    out = []
    for name, spec, kept in swaptions():
        with v.case_settings(TODAY) as st:
            scale = fvf.scale_of(spec["swap"])
            npvs = []
            for e in ENGINES:
                swaption = swp.make_swaption(spec)
                swaption.setPricingEngine(engine_of(e))
                npvs.append(fvf.ibc.scaled(swaption.NPV, scale))
            if not kept:
                continue
            out.append({"name": name,
                        "inputs": {"settings": st, "swaption": spec, "discountCurve": DISCOUNT, "engines": ENGINES},
                        "expected": {"NPV": npvs}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/g2swaptionengine.hpp", cases)
