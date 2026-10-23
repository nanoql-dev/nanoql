# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FloatFloatSwaption (ql/instruments/floatfloatswaption.hpp, .cpp) priced by its
Gaussian1dFloatFloatSwaptionEngine (ql/pricingengines/swaption/gaussian1dfloatfloatswaptionengine.hpp, .cpp): European
and Bermudan float-float swaptions on the evaluation date 15 January 2025, each priced on
pricingengines/swaption/gaussian1dswaptionengine.py's three GSR models, with that generator's engine settings varied on
the first, an option-adjusted spread of 20bp, the underlying's value, today's exercise included, and the engine's
probabilities, naive and digital.

The swaptions (instruments/floatfloatswap.py's swaps, Euribor 3M forecast on a flat 3.1%, Euribor 6M on 3.3%, CMS 10Y on
EUR swaps of Euribor 6M on 3.1%): Euribor 3M against 6M, payer and receiver, European and Bermudan; a gearing of 0.5
with spreads at the money, Bermudan; a zero gearing with a 3% spread, a fixed leg in all but name (QuantLib makes the
gearing its machine epsilon), Bermudan; amortizing nominals with both capital exchanges, whose redemptions the exercise
takes with their coupons, Bermudan; caps and floors on Euribor 3M, Bermudan; Euribor 6M against CMS 10Y, Bermudan, and
CMS 10Y capped against Euribor 6M, European; an exercise on the evaluation date, one past, and a Bermudan whose first
exercise has passed. NPVs and the underlying's value are compared at the largest nominal, the probabilities as they are.

