# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Gaussian1dSwaptionEngine (ql/pricingengines/swaption/gaussian1dswaptionengine.hpp, .cpp):
European and Bermudan swaptions on vanilla and overnight-indexed swaps, on the evaluation date 15 January 2025, each
priced on three GSR models, with the engine's settings varied on the first, and the engine's refusals.

The swaptions are of instruments/swaption.py's kind: payers and receivers on Euribor 6M swaps (the index forecast on a
flat 3.1%) and ESTR overnight-indexed swaps (on the same curve), from 1Y to 5Y options on 2Y to 10Y swaps, struck from
1.5% to 5%, at nominals of 1 to 10 million; Bermudans with annual exercises; an overnight-indexed swap paid two days
after each period's end and one with a five-day lookback under the observation shift; swaps starting two business days
after the exercise and on it; an exercise on the evaluation date, and one past. The models (gsr.py's specs, on
instruments/swaption.py's flat 2.7% curve): a constant volatility and reversion; piecewise volatilities on three step
dates; and piecewise reversions on the same dates, at another numeraire time. Each swaption's outputs are the NPV on
each model ("gsr", in the order of the inputs' list) and on the first model with each of the settings ("settings"):
32 integration points and 5 standard deviations; no payoff extrapolation; a discount curve given, a flat 2.5%. Amounts
are compared at the nominal.

Left out, each a documented divergence: a discount or forwarding curve on another clock than the model's (QuantLib
reads it at the model's times; the S-g1d plan's G1R6 (xvi), tests/NanoQL.Tests/Divergences/Gaussian1dMismatchedTimesTests.cs),
so every curve here is on Actual/365 (Fixed) from the evaluation date; flat payoff extrapolation, whose right tail
QuantLib integrates at the second-last node (G1R6 (xxi), FlatPayoffExtrapolationTests.cs); reversions between 0 and
1e-2 (G1R4 (i), GsrSmallReversionTests.cs). The engine's probabilities, an additional result the wheel doesn't bind,
are the harness's (cxx/pricingengines/swaption/gaussian1dswaptionengine.py).

The refusals: a cash swaption settled at the par yield curve, and nominals that aren't constant. The wheel's
Gaussian1dSwaptionEngine(model, integrationPoints, stddevs, extrapolatePayoff, flatPayoffExtrapolation,
discountCurve, probabilities) is the proxy of the C++ constructor; ql.Gsr(handle, dates, quotes, quotes, T) of Gsr's
constructor on quotes. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/Gaussian1dSwaptionEngineVectorTests.cs."""
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


swp = _load("wheel_swaption", "instruments/swaption.py")
fvf, irx = swp.fvf, swp.irx

TODAY = swp.TODAY
TARGET_CAL = swp.TARGET_CAL
D, M, Y = ql.Days, ql.Months, ql.Years
DISCOUNT = swp.DISCOUNT
OTHER_DISCOUNT = fvf.curve(0.025)


def step_dates(*years):
    return [v.enc(TARGET_CAL.advance(TODAY, y, Y)) for y in years]


MODELS = [
    {"curve": DISCOUNT, "volstepdates": [], "volatilities": [0.01], "reversions": [0.01], "T": 60.0},
    {"curve": DISCOUNT, "volstepdates": step_dates(1, 2, 5), "volatilities": [0.008, 0.009, 0.0105, 0.012],
     "reversions": [0.02], "T": 60.0},
    {"curve": DISCOUNT, "volstepdates": step_dates(1, 2, 5), "volatilities": [0.007, 0.0085, 0.009, 0.0095],
     "reversions": [0.03, 0.05, 0.025, 0.04], "T": 40.0},
]
SETTINGS = [
    {"integrationPoints": 32, "stddevs": 5.0, "extrapolatePayoff": True, "discountCurve": None},
    {"integrationPoints": 64, "stddevs": 7.0, "extrapolatePayoff": False, "discountCurve": None},
    {"integrationPoints": 64, "stddevs": 7.0, "extrapolatePayoff": True, "discountCurve": OTHER_DISCOUNT},
]


def make_gsr(spec):
    quotes = lambda xs: [ql.QuoteHandle(ql.SimpleQuote(x)) for x in xs]  # noqa: E731
    return ql.Gsr(irx.handle(spec["curve"]), [ql.Date(d) for d in spec["volstepdates"]],
                  quotes(spec["volatilities"]), quotes(spec["reversions"]), spec["T"])


def engine(model, settings=None):
    s = settings or {"integrationPoints": 64, "stddevs": 7.0, "extrapolatePayoff": True, "discountCurve": None}
    curve = ql.YieldTermStructureHandle() if s["discountCurve"] is None else irx.handle(s["discountCurve"])
    return ql.Gaussian1dSwaptionEngine(model, s["integrationPoints"], s["stddevs"], s["extrapolatePayoff"], False,
                                       curve)


def annual(first, count):
    return [TARGET_CAL.advance(first, i, Y) for i in range(count)]


def swaptions():
    """(name, swaption spec) for the swaptions."""
    out = []
    k = 0
    for exercise_tenor in ((1, Y), (2, Y), (5, Y)):
        for length in ((2, Y), (5, Y), (10, Y)):
            exercise, start, maturity = swp.dates_of(ql.Period(*exercise_tenor), ql.Period(*length))
            for swap_type in (swp.PAYER, swp.RECEIVER):
                strike = [0.02, 0.031, 0.045, 0.015, 0.05][k % 5]
                nominal = [1.0, 1e6, 1e7][k % 3]
                fixed_tenor = ql.Period(6, M) if k % 4 == 3 else ql.Period(1, Y)
                out.append((f"European {exercise_tenor[0]}Y on {length[0]}Y / "
                            f"{'payer' if swap_type == swp.PAYER else 'receiver'} / {strike} / {k}",
                            swp.swaption_spec(swp.vanilla(swap_type, nominal, strike, start, maturity,
                                                          fixed_tenor=fixed_tenor), swp.european(exercise))))
                k += 1
    # Bermudans: annual exercises into the swap from the first.
    for first_tenor, length, count in (((1, Y), (10, Y), 9), ((2, Y), (5, Y), 4), ((6, M), (3, Y), 3)):
        exercise, start, maturity = swp.dates_of(ql.Period(*first_tenor), ql.Period(*length))
        for swap_type in (swp.PAYER, swp.RECEIVER):
            for strike in (0.025, 0.035):
                out.append((f"Bermudan from {first_tenor[0]}{'MY'[first_tenor[1] == Y]} on {length[0]}Y / "
                            f"{swap_type} / {strike}",
                            swp.swaption_spec(swp.vanilla(swap_type, 1e6, strike, start, maturity),
                                              swp.bermudan(annual(exercise, count)))))
    # Overnight-indexed swaps: European and Bermudan, a payment lag, a lookback with the observation shift.
    for k, (exercise_tenor, length) in enumerate((((1, Y), (5, Y)), ((2, Y), (3, Y)))):
        exercise, start, maturity = swp.dates_of(ql.Period(*exercise_tenor), ql.Period(*length))
        s = fvf.sched(start, maturity, ql.Period(1, Y), swp.TARGET, ql.ModifiedFollowing)
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"overnight-indexed / {exercise_tenor[0]}Y on {length[0]}Y / {swap_type}",
                        swp.swaption_spec(swp.overnight(swap_type, [1e6, 2e6][k], 0.029, start, maturity),
                                          swp.european(exercise))))
            out.append((f"overnight-indexed, paid 2 days late / {exercise_tenor[0]}Y on {length[0]}Y / {swap_type}",
                        swp.swaption_spec(fvf.ois("nominal", swap_type, [1e6], s, 0.029, swp.A360, swp.ESTR,
                                                  payment_lag=2, payment_calendar=swp.TARGET), swp.european(exercise))))
            out.append((f"overnight-indexed, 5-day lookback with the shift / {exercise_tenor[0]}Y on "
                        f"{length[0]}Y / {swap_type}",
                        swp.swaption_spec(fvf.ois("nominal", swap_type, [1e6], s, 0.029, swp.A360, swp.ESTR,
                                                  lookback=5, shift=True, payment_calendar=swp.TARGET),
                                          swp.european(exercise))))
            out.append((f"overnight-indexed, Bermudan / {exercise_tenor[0]}Y on {length[0]}Y / {swap_type}",
                        swp.swaption_spec(swp.overnight(swap_type, 1e6, 0.029, start, maturity),
                                          swp.bermudan(annual(exercise, length[0])))))
    # Swaps starting on the exercise date; an exercise on the evaluation date; one past, expired.
    for exercise_tenor, length in (((1, Y), (4, Y)), ((3, Y), (7, Y))):
        exercise = TARGET_CAL.advance(TODAY, ql.Period(*exercise_tenor))
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"no delay / {exercise_tenor[0]}Y on {length[0]}Y / {swap_type}",
                        swp.swaption_spec(swp.vanilla(swap_type, 1e6, 0.03, exercise, exercise + ql.Period(*length)),
                                          swp.european(exercise))))
    out.append(("exercised on the evaluation date", swp.swaption_spec(
        swp.vanilla(swp.RECEIVER, 1e6, 0.03, TARGET_CAL.advance(TODAY, 2, D), TODAY + ql.Period(5, Y)),
        swp.european(TODAY))))
    past = TARGET_CAL.advance(TODAY, -1, M)
    out.append(("expired a month ago", swp.swaption_spec(
        swp.vanilla(swp.PAYER, 1e6, 0.03, TARGET_CAL.advance(past, 2, D), TARGET_CAL.advance(past, 2, D) + ql.Period(5, Y)),
        swp.european(past))))
    # A Bermudan whose first exercise has passed.
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    out.append(("Bermudan, first exercise past", swp.swaption_spec(
        swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity),
        swp.bermudan([TARGET_CAL.advance(TODAY, -1, M), exercise, TARGET_CAL.advance(exercise, 1, Y)]))))
    return out


def swaption_case(name, spec):
    with v.case_settings(TODAY) as st:
        scale = fvf.scale_of(spec["swap"])
        swaption = swp.make_swaption(spec)
        expected = {"gsr": [], "settings": []}
        for m in MODELS:
            swaption.setPricingEngine(engine(make_gsr(m)))
            expected["gsr"].append(fvf.ibc.scaled(swaption.NPV, scale))
        first = make_gsr(MODELS[0])
        for s in SETTINGS:
            swaption.setPricingEngine(engine(first, s))
            expected["settings"].append(fvf.ibc.scaled(swaption.NPV, scale))
        return {"name": name,
                "inputs": {"settings": st, "swaption": spec, "models": MODELS, "engineSettings": SETTINGS},
                "expected": expected}


def refusal_cases():
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    plain = swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity)
    fixed = fvf.sched(start, maturity, ql.Period(1, Y), swp.TARGET, ql.ModifiedFollowing)
    amortizing = fvf.ois("nominals", swp.PAYER, [1e6, 9e5, 8e5, 7e5, 6e5], fixed, 0.03, swp.A360, swp.ESTR)
    rows = [
        ("par yield curve", swp.swaption_spec(plain, swp.european(exercise), swp.CASH, swp.PAR_YIELD)),
        ("nominals that aren't constant", swp.swaption_spec(amortizing, swp.european(exercise))),
        ("par yield curve and nominals that aren't constant",
         swp.swaption_spec(amortizing, swp.european(exercise), swp.CASH, swp.PAR_YIELD)),
    ]
    out = []
    for name, spec in rows:
        with v.case_settings(TODAY) as st:
            swaption = swp.make_swaption(spec)
            swaption.setPricingEngine(engine(make_gsr(MODELS[0])))
            out.append({"name": "refused / " + name,
                        "inputs": {"settings": st, "swaption": spec, "model": MODELS[0]},
                        "expected": {"NPV": v.attempt(swaption.NPV)}})
    return out


def cases():
    out = [swaption_case(*row) for row in swaptions()]
    out += refusal_cases()
    by_name = {c["name"]: c["expected"] for c in out}
    for c in out[-3:]:
        assert c["expected"]["NPV"] == v.THROWS, c["name"]
    assert by_name["expired a month ago"]["gsr"][0]["value"] == 0.0
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/gaussian1dswaptionengine.hpp", cases)
