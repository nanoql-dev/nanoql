# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Gaussian1dNonstandardSwaptionEngine (ql/pricingengines/swaption/
gaussian1dnonstandardswaptionengine.hpp, .cpp): European and Bermudan nonstandard swaptions on the evaluation date 15
January 2025, each priced on gaussian1dswaptionengine.py's three GSR models, with that generator's engine settings
varied on the first, an option-adjusted spread of 20bp, and the engine's probabilities, naive and digital.

The swaptions (instruments/nonstandardswap.py's swaps, on Euribor 6M forecast on a flat 3.1%): constant terms, payer and
receiver, European and Bermudan; amortizing nominals with step-up rates, gearings and spreads, Bermudan; the same with
both capital exchanges, whose redemptions the exercise takes with their coupons; a Bermudan exercise with a rebate per
date paid two TARGET days after it (RebatedExercise); an overnight index (SOFR's conventions) paid two days late; an
exercise on the evaluation date, one past, and a Bermudan whose first exercise has passed. NPVs are compared at the
largest nominal, the probabilities as they are.

Left out, each a documented divergence: a discount or forwarding curve on another clock
(Gaussian1dMismatchedTimesTests), so SOFR is forecast here on an Actual/365 (Fixed) curve; flat payoff extrapolation
(FlatPayoffExtrapolationTests), reversions between 0 and 1e-2, and fixed nominals positive but none above 1e-8 (#2845's
class, NonstandardInitialGuessTests). The wheel's Gaussian1dNonstandardSwaptionEngine(model, integrationPoints, stddevs,
extrapolatePayoff, flatPayoffExtrapolation, oas, discountCurve, probabilities) is the C++ constructor's proxy, its
probabilities read by the swaption's probabilities(). Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Gaussian1dNonstandardSwaptionEngineVectorTests.cs."""
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


g1d = _load("wheel_gaussian1dswaptionengine", "pricingengines/swaption/gaussian1dswaptionengine.py")
nss = _load("wheel_nonstandardswap", "instruments/nonstandardswap.py")
swp = g1d.swp
TODAY = g1d.TODAY
TARGET_CAL = g1d.TARGET_CAL
D, M, Y = ql.Days, ql.Months, ql.Years
MODELS, SETTINGS = g1d.MODELS, g1d.SETTINGS
OAS = 0.002
SOFR_A365 = nss.irx.overnight_spec("SOFR", 0, nss.US, nss.A360, nss.fvf.curve(0.043))


def rebated(exercise, rebates, days=2, calendar=nss.TARGET, convention=ql.Following):
    return {"type": "RebatedExercise", "exercise": exercise, "rebates": list(rebates), "rebateSettlementDays": days,
            "rebatePaymentCalendar": calendar, "rebatePaymentConvention": int(convention)}


def make_exercise(spec):
    if spec["type"] == "RebatedExercise":
        return ql.RebatedExercise(swp.make_exercise(spec["exercise"]), spec["rebates"], spec["rebateSettlementDays"],
                                  g1d.irx.calendar(spec["rebatePaymentCalendar"]), spec["rebatePaymentConvention"])
    return swp.make_exercise(spec)


def swaption_spec(swap, exercise):
    return {"swap": swap, "exercise": exercise, "settlementType": swp.PHYSICAL, "settlementMethod": swp.OTC}


def make_swaption(spec):
    return ql.NonstandardSwaption(nss.make(spec["swap"]), make_exercise(spec["exercise"]), spec["settlementType"],
                                  spec["settlementMethod"])


def engine(model, settings=None, oas=None, probabilities=None):
    s = settings or {"integrationPoints": 64, "stddevs": 7.0, "extrapolatePayoff": True, "discountCurve": None}
    curve = ql.YieldTermStructureHandle() if s["discountCurve"] is None else g1d.irx.handle(s["discountCurve"])
    quote = ql.QuoteHandle() if oas is None else ql.QuoteHandle(ql.SimpleQuote(oas))
    probs = ql.Gaussian1dNonstandardSwaptionEngine.NoProb if probabilities is None else probabilities
    return ql.Gaussian1dNonstandardSwaptionEngine(model, s["integrationPoints"], s["stddevs"], s["extrapolatePayoff"],
                                                  False, quote, curve, probs)


def annual(first, count):
    return [TARGET_CAL.advance(first, i, Y) for i in range(count)]


def swaptions():
    out = []
    exercise1, start1, _ = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    exercise2, start2, _ = swp.dates_of(ql.Period(2, Y), ql.Period(3, Y))
    for swap_type, name in ((ql.Swap.Payer, "payer"), (ql.Swap.Receiver, "receiver")):
        out.append((f"constant terms, European, {name}",
                    swaption_spec(nss.constant(swap_type, 1e6, 0.03, start1, 5), swp.european(exercise1))))
        out.append((f"constant terms, Bermudan, {name}",
                    swaption_spec(nss.constant(swap_type, 1e6, 0.031, start1, 5), swp.bermudan(annual(exercise1, 4)))))
    amortizing = nss.swaps()[1][1]
    exchanged = nss.swaps()[2][1]
    # SOFR forecast on a curve of the model's clock (Actual/365 (Fixed)), not instruments/nonstandardswap.py's Actual/360
    sofr = {**nss.swaps()[6][1], "index": SOFR_A365}
    first = TARGET_CAL.advance(TODAY, 1, Y)
    out.append(("amortizing, step-up, gearings and spreads, Bermudan",
                swaption_spec(amortizing, swp.bermudan(annual(first, 4)))))
    out.append(("both capital exchanges, Bermudan", swaption_spec(exchanged, swp.bermudan(annual(first, 4)))))
    out.append(("a rebate per exercise date, Bermudan",
                swaption_spec(nss.constant(ql.Swap.Payer, 1e6, 0.033, start1, 5),
                              rebated(swp.bermudan(annual(exercise1, 4)), [1000.0, -500.0, 2500.0, 0.0]))))
    out.append(("SOFR paid two days late, European",
                swaption_spec(sofr, swp.european(TARGET_CAL.advance(first, -2, D)))))
    out.append(("constant terms, European on a 3Y swap from 2Y",
                swaption_spec(nss.constant(ql.Swap.Payer, 1e7, 0.027, start2, 3), swp.european(exercise2))))
    start0 = TARGET_CAL.advance(TODAY, 2, D)
    out.append(("exercised on the evaluation date",
                swaption_spec(nss.constant(ql.Swap.Receiver, 1e6, 0.03, start0, 5), swp.european(TODAY))))
    past = TARGET_CAL.advance(TODAY, -1, M)
    out.append(("expired a month ago",
                swaption_spec(nss.constant(ql.Swap.Payer, 1e6, 0.03, TARGET_CAL.advance(past, 2, D), 5),
                              swp.european(past))))
    out.append(("Bermudan, first exercise past",
                swaption_spec(nss.constant(ql.Swap.Payer, 1e6, 0.03, start1, 5),
                              swp.bermudan([TARGET_CAL.advance(TODAY, -1, M), exercise1,
                                            TARGET_CAL.advance(exercise1, 1, Y)]))))
    return out


def swaption_case(name, spec):
    with v.case_settings(TODAY) as st:
        scale = nss.scale_of(spec["swap"])
        swaption = make_swaption(spec)
        expected = {"gsr": [], "settings": []}
        for m in MODELS:
            swaption.setPricingEngine(engine(g1d.make_gsr(m)))
            expected["gsr"].append(v.amount(v.result(swaption.NPV), scale))
        model = g1d.make_gsr(MODELS[0])
        for s in SETTINGS:
            swaption.setPricingEngine(engine(model, s))
            expected["settings"].append(v.amount(v.result(swaption.NPV), scale))
        swaption.setPricingEngine(engine(model, oas=OAS))
        expected["oas"] = v.amount(v.result(swaption.NPV), scale)
        for label, kind in (("naive", ql.Gaussian1dNonstandardSwaptionEngine.Naive),
                            ("digital", ql.Gaussian1dNonstandardSwaptionEngine.Digital)):
            swaption.setPricingEngine(engine(model, probabilities=kind))
            if swaption.isExpired() or swaption.exercise().dates()[-1] <= TODAY:
                continue
            expected[f"{label}NPV"] = v.amount(v.result(swaption.NPV), scale)
            expected[f"{label}Probabilities"] = list(swaption.probabilities())
        return {"name": name,
                "inputs": {"settings": st, "swaption": spec, "models": MODELS, "engineSettings": SETTINGS, "oas": OAS},
                "expected": expected}


def cases():
    return [swaption_case(name, spec) for name, spec in swaptions()]


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/gaussian1dnonstandardswaptionengine.hpp", cases)
