# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for SwaptionVolatilityStructure (ql/termstructures/volatility/swaption/swaptionvolstructure.hpp), each
output computed by the C++ harness.

- "limits" cases, on 6 constant structures of tools/vectors/gen/termstructures/volatility/swaption/
  swaptionconstantvol.py, whose functions build them, and 4 of the harness's own structures, which override only
  what SwaptionVolatilityStructure leaves abstract, with a maximum swap tenor in years, in months and in days: their
  maxSwapTenor ([length, units]), maxSwapLength (refused for a tenor in days) and volatilityType; swapLength at 10
  tenors (in months and years, and refused: not positive, in weeks or days) and between 6 pairs of dates (refused
  where the end isn't after the start).
- "overloads" cases, at the 10 option-and-swap pairs of that generator: on the constant structures, volatility and
  blackVariance in the four overloads the wheel doesn't bind ((Period,Period), (Time,Period), (Period,Time),
  (Date,Time)); on the harness's own structures, whose volatility varies with the option time, the swap length and
  the strike, volatility, blackVariance and shift in all six overloads and the summary of the smile section of each
  smileSection overload (tools/cxx/src/termstructures/volatility/smilesectionspec.hpp), called without
  extrapolating, through the base's date and tenor defaults.
- "extrapolation" cases, on the same structures as built: the smile section of each of the six overloads called with
  extrapolate true at 6 pairs past the structure's maximum swap tenor, swap length or time, as [pair][overload]. The
  overloads on a time and a tenor, on a tenor and a length and on a date and a length check the query with the flag
  and then call another overload without it, which refuses; the other three extrapolate.

A refused value is {"throws": true}. Each case's input "surface" is the spec tools/cxx/src/termstructures/volatility/
swaption/swaptionvolspec.hpp reads; the pairs are as in that generator, the option time "max+" the structure's
maximum time plus 1. The option dates and maximum times that place the inputs are read from the wheel's constant
structure on the same reference date, calendar, convention and day counter, built under the same evaluation date;
every output is the harness's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/SwaptionVolStructureCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_swaptionconstantvol",
    os.path.join(v.GEN_ROOT, "termstructures", "volatility", "swaption", "swaptionconstantvol.py"))
scv = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(scv)

D, W, M, Y = scv.D, scv.W, scv.M, scv.Y
NAN = float("nan")
METHOD = "SwaptionVolatilityStructure::"
OVERLOADS = ["(Period,Period)", "(Date,Period)", "(Time,Period)", "(Period,Time)", "(Date,Time)", "(Time,Time)"]
UNBOUND = ["(Period,Period)", "(Time,Period)", "(Period,Time)", "(Date,Time)"]


def base_spec(reference, cal, convention, vol, dc, max_swap_tenor, extrapolate=False):
    return {"type": "BaseSwaptionVolatility", "referenceDate": v.enc(reference), "calendar": cal,
            "convention": int(convention), "volatility": vol, "dayCounter": dc, "maxSwapTenor": max_swap_tenor,
            "enableExtrapolation": extrapolate}


E1 = ql.Date(15, ql.January, 2025)
SURFACES = [scv.SURFACES[k] for k in (0, 3, 5, 9, 13, 16)] + [
    (E1, base_spec(E1, scv.TARGET, ql.Following, 0.2, scv.A365, [30, Y])),
    (E1, base_spec(E1, scv.UK_EXCHANGE, ql.ModifiedFollowing, 0.15, scv.A360, [18, M])),
    (E1, base_spec(E1, scv.TARGET, ql.Following, 0.25, scv.ACT_ISDA, [10, Y], True)),
    (E1, base_spec(E1, scv.NULL_CALENDAR, ql.Unadjusted, 0.1, scv.A365, [3650, D])),
]
TENORS = [[1, Y], [18, M], [6, M], [100, Y], [1, M], [0, M], [-1, Y], [2, W], [10, D], [0, Y]]
DATE_PAIRS = [
    (ql.Date(15, ql.January, 2025), ql.Date(15, ql.January, 2030)),
    (ql.Date(15, ql.January, 2025), ql.Date(16, ql.February, 2025)),
    (ql.Date(29, ql.February, 2024), ql.Date(28, ql.February, 2025)),
    (ql.Date(15, ql.January, 2025), ql.Date(31, ql.December, 2199)),
    (ql.Date(15, ql.January, 2025), ql.Date(15, ql.January, 2025)),
    (ql.Date(15, ql.January, 2026), ql.Date(15, ql.January, 2025)),
]
# (option tenor, option time, swap tenor, swap length): past the maximum swap tenor or length, or the maximum time.
EXTRAPOLATED = [
    ([1, Y], 1.0, [150, Y], 150.0),
    ([2, Y], 2.0, [40, Y], 40.0),
    ([6, M], "max+", [5, Y], 5.0),
    ([1, Y], 1.0, [24, M], 2.0),
    ([3, M], 0.25, [11, Y], 11.0),
    ([1, Y], "max+", [200, Y], 200.0),
]


def kind_of(spec):
    return "fixed" if "referenceDate" in spec else f"{spec['settlementDays']} days"


