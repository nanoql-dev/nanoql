# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Gaussian1dJamshidianSwaptionEngine
(ql/pricingengines/swaption/gaussian1djamshidianswaptionengine.hpp, .cpp): European swaptions on vanilla and
overnight-indexed swaps, on the evaluation date 15 January 2025, each priced on the three GSR models of
gaussian1dswaptionengine.py, and the engine's refusals.

The swaptions are payers and receivers on Euribor 6M and ESTR swaps, from 1Y to 5Y options on 2Y to 10Y swaps, struck
from 2% to 4%, at nominals of 1 to 10 million, with annual and semiannual fixed legs; an overnight-indexed swap paid
two days after each period's end, whose delay the engine freezes into the nominal; and swaps starting on the exercise
date. Each swaption's output is the NPV on each model ("gsr"). Amounts are compared at the nominal.

Left out, each a documented divergence: short-dated swaptions away from the money, which QuantLib's fixed bracket for
y*, [-8, 8], refuses (the S-g1d plan's G1R6 (xviii); tests/NanoQL.Tests/Divergences/JamshidianBracketTests.cs); a
swaption with no fixed coupon starting after its exercise, where QuantLib reads past the fixed reset dates
(JamshidianNoFixedCouponTests.cs); and, as in gaussian1dswaptionengine.py, curves on another clock and reversions
between 0 and 1e-2.

The refusals: a cash swaption settled at the par yield curve; a Bermudan exercise ("exotic swaptions"); a floating
spread; nominals that aren't constant; and each one checked first when several fail. The wheel's
Gaussian1dJamshidianSwaptionEngine(model) is the proxy of the C++ constructor. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Gaussian1dJamshidianSwaptionEngineVectorTests.cs."""
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
swp, fvf = g1d.swp, g1d.fvf

TODAY = g1d.TODAY
TARGET_CAL = g1d.TARGET_CAL
D, M, Y = ql.Days, ql.Months, ql.Years
MODELS = g1d.MODELS


def engine(model):
    return ql.Gaussian1dJamshidianSwaptionEngine(model)


def swaptions():
    """(name, swaption spec) for the swaptions."""
    out = []
    k = 0
    for exercise_tenor in ((1, Y), (2, Y), (5, Y)):
        for length in ((2, Y), (5, Y), (10, Y)):
            exercise, start, maturity = swp.dates_of(ql.Period(*exercise_tenor), ql.Period(*length))
            for swap_type in (swp.PAYER, swp.RECEIVER):
                strike = [0.02, 0.031, 0.04, 0.025, 0.035][k % 5]
                nominal = [1.0, 1e6, 1e7][k % 3]
                fixed_tenor = ql.Period(6, M) if k % 4 == 3 else ql.Period(1, Y)
                out.append((f"{exercise_tenor[0]}Y on {length[0]}Y / "
                            f"{'payer' if swap_type == swp.PAYER else 'receiver'} / {strike} / {k}",
                            swp.swaption_spec(swp.vanilla(swap_type, nominal, strike, start, maturity,
                                                          fixed_tenor=fixed_tenor), swp.european(exercise))))
                k += 1
    for k, (exercise_tenor, length) in enumerate((((1, Y), (5, Y)), ((2, Y), (3, Y)))):
        exercise, start, maturity = swp.dates_of(ql.Period(*exercise_tenor), ql.Period(*length))
        s = fvf.sched(start, maturity, ql.Period(1, Y), swp.TARGET, ql.ModifiedFollowing)
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"overnight-indexed / {exercise_tenor[0]}Y on {length[0]}Y / {swap_type}",
                        swp.swaption_spec(swp.overnight(swap_type, [1e6, 2e6][k], 0.029, start, maturity),
                                          swp.european(exercise))))
            out.append((f"overnight-indexed, paid 2 days late / {exercise_tenor[0]}Y on {length[0]}Y / {swap_type}",
                        swp.swaption_spec(fvf.ois("nominal", swap_type, [1e6], s, 0.029, swp.A360, swp.ESTR,
                                                  payment_lag=2, payment_calendar=swp.TARGET),
                                          swp.european(exercise))))
    for exercise_tenor, length in (((1, Y), (4, Y)), ((3, Y), (7, Y))):
        exercise = TARGET_CAL.advance(TODAY, ql.Period(*exercise_tenor))
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"no delay / {exercise_tenor[0]}Y on {length[0]}Y / {swap_type}",
                        swp.swaption_spec(swp.vanilla(swap_type, 1e6, 0.03, exercise, exercise + ql.Period(*length)),
                                          swp.european(exercise))))
    return out


def swaption_case(name, spec):
    with v.case_settings(TODAY) as st:
        scale = fvf.scale_of(spec["swap"])
        swaption = swp.make_swaption(spec)
        expected = {"gsr": []}
        for m in MODELS:
            swaption.setPricingEngine(engine(g1d.make_gsr(m)))
            expected["gsr"].append(fvf.ibc.scaled(swaption.NPV, scale))
        return {"name": name, "inputs": {"settings": st, "swaption": spec, "models": MODELS}, "expected": expected}


def refusal_cases():
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    plain = swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity)
    spread = swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity, spread=0.001)
    bermudan = swp.bermudan([exercise, TARGET_CAL.advance(exercise, 1, Y)])
    fixed = fvf.sched(start, maturity, ql.Period(1, Y), swp.TARGET, ql.ModifiedFollowing)
    amortizing = fvf.ois("nominals", swp.PAYER, [1e6, 9e5, 8e5, 7e5, 6e5], fixed, 0.03, swp.A360, swp.ESTR)
    rows = [
        ("par yield curve", swp.swaption_spec(plain, swp.european(exercise), swp.CASH, swp.PAR_YIELD)),
        ("Bermudan", swp.swaption_spec(plain, bermudan)),
        ("floating spread", swp.swaption_spec(spread, swp.european(exercise))),
        ("nominals that aren't constant", swp.swaption_spec(amortizing, swp.european(exercise))),
        ("par yield curve, Bermudan and a spread", swp.swaption_spec(spread, bermudan, swp.CASH, swp.PAR_YIELD)),
        ("Bermudan and a spread", swp.swaption_spec(spread, bermudan)),
        ("a spread and nominals that aren't constant", swp.swaption_spec(
            fvf.ois("nominals", swp.PAYER, [1e6, 9e5, 8e5, 7e5, 6e5], fixed, 0.03, swp.A360, swp.ESTR, spread=0.002),
            swp.european(exercise))),
    ]
    out = []
    for name, spec in rows:
        with v.case_settings(TODAY) as st:
            swaption = swp.make_swaption(spec)
            swaption.setPricingEngine(engine(g1d.make_gsr(MODELS[0])))
            out.append({"name": "refused / " + name,
                        "inputs": {"settings": st, "swaption": spec, "model": MODELS[0]},
                        "expected": {"NPV": v.attempt(swaption.NPV)}})
    return out


def cases():
    out = [swaption_case(*row) for row in swaptions()]
    out += refusal_cases()
    for c in out[-7:]:
        assert c["expected"]["NPV"] == v.THROWS, c["name"]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/gaussian1djamshidianswaptionengine.hpp", cases)
