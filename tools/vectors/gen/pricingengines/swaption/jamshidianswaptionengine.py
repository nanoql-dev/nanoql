# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's JamshidianSwaptionEngine (ql/pricingengines/swaption/jamshidianswaptionengine.hpp): 84
European swaptions on the evaluation date 15 January 2025, each priced on three Hull-White and two Vasicek models,
and 12 refusals.

The swaptions are those of instruments/swaption.py's kind: payers and receivers from 1M to 10Y options on 1Y to 15Y
Euribor 6M and 3M swaps, struck from 1% to 6%, at nominals of 1 to 10 million, with annual and semiannual fixed legs,
swaps starting two business days after the exercise (the start delay Hull-White's five-argument bond option accounts
for) or on it, and overnight-indexed swaps, which the engine prices on their fixed leg. The Hull-White models are on
the discount curve (a flat 2.7%), with reversions from 0.05 to 0.3; the Vasicek models, which have no curve of their own, price with the discount curve given to the
engine, which fixes the reference date and day counter of the times. A Hull-White engine given another curve ignores
it: the model's own gives the times.

A Hull-White and a Vasicek model at a = 1e-9, below the formulas' sqrt(QL_EPSILON), where QuantLib 1.44's forms take
their a = 0 limits and drop terms of order a*tau, are left out: nanoql's forms are QuantLib's master's (#2882), which
move those prices beyond the tolerance (tests/NanoQL.Tests/Divergences/SmallReversionTests.cs).

The refusals: a cash swaption settled at the par yield curve; a Bermudan exercise ("exotic swaptions"); a floating
spread; nominals that aren't constant; a Vasicek model with no curve; and each one checked first when several fail.
An expired swaption, exercised before the evaluation date or on it, is worth 0.