def wheel_twin(spec):
    """The wheel's constant structure on the spec's reference date, calendar, convention and day counter, built under
    the evaluation date the caller has set: its option dates and maximum time are the spec's structure's, the harness's
    own structure's maximum date being Date::maxDate too."""
    if spec["type"] == "ConstantSwaptionVolatility":
        return scv.make_surface(spec)
    return scv.make_surface(scv.fixed_spec(ql.Date(spec["referenceDate"]), spec["calendar"], spec["convention"],
                                           spec["volatility"], spec["dayCounter"]))


def pairs_of(evaluation_date, spec, rows):
    """The pairs' inputs on a structure, the option dates from the wheel twin's optionDateFromTenor and "max+" its
    maximum time plus 1."""
    with v.case_settings(evaluation_date):
        twin = wheel_twin(spec)
        out = []
        for row in rows:
            option, time, swap, length = row[:4]
            strike = row[4] if len(row) > 4 else 0.03
            extrapolate = row[5] if len(row) > 5 else False
            out.append({"optionTenor": option, "optionDate": v.enc(twin.optionDateFromTenor(scv.period(option))),
                        "optionTime": twin.maxTime() + 1.0 if time == "max+" else time, "swapTenor": swap,
                        "swapLength": length, "strike": strike, "extrapolate": extrapolate})
    return out


def args_of(p, overload):
    option, swap = overload[1:-1].split(",")
    o = {"Period": p["optionTenor"], "Date": p["optionDate"], "Time": p["optionTime"]}[option]
    s = {"Period": p["swapTenor"], "Time": p["swapLength"]}[swap]
    return o, s


def limits_case(H, k, evaluation_date, spec):
    with H.case_settings(v.enc(evaluation_date)) as st:
        expected = {m: v.attempt(lambda m=m: H.call(METHOD + m, spec))
                    for m in ("maxSwapTenor", "maxSwapLength", "volatilityType")}
        expected["swapLengthTenors"] = [v.attempt(lambda t=t: H.call(METHOD + "swapLength(Period)", spec, t))
                                        for t in TENORS]
        expected["swapLengthDates"] = [
            v.attempt(lambda a=a, b=b: H.call(METHOD + "swapLength(Date,Date)", spec, v.enc(a), v.enc(b)))
            for a, b in DATE_PAIRS]
        return {"name": f"limits {spec['type']} {kind_of(spec)} {evaluation_date.ISO()} / {k}",
                "inputs": {"settings": st, "kind": "limits", "surface": spec, "tenors": TENORS,
                           "datePairs": [[v.enc(a), v.enc(b)] for a, b in DATE_PAIRS]},
                "expected": expected}


def overloads_case(H, k, evaluation_date, spec):
    with H.case_settings(v.enc(evaluation_date)) as st:
        pairs = pairs_of(evaluation_date, spec, scv.PAIRS)
        base = spec["type"] == "BaseSwaptionVolatility"
        expected = {}
        for overload in (OVERLOADS if base else UNBOUND):
            for member in ("volatility", "blackVariance"):
                expected[member + overload] = [
                    v.attempt(lambda p=p: H.call(METHOD + member + overload, spec, *args_of(p, overload),
                                                 p["strike"], p["extrapolate"]))
                    for p in pairs]
        if base:
            for overload in OVERLOADS:
                expected["shift" + overload] = [
                    v.attempt(lambda p=p: H.call(METHOD + "shift" + overload, spec, *args_of(p, overload),
                                                 p["extrapolate"]))
                    for p in pairs]
            for overload in OVERLOADS:
                expected["smileSection" + overload] = [
                    v.attempt(lambda p=p: H.call(METHOD + "smileSection" + overload, spec, *args_of(p, overload),
                                                 False, p["strike"]))
                    for p in pairs]
        return {"name": f"overloads {spec['type']} {kind_of(spec)} {evaluation_date.ISO()} / {k}",
                "inputs": {"settings": st, "kind": "overloads", "surface": spec, "pairs": v.enc(pairs)},
                "expected": expected}


def extrapolation_case(H, k, evaluation_date, spec):
    with H.case_settings(v.enc(evaluation_date)) as st:
        pairs = pairs_of(evaluation_date, spec, EXTRAPOLATED)
        expected = {"smileSections": [
            [v.attempt(lambda p=p, o=o: H.call(METHOD + "smileSection" + o, spec, *args_of(p, o), True, 0.03))
             for o in OVERLOADS]
            for p in pairs]}
        return {"name": f"extrapolation {spec['type']} {kind_of(spec)} {evaluation_date.ISO()} / {k}",
                "inputs": {"settings": st, "kind": "extrapolation", "surface": spec, "pairs": v.enc(pairs),
                           "overloads": OVERLOADS, "strike": 0.03},
                "expected": expected}


def cases():
    with cxx.connect() as H:
        out = [limits_case(H, k, ev, spec) for k, (ev, spec) in enumerate(SURFACES)]
        out += [overloads_case(H, k, ev, spec) for k, (ev, spec) in enumerate(SURFACES)]
        out += [extrapolation_case(H, k, ev, spec) for k, (ev, spec) in enumerate(SURFACES)]
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/swaptionvolstructure.hpp", cases)