Left out, each a documented divergence: a rebate (G1R6 (v), FloatFloatRebateTests); a discount or forwarding curve on
another clock (G1R6 (xvi), Gaussian1dMismatchedTimesTests); and flat payoff extrapolation (G1R6 (xxi),
FlatPayoffExtrapolationTests). The wheel's Gaussian1dFloatFloatSwaptionEngine(model, integrationPoints, stddevs,
extrapolatePayoff, flatPayoffExtrapolation, oas, discountCurve, includeTodaysExercise, probabilities) is the C++
constructor's proxy, its underlying value and probabilities read by the swaption's underlyingValue() and
probabilities(). Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Gaussian1dFloatFloatSwaptionEngineVectorTests.cs."""
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
ffs = _load("wheel_floatfloatswap", "instruments/floatfloatswap.py")
swp = g1d.swp
TODAY = g1d.TODAY
TARGET_CAL = g1d.TARGET_CAL
D, M, Y = ql.Days, ql.Months, ql.Years
MODELS, SETTINGS = g1d.MODELS, g1d.SETTINGS
OAS = 0.002
PAYER, RECEIVER = ffs.PAYER, ffs.RECEIVER
E3M, E6M, CMS10Y = ffs.E3M, ffs.E6M, ffs.CMS10Y


def swaption_spec(swap, exercise):
    return {"swap": swap, "exercise": exercise, "settlementType": swp.PHYSICAL, "settlementMethod": swp.OTC}


def make_swaption(spec):
    return ql.FloatFloatSwaption(ffs.make_swap(spec["swap"]), swp.make_exercise(spec["exercise"]),
                                 spec["settlementType"], spec["settlementMethod"])


def engine(model, settings=None, oas=None, include_today=False, probabilities=None):
    s = settings or {"integrationPoints": 64, "stddevs": 7.0, "extrapolatePayoff": True, "discountCurve": None}
    curve = ql.YieldTermStructureHandle() if s["discountCurve"] is None else g1d.irx.handle(s["discountCurve"])
    quote = ql.QuoteHandle() if oas is None else ql.QuoteHandle(ql.SimpleQuote(oas))
    probs = ql.Gaussian1dFloatFloatSwaptionEngine.NoProb if probabilities is None else probabilities
    return ql.Gaussian1dFloatFloatSwaptionEngine(model, s["integrationPoints"], s["stddevs"], s["extrapolatePayoff"],
                                                 False, quote, curve, include_today, probs)


def annual(first, count):
    return [TARGET_CAL.advance(first, i, Y) for i in range(count)]


def periods(schedule):
    return len(ffs.fvf.schedule(schedule).dates()) - 1


def euribor(swap_type, start, years, nominal=1e6, **terms):
    """Euribor 3M against 6M from start for the given years, at a constant nominal."""
    end = start + ql.Period(years, Y)
    s1, s2 = ffs.sched(start, end, (3, M)), ffs.sched(start, end, (6, M))
    return ffs.swap(swap_type, [nominal] * periods(s1), [nominal] * periods(s2), s1, E3M, s2, E6M, **terms)


def swaptions():
    out = []
    exercise1, start1, _ = swp.dates_of(ql.Period(1, Y), ql.Period(4, Y))
    for swap_type, name in ((PAYER, "payer"), (RECEIVER, "receiver")):
        out.append((f"Euribor 3M against 6M, European, {name}",
                    swaption_spec(euribor(swap_type, start1, 4), swp.european(exercise1))))
        out.append((f"Euribor 3M against 6M, Bermudan, {name}",
                    swaption_spec(euribor(swap_type, start1, 4), swp.bermudan(annual(exercise1, 4)))))
    out.append(("a gearing of 0.5 and spreads at the money, Bermudan, receiver",
                swaption_spec(euribor(RECEIVER, start1, 4, gearing1=[0.5] * 16, spread1=[0.0175] * 16,
                                      spread2=[0.002 + 0.0001 * i for i in range(8)]),
                              swp.bermudan(annual(exercise1, 4)))))
    out.append(("a zero gearing and a 3% spread on Euribor 3M, Bermudan, payer",
                swaption_spec(euribor(PAYER, start1, 4, gearing1=[0.0] * 16, spread1=[0.03] * 16),
                              swp.bermudan(annual(exercise1, 3)))))
    end1 = start1 + ql.Period(4, Y)
    s3, s6 = ffs.sched(start1, end1, (3, M)), ffs.sched(start1, end1, (6, M))
    steps = [1e6, 1e6, 8e5, 8e5, 6e5, 6e5, 4e5, 4e5]
    out.append(("amortizing, capital exchanged, Bermudan",
                swaption_spec(ffs.swap(PAYER, [x for x in steps for _ in range(2)], steps, s3, E3M, s6, E6M,
                                       intermediate=True, final=True), swp.bermudan(annual(exercise1, 4)))))
    out.append(("caps and floors on Euribor 3M, Bermudan",
                swaption_spec(euribor(PAYER, start1, 4, caps1=[0.035] * 16, floors1=[0.025] * 16),
                              swp.bermudan(annual(exercise1, 4)))))
    end5 = start1 + ql.Period(5, Y)
    s6y, s1y = ffs.sched(start1, end5, (6, M)), ffs.sched(start1, end5, (1, Y))
    out.append(("Euribor 6M against CMS 10Y, Bermudan",
                swaption_spec(ffs.swap(PAYER, [1e6] * periods(s6y), [1e6] * periods(s1y), s6y, E6M, s1y, CMS10Y),
                              swp.bermudan(annual(exercise1, 5)))))
    out.append(("CMS 10Y capped against Euribor 6M, European",
                swaption_spec(ffs.swap(RECEIVER, [1e6] * periods(s1y), [1e6] * periods(s6y), s1y, CMS10Y, s6y, E6M,
                                       caps1=[0.035] * periods(s1y)), swp.european(exercise1))))
    start0 = TARGET_CAL.advance(TODAY, 2, D)
    out.append(("exercised on the evaluation date", swaption_spec(euribor(PAYER, start0, 3), swp.european(TODAY))))
    past = TARGET_CAL.advance(TODAY, -1, M)
    out.append(("expired a month ago",
                swaption_spec(euribor(PAYER, TARGET_CAL.advance(past, 2, D), 3), swp.european(past))))
    out.append(("Bermudan, first exercise past",
                swaption_spec(euribor(PAYER, start1, 4),
                              swp.bermudan([TARGET_CAL.advance(TODAY, -1, M), exercise1,
                                            TARGET_CAL.advance(exercise1, 1, Y)]))))
    return out


def scale_of(swap):
    return max(abs(x) for x in swap["nominal1"] + swap["nominal2"])


def swaption_case(name, spec):
    with v.case_settings(TODAY) as st:
        scale = scale_of(spec["swap"])
        swaption = make_swaption(spec)
        expected = {"gsr": [], "settings": []}
        for m in MODELS:
            swaption.setPricingEngine(engine(g1d.make_gsr(m)))
            expected["gsr"].append(v.amount(v.result(swaption.NPV), scale))
        model = g1d.make_gsr(MODELS[0])
        swaption.setPricingEngine(engine(model))
        if not swaption.isExpired():
            expected["underlyingValue"] = v.amount(v.result(swaption.underlyingValue), scale)
        for s in SETTINGS:
            swaption.setPricingEngine(engine(model, s))
            expected["settings"].append(v.amount(v.result(swaption.NPV), scale))
        swaption.setPricingEngine(engine(model, oas=OAS))
        expected["oas"] = v.amount(v.result(swaption.NPV), scale)
        swaption.setPricingEngine(engine(model, include_today=True))
        expected["includeToday"] = v.amount(v.result(swaption.NPV), scale)
        for label, kind in (("naive", ql.Gaussian1dFloatFloatSwaptionEngine.Naive),
                            ("digital", ql.Gaussian1dFloatFloatSwaptionEngine.Digital)):
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
    v.main(__file__, "ql/pricingengines/swaption/gaussian1dfloatfloatswaptionengine.hpp", cases)