Each swaption's outputs are the NPV on each Hull-White model ("hullWhite", in the order of the inputs' list), on each
Vasicek one ("vasicek"), and on the first Hull-White model with a 5% curve given to the engine ("hullWhiteOtherCurve");
a refusal's case has the one output "NPV" for the model it names. Amounts are compared at the nominal. Each case's
inputs hold the swaption (instruments/swaption.py's spec), the discount curve, and the models: {"a", "sigma"} on the
discount curve for Hull-White, {"r0", "a", "b", "sigma", "lambda"} for Vasicek. The wheel's JamshidianSwaptionEngine is
the proxy of the C++ constructor. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/JamshidianSwaptionEngineVectorTests.cs."""
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
OTHER_CURVE = fvf.curve(0.05)

HULL_WHITE = [{"a": 0.05, "sigma": 0.01}, {"a": 0.1, "sigma": 0.005}, {"a": 0.3, "sigma": 0.02}]
VASICEK = [{"r0": 0.03, "a": 0.08, "b": 0.035, "sigma": 0.009, "lambda": 0.0},
           {"r0": 0.02, "a": 0.2, "b": 0.04, "sigma": 0.012, "lambda": 0.1}]


def hull_white(curve, spec):
    return ql.HullWhite(irx.handle(curve), spec["a"], spec["sigma"])


def vasicek(spec):
    return ql.Vasicek(spec["r0"], spec["a"], spec["b"], spec["sigma"], spec["lambda"])


def swaptions():
    """(name, swaption spec) for the 84 swaptions."""
    out = []
    k = 0
    for exercise_tenor in ((1, M), (6, M), (1, Y), (2, Y), (5, Y), (10, Y)):
        for length in ((1, Y), (5, Y), (10, Y)):
            exercise, start, maturity = swp.dates_of(ql.Period(*exercise_tenor), ql.Period(*length))
            for swap_type in (swp.PAYER, swp.RECEIVER):
                strike = [0.02, 0.031, 0.045, 0.01, 0.06][k % 5]
                nominal = [1.0, 1e6, 1e7][k % 3]
                fixed_tenor = ql.Period(6, M) if k % 4 == 3 else ql.Period(1, Y)
                index = swp.E3M if k % 7 == 6 else swp.E6M
                out.append((f"{exercise_tenor[0]}{'MY'[exercise_tenor[1] == Y]} on {length[0]}Y / "
                            f"{'payer' if swap_type == swp.PAYER else 'receiver'} / {strike} / {k}",
                            swp.swaption_spec(swp.vanilla(swap_type, nominal, strike, start, maturity, index=index,
                                                          fixed_tenor=fixed_tenor), swp.european(exercise))))
                k += 1
    # 36 so far: swaps starting on the exercise date, with no delay.
    for exercise_tenor, length in (((1, Y), (4, Y)), ((3, Y), (7, Y)), ((6, M), (2, Y)), ((2, Y), (15, Y))):
        exercise = TARGET_CAL.advance(TODAY, ql.Period(*exercise_tenor))
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"no delay / {exercise_tenor[0]}{'MY'[exercise_tenor[1] == Y]} on {length[0]}Y / "
                        f"{swap_type}",
                        swp.swaption_spec(swp.vanilla(swap_type, 1e6, 0.03, exercise, exercise + ql.Period(*length)),
                                          swp.european(exercise))))
    # 44 so far: physical and collateralized cash settlements, which the engine prices alike.
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    for st, sm in ((swp.PHYSICAL, swp.CLEARED), (swp.CASH, swp.COLLATERALIZED)):
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"settlement {st}-{sm} / {swap_type}",
                        swp.swaption_spec(swp.vanilla(swap_type, 1e6, 0.028, start, maturity),
                                          swp.european(exercise), st, sm)))
    # 48 so far: overnight-indexed swaps, priced on their fixed leg.
    for k, (exercise_tenor, length) in enumerate((((1, Y), (5, Y)), ((2, Y), (3, Y)), ((6, M), (1, Y)),
                                                  ((5, Y), (5, Y)))):
        exercise, start, maturity = swp.dates_of(ql.Period(*exercise_tenor), ql.Period(*length))
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"overnight-indexed / {exercise_tenor[0]}{'MY'[exercise_tenor[1] == Y]} on {length[0]}Y / "
                        f"{swap_type}",
                        swp.swaption_spec(swp.overnight(swap_type, [1e6, 2e6][k % 2], 0.029, start, maturity),
                                          swp.european(exercise))))
    # 56 so far: deep in and out of the money, and the at-the-money forward's neighbourhood.
    exercise, start, maturity = swp.dates_of(ql.Period(2, Y), ql.Period(5, Y))
    for strike in (0.001, 0.005, 0.025, 0.03, 0.035, 0.08, 0.12):
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"strike {strike} / {swap_type}",
                        swp.swaption_spec(swp.vanilla(swap_type, 1e6, strike, start, maturity),
                                          swp.european(exercise))))
    # 70 so far: other day counters and stubs.
    for k, (fixed_dc, fixed_tenor) in enumerate(((swp.A360, ql.Period(1, Y)), (swp.A365, ql.Period(6, M)),
                                                 (swp.THIRTY360_BB, ql.Period(3, M)))):
        exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(3, Y))
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"fixed leg {fixed_dc['type']} / {fixed_tenor} / {swap_type}",
                        swp.swaption_spec(swp.vanilla(swap_type, 1e6, 0.03, start, maturity, fixed_dc=fixed_dc,
                                                      fixed_tenor=fixed_tenor), swp.european(exercise))))
    stub_exercise, stub_start, _ = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    stub_maturity = stub_start + ql.Period(4, Y) + ql.Period(5, M)
    for swap_type in (swp.PAYER, swp.RECEIVER):
        out.append((f"short front stub / {swap_type}",
                    swp.swaption_spec(swp.vanilla(swap_type, 1e6, 0.03, stub_start, stub_maturity),
                                      swp.european(stub_exercise))))
    # 78 so far: expired, exercised before the evaluation date and on it, and exercised the next day.
    past = TARGET_CAL.advance(TODAY, -1, M)
    out.append(("expired a month ago", swp.swaption_spec(
        swp.vanilla(swp.PAYER, 1e6, 0.03, TARGET_CAL.advance(past, 2, D),
                    TARGET_CAL.advance(past, 2, D) + ql.Period(5, Y)), swp.european(past))))
    out.append(("exercised on the evaluation date", swp.swaption_spec(
        swp.vanilla(swp.RECEIVER, 1e6, 0.03, TARGET_CAL.advance(TODAY, 2, D), TODAY + ql.Period(5, Y)),
        swp.european(TODAY))))
    tomorrow = TARGET_CAL.advance(TODAY, 1, D)
    for swap_type in (swp.PAYER, swp.RECEIVER):
        out.append((f"exercised the next business day / {swap_type}", swp.swaption_spec(
            swp.vanilla(swap_type, 1e6, 0.027, TARGET_CAL.advance(tomorrow, 2, D),
                        TARGET_CAL.advance(tomorrow, 2, D) + ql.Period(3, Y)), swp.european(tomorrow))))
    late, late_start, late_maturity = swp.dates_of(ql.Period(20, Y), ql.Period(10, Y))
    for swap_type in (swp.PAYER, swp.RECEIVER):
        out.append((f"20Y on 10Y / {swap_type}", swp.swaption_spec(
            swp.vanilla(swap_type, 1e6, 0.03, late_start, late_maturity), swp.european(late))))
    assert len(out) == 84, len(out)
    return out


def priced(spec):
    with v.case_settings(TODAY) as st:
        scale = fvf.scale_of(spec["swap"])
        swaption = swp.make_swaption(spec)
        expected = {"hullWhite": [], "vasicek": []}
        for m in HULL_WHITE:
            swaption.setPricingEngine(ql.JamshidianSwaptionEngine(hull_white(DISCOUNT, m)))
            expected["hullWhite"].append(fvf.ibc.scaled(swaption.NPV, scale))
        for m in VASICEK:
            swaption.setPricingEngine(ql.JamshidianSwaptionEngine(vasicek(m), irx.handle(DISCOUNT)))
            expected["vasicek"].append(fvf.ibc.scaled(swaption.NPV, scale))
        swaption.setPricingEngine(ql.JamshidianSwaptionEngine(hull_white(DISCOUNT, HULL_WHITE[0]),
                                                              irx.handle(OTHER_CURVE)))
        expected["hullWhiteOtherCurve"] = fvf.ibc.scaled(swaption.NPV, scale)
        return st, expected


def swaption_case(name, spec):
    st, expected = priced(spec)
    return {"name": name,
            "inputs": {"settings": st, "swaption": spec, "discountCurve": DISCOUNT, "otherCurve": OTHER_CURVE,
                       "hullWhite": HULL_WHITE, "vasicek": VASICEK},
            "expected": expected}


def refusal_cases():
    """The refusals: (name, swaption spec, model kind, curve given)."""
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    plain = swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity)
    spread = swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity, spread=0.001)
    bermudan = swp.bermudan([exercise, TARGET_CAL.advance(exercise, 1, Y)])
    fixed = fvf.sched(start, maturity, ql.Period(1, Y), swp.TARGET, ql.ModifiedFollowing)
    amortizing = fvf.ois("nominals", swp.PAYER, [1e6, 9e5, 8e5, 7e5, 6e5], fixed, 0.03, swp.A360, swp.ESTR)
    rows = [
        ("par yield curve", swp.swaption_spec(plain, swp.european(exercise), swp.CASH, swp.PAR_YIELD), "HullWhite",
         None),
        ("Bermudan", swp.swaption_spec(plain, bermudan), "HullWhite", None),
        ("floating spread", swp.swaption_spec(spread, swp.european(exercise)), "HullWhite", None),
        ("nominals that aren't constant", swp.swaption_spec(amortizing, swp.european(exercise)), "HullWhite", None),
        ("Vasicek with no curve", swp.swaption_spec(plain, swp.european(exercise)), "Vasicek", None),
        ("Vasicek with no curve, receiver", swp.swaption_spec(
            swp.vanilla(swp.RECEIVER, 1e6, 0.03, start, maturity), swp.european(exercise)), "Vasicek", None),
        ("par yield curve, Bermudan and a spread", swp.swaption_spec(spread, bermudan, swp.CASH, swp.PAR_YIELD),
         "HullWhite", None),
        ("Bermudan and a spread", swp.swaption_spec(spread, bermudan), "HullWhite", None),
        ("a spread and nominals that aren't constant", swp.swaption_spec(
            fvf.ois("nominals", swp.PAYER, [1e6, 9e5, 8e5, 7e5, 6e5], fixed, 0.03, swp.A360, swp.ESTR, spread=0.002),
            swp.european(exercise)), "HullWhite", None),
        ("Vasicek, Bermudan, with no curve", swp.swaption_spec(plain, bermudan), "Vasicek", None),
        ("Vasicek, a spread, with a curve", swp.swaption_spec(spread, swp.european(exercise)), "Vasicek", DISCOUNT),
        ("Vasicek, par yield curve, with a curve", swp.swaption_spec(plain, swp.european(exercise), swp.CASH,
                                                                     swp.PAR_YIELD), "Vasicek", DISCOUNT),
    ]
    out = []
    for name, spec, kind, curve in rows:
        with v.case_settings(TODAY) as st:
            swaption = swp.make_swaption(spec)
            if kind == "HullWhite":
                engine = ql.JamshidianSwaptionEngine(hull_white(DISCOUNT, HULL_WHITE[0]))
            elif curve is None:
                engine = ql.JamshidianSwaptionEngine(vasicek(VASICEK[0]))
            else:
                engine = ql.JamshidianSwaptionEngine(vasicek(VASICEK[0]), irx.handle(curve))
            swaption.setPricingEngine(engine)
            out.append({"name": "refused / " + name,
                        "inputs": {"settings": st, "swaption": spec, "discountCurve": DISCOUNT,
                                   "model": kind, "engineCurve": curve, "hullWhite": HULL_WHITE[0],
                                   "vasicek": VASICEK[0]},
                        "expected": {"NPV": v.attempt(swaption.NPV)}})
    return out


def cases():
    out = [swaption_case(*row) for row in swaptions()]
    out += refusal_cases()
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    for c in out[-12:]:
        assert c["expected"]["NPV"] == v.THROWS, c["name"]
    # A Hull-White engine reads the model's curve, whatever curve it is given.
    for c in out[:-12]:
        assert c["expected"]["hullWhiteOtherCurve"] == c["expected"]["hullWhite"][0], c["name"]
    assert by_name["expired a month ago"]["hullWhite"][0]["value"] == 0.0
    assert by_name["exercised on the evaluation date"]["vasicek"][0]["value"] == 0.0


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/jamshidianswaptionengine.hpp", cases)
