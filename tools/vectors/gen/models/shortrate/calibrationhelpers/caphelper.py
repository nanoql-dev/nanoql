# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CapHelper (ql/models/shortrate/calibrationhelpers/caphelper.hpp, .cpp), with its base
BlackCalibrationHelper (ql/models/calibrationhelper.hpp): 34 helpers and 8 refusals, on
tools/vectors/gen/models/shortrate/calibrationhelpers/swaptionhelper.py's two markets (QuantLib's test-suite market of
15 February 2002, a flat 4.875825% curve and Euribor 6M on it; and that of 15 January 2025, a flat 2.7% discount curve
and Euribor-style 6M and 3M indexes forecasting on a flat 3.1% one, one with no fixing days).

The helpers cover lengths from 1 to 10 years, with and without the first caplet (the cap from the curve's reference
date, its first caplet fixed then, or from a period of the index's tenor after it), fixed legs of annual, semiannual and
quarterly frequency on Actual/360, Thirty/360 and Actual/365 (Fixed) (the swap whose fair rate strikes the cap), the
three error types, and normal and shifted lognormal volatilities with and without a shift.

Each helper's outputs are its market value; times(), the discretized cap's mandatory times on the term structure's
reference date and day counter; the Black price at half, once and twice the quoted volatility; the model value and
the calibration error with an AnalyticCapFloorEngine on a HullWhite (a = 0.05, sigma = 0.01) on the helper's curve;
the volatility that value implies (accuracy 1e-10, 1,000 evaluations, within the error type's bracket); and, for an
implied-volatility error, the errors under two more Hull-White models whose values fall below and above the Black
prices at the bracket's ends, so the implied volatility is clamped to them ("calibrationErrorClamped"). The refusals:
an unknown error type (which QuantLib refuses only in calibrationError), an unknown volatility type (in the market
value), no engine (in the model value and the error), an implied volatility whose guess, the quote, is outside the
bracket or whose target the bracket doesn't hold, and no term structure. QuantLib's helper builds a dummy index it
never reads, which nanoql's leaves out.

The at-par switch is set once for the run (at par, the wheel's default) and recorded in every case's settings. A
helper spec is {"length" ([length, units]), "volatility", "index" (a spec of instruments/fixedvsfloatingswap.py),
"fixedLegFrequency", "fixedLegDayCounter", "includeFirstSwaplet", "termStructure" (a flat forward spec, or null for
QuantLib's empty handle), "errorType", "type", "shift"}, the constructor's arguments in order. The wheel's
ql.CapHelper is the proxy of the C++ constructor, every argument by position, and setPricingEngine, marketValue,
modelValue, calibrationError, blackPrice, times and impliedVolatility forward to the C++ members of those names. Read
back by tests/NanoQL.Tests/Vectors/Models/CapHelperVectorTests.cs."""
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


swh = _load("wheel_swaptionhelper", "models/shortrate/calibrationhelpers/swaptionhelper.py")
fvf, irx = swh.fvf, swh.irx

M, Y = ql.Months, ql.Years
A360, A365, THIRTY360_BB = swh.A360, swh.A365, swh.THIRTY360_BB
REL, PRICE, IMPLIED, SL, N = swh.REL, swh.PRICE, swh.IMPLIED, swh.SL, swh.N
ANNUAL, SEMIANNUAL, QUARTERLY = int(ql.Annual), int(ql.Semiannual), int(ql.Quarterly)

HULL_WHITE = swh.HULL_WHITE
CLAMPS = swh.CLAMPS


def helper_spec(length, vol, index, ts, frequency=ANNUAL, fixed_dc=A360, include_first=False, error_type=REL,
                vol_type=SL, shift=0.0):
    return {"length": [length[0], int(length[1])], "volatility": vol, "index": index, "fixedLegFrequency": frequency,
            "fixedLegDayCounter": fixed_dc, "includeFirstSwaplet": include_first, "termStructure": ts,
            "errorType": int(error_type), "type": int(vol_type), "shift": shift}


def make_helper(spec):
    """The wheel's helper for a spec, every argument by position."""
    return ql.CapHelper(ql.Period(*spec["length"]), ql.QuoteHandle(ql.SimpleQuote(spec["volatility"])),
                        fvf.make_index(spec["index"]), spec["fixedLegFrequency"],
                        irx.day_counter(spec["fixedLegDayCounter"]), spec["includeFirstSwaplet"],
                        irx.handle(spec["termStructure"]), spec["errorType"], spec["type"], spec["shift"])


def helpers():
    """(name, spec) for the 34 helpers."""
    out = []
    # QuantLib's test-suite market: lengths, the three error types, with and without the first caplet.
    for et in (REL, PRICE, IMPLIED):
        for length, vol in ((1, 0.18), (3, 0.16), (5, 0.15)):
            for first in (False, True):
                out.append((f"2002 {length}Y / error type {et} / first caplet {first}",
                            helper_spec((length, Y), vol, swh.EURIBOR6M_2002, swh.CURVE_2002, include_first=first,
                                        error_type=et)))
    # 18 so far: the second market's indexes and fixed legs.
    for name, index in (("E6M", swh.E6M), ("E3M", swh.E3M), ("E6M, no fixing days", swh.E6M0)):
        out.append((f"2025 4Y / {name}", helper_spec((4, Y), 0.2, index, swh.DISCOUNT)))
        out.append((f"2025 4Y / {name} / first caplet", helper_spec((4, Y), 0.2, index, swh.DISCOUNT,
                                                                    include_first=True)))
    for frequency, dc in ((SEMIANNUAL, THIRTY360_BB), (QUARTERLY, A365), (ANNUAL, THIRTY360_BB)):
        out.append((f"2025 3Y / fixed leg {frequency} / {dc['type']}",
                    helper_spec((3, Y), 0.21, swh.E6M, swh.DISCOUNT, frequency=frequency, fixed_dc=dc)))
    # 27 so far: volatility types.
    for vol_type, vol, shift, et in ((N, 0.0085, 0.0, REL), (N, 0.0085, 0.0, IMPLIED), (N, 0.006, 0.0, PRICE),
                                     (SL, 0.15, 0.01, REL), (SL, 0.15, 0.01, IMPLIED)):
        out.append((f"volatility type {vol_type} / {vol} / shift {shift} / error type {et}",
                    helper_spec((5, Y), vol, swh.E6M, swh.DISCOUNT, error_type=et, vol_type=vol_type, shift=shift)))
    # 32 so far: a short and a long cap.
    out.append(("2025 18M", helper_spec((18, M), 0.25, swh.E6M, swh.DISCOUNT)))
    out.append(("2025 10Y / first caplet", helper_spec((10, Y), 0.17, swh.E3M, swh.DISCOUNT, include_first=True)))
    assert len(out) == 34, len(out)
    return out


def priced_outputs(spec, helper):
    ts = spec["termStructure"]
    out = {"marketValue": helper.marketValue(), "times": v.enc(list(helper.times())),
           "blackPrice": [helper.blackPrice(f * spec["volatility"]) for f in (0.5, 1.0, 2.0)]}
    hw = swh.hull_white(ts, HULL_WHITE)
    helper.setPricingEngine(ql.AnalyticCapFloorEngine(hw))
    hw_value = helper.modelValue()
    out["modelValueHullWhite"] = hw_value
    out["calibrationErrorHullWhite"] = helper.calibrationError()
    low, high = swh.implied_bracket(spec)
    out["impliedVolatility"] = v.attempt(lambda: helper.impliedVolatility(hw_value, 1e-10, 1000, low, high))
    if spec["errorType"] == IMPLIED:
        out["calibrationErrorClamped"] = []
        for m in CLAMPS:
            helper.setPricingEngine(ql.AnalyticCapFloorEngine(swh.hull_white(ts, m)))
            out["calibrationErrorClamped"].append(v.attempt(helper.calibrationError))
    return out


def helper_case(name, spec):
    today = swh.TODAY_2002 if spec["termStructure"] is swh.CURVE_2002 else swh.TODAY
    with v.case_settings(today, use_at_par_coupons=True) as st:
        inputs = {"settings": st, "helper": spec, "hullWhite": HULL_WHITE}
        if spec["errorType"] == IMPLIED:
            inputs["clamps"] = CLAMPS
        return {"name": name, "inputs": inputs, "expected": priced_outputs(spec, make_helper(spec))}


def refusal_cases():
    out = []

    def add(name, spec, outputs):
        with v.case_settings(swh.TODAY, use_at_par_coupons=True) as st:
            expected = {}
            for output in outputs:
                helper = make_helper(spec)
                if output == "calibrationError":
                    helper.setPricingEngine(ql.AnalyticCapFloorEngine(swh.hull_white(spec["termStructure"],
                                                                                      HULL_WHITE)))
                    expected[output] = v.attempt(helper.calibrationError)
                elif output == "calibrationErrorNoEngine":
                    expected[output] = v.attempt(helper.calibrationError)
                elif output == "modelValueNoEngine":
                    expected[output] = v.attempt(helper.modelValue)
                elif output == "marketValue":
                    expected[output] = v.attempt(helper.marketValue)
                elif output == "impliedGuessOutside":
                    expected[output] = v.attempt(lambda: helper.impliedVolatility(0.01, 1e-10, 100, 0.25, 1.0))
                elif output == "impliedNotBracketed":
                    expected[output] = v.attempt(lambda: helper.impliedVolatility(10.0, 1e-10, 100, 0.01, 1.0))
                else:
                    raise ValueError(output)
            out.append({"name": "refused / " + name,
                        "inputs": {"settings": st, "helper": spec, "hullWhite": HULL_WHITE},
                        "expected": expected})

    def spec_of(**kw):
        return helper_spec((3, Y), kw.pop("vol", 0.2), swh.E6M, kw.pop("ts", swh.DISCOUNT), **kw)

    add("unknown error type", spec_of(error_type=7), ["calibrationError"])
    add("unknown volatility type", spec_of(vol_type=7), ["marketValue"])
    add("no engine", spec_of(), ["modelValueNoEngine", "calibrationErrorNoEngine"])
    add("implied volatility, guess outside the bracket", spec_of(), ["impliedGuessOutside"])
    add("implied volatility, target not bracketed", spec_of(), ["impliedNotBracketed"])
    add("no term structure", spec_of(ts=None), ["marketValue"])
    add("no term structure, first caplet", spec_of(ts=None, include_first=True), ["marketValue"])
    add("normal volatility, implied guess outside the bracket", spec_of(vol_type=N, vol=0.008),
        ["impliedGuessOutside"])
    return out


def cases():
    out = [helper_case(*row) for row in helpers()]
    out += refusal_cases()
    for c in out[-8:]:
        assert all(x == v.THROWS for x in c["expected"].values()), c["name"]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/calibrationhelpers/caphelper.hpp", cases)
