# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ConstantSwaptionVolatility (ql/termstructures/volatility/swaption/swaptionconstantvol.hpp),
through the SwaptionVolatilityStructure interface it inherits as the wheel binds it: 20 structures, 10 with a fixed
reference date and 10 whose reference date is 0 to 3 business days after the evaluation date (QuantLib's moving
structure, which nanoql builds by ConstantSwaptionVolatility.Create from the context), over four calendars, five
business-day conventions, four day counters, both volatility types and shifts from 0 to 0.03, some with
extrapolation enabled.

Each structure gives one case: its reference date, maxDate, minStrike, maxStrike and volatilityType; and at 10
option-and-swap pairs (an option tenor, its date, an option time, a swap tenor and a swap length, a strike and the
extrapolate argument), as arrays over the pairs: volatility and blackVariance on an option date and a swap tenor and on
an option time and a swap length, the two overloads the wheel binds; shift in its six overloads; and the smile section
of each of the six smileSection overloads, called without extrapolating, read at the pair's strike: its exercise date
and time, volatility type, shift, ATM level and volatility for the overloads on a date and a tenor and on a time and a
length, as [pair][member], and its volatility for the other four. The pairs reach a swap tenor past the maximum, 100
years, with and without the extrapolate argument, a tenor in weeks, which the constant structure takes by date and
tenor (it doesn't convert the tenor to a length there) and refuses wherever the base converts it, an option time past
the maximum and a negative one, and a NaN strike. A normal structure refuses its shift ("shift parameter only makes
sense for lognormal volatilities").

A refused value is {"throws": true}. A structure spec ("surface") is {"type": "ConstantSwaptionVolatility",
"referenceDate" or "settlementDays", "calendar", "convention", "volatility", "dayCounter", "volatilityType", "shift",
"enableExtrapolation"}, as tools/cxx/src/termstructures/volatility/swaption/swaptionvolspec.hpp reads it; calendars and
day counters as in tools/vectors/gen/indexes/interestrateindex.py. A pair is {"optionTenor", "optionDate",
"optionTime", "swapTenor", "swapLength", "strike", "extrapolate"}, the tenors as [length, units] and the date read from
the wheel's optionDateFromTenor. The wheel's ConstantSwaptionVolatility is the proxy of the C++ class's constructors,
with no logic of its own. The structure functions here are shared by the harness generators of cxx/termstructures/
volatility/swaption/, which import this file. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/SwaptionConstantVolVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

TARGET, US_SOFR, UK_EXCHANGE, NULL_CALENDAR = irx.TARGET, irx.US_SOFR, irx.UK_EXCHANGE, irx.NULL_CALENDAR
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}
SL, N = int(ql.ShiftedLognormal), int(ql.Normal)
D, W, M, Y = int(ql.Days), int(ql.Weeks), int(ql.Months), int(ql.Years)
NAN = float("nan")


def fixed_spec(reference, cal, convention, vol, dc, vol_type=SL, shift=0.0, extrapolate=False):
    return {"type": "ConstantSwaptionVolatility", "referenceDate": v.enc(reference), "calendar": cal,
            "convention": int(convention), "volatility": vol, "dayCounter": dc, "volatilityType": vol_type,
            "shift": shift, "enableExtrapolation": extrapolate}


def moving_spec(days, cal, convention, vol, dc, vol_type=SL, shift=0.0, extrapolate=False):
    return {"type": "ConstantSwaptionVolatility", "settlementDays": days, "calendar": cal,
            "convention": int(convention), "volatility": vol, "dayCounter": dc, "volatilityType": vol_type,
            "shift": shift, "enableExtrapolation": extrapolate}


def make_surface(spec):
    """The structure a spec describes, built with the wheel's ConstantSwaptionVolatility on a fixed reference date or
    on settlement days from the evaluation date, which the caller has set."""
    cal = irx.calendar(spec["calendar"])
    dc = irx.day_counter(spec["dayCounter"])
    first = ql.Date(spec["referenceDate"]) if "referenceDate" in spec else spec["settlementDays"]
    s = ql.ConstantSwaptionVolatility(first, cal, spec["convention"], spec["volatility"], dc, spec["volatilityType"],
                                      spec["shift"])
    if spec["enableExtrapolation"]:
        s.enableExtrapolation()
    return s


def period(p):
    return ql.Period(p[0], p[1])


# (option tenor, option time, swap tenor, swap length, strike, extrapolate); the option time "max+" is the structure's
# maximum time plus 1.
PAIRS = [
    ([1, Y], 1.0, [5, Y], 5.0, 0.03, False),
    ([1, M], 1.0 / 12, [1, Y], 1.0, 0.01, False),
    ([10, Y], 10.0, [10, Y], 10.0, 0.05, False),
    ([0, D], 0.0, [6, M], 0.5, -0.01, False),
    ([6, M], 0.5, [30, Y], 30.0, 0.02, True),
    ([2, Y], 2.0, [100, Y], 100.0, 0.03, False),
    ([2, Y], 2.0, [101, Y], 101.0, 0.03, False),
    ([3, M], "max+", [101, Y], 101.0, NAN, True),
    ([18, M], -0.25, [18, M], 1.5, NAN, False),
    ([5, Y], 5.0, [2, W], 2.0 / 52, 0.04, False),
]

E1 = ql.Date(15, ql.January, 2025)
E2 = ql.Date(29, ql.February, 2024)
E3 = ql.Date(24, ql.December, 2026)
SURFACES = [
    (E1, fixed_spec(E1, TARGET, ql.Following, 0.2, A365)),
    (E1, fixed_spec(E1, TARGET, ql.ModifiedFollowing, 0.35, A360, SL, 0.01)),
    (E1, fixed_spec(ql.Date(17, ql.January, 2025), US_SOFR, ql.Preceding, 0.0085, THIRTY360_BB, N)),
    (E2, fixed_spec(E2, UK_EXCHANGE, ql.ModifiedPreceding, 0.012, ACT_ISDA, N, 0.005)),
    (E2, fixed_spec(ql.Date(1, ql.March, 2024), NULL_CALENDAR, ql.Unadjusted, 0.0, A365, SL, 0.03)),
    (E2, fixed_spec(E2, TARGET, ql.Following, 0.6, A365, SL, 0.0, True)),
    (E3, fixed_spec(E3, US_SOFR, ql.ModifiedFollowing, 0.007, A360, N, 0.0, True)),
    (E3, fixed_spec(ql.Date(31, ql.December, 2026), UK_EXCHANGE, ql.Following, 0.25, THIRTY360_BB, SL, 0.02)),
    (E3, fixed_spec(E3, NULL_CALENDAR, ql.Preceding, 1.1, ACT_ISDA)),
    (E1, fixed_spec(E1, TARGET, ql.Unadjusted, 0.015, A365, N, 0.01)),
    (E1, moving_spec(0, TARGET, ql.Following, 0.2, A365)),
    (E1, moving_spec(2, TARGET, ql.ModifiedFollowing, 0.18, A360, SL, 0.01)),
    (ql.Date(18, ql.January, 2025), moving_spec(0, US_SOFR, ql.Following, 0.009, A365, N)),
    (ql.Date(17, ql.January, 2025), moving_spec(3, UK_EXCHANGE, ql.Preceding, 0.3, THIRTY360_BB, SL, 0.03)),
    (E2, moving_spec(1, NULL_CALENDAR, ql.Unadjusted, 0.0075, ACT_ISDA, N, 0.02)),
    (E2, moving_spec(2, TARGET, ql.ModifiedFollowing, 0.45, A365, SL, 0.0, True)),
    (E3, moving_spec(2, TARGET, ql.Following, 0.005, A360, N, 0.0, True)),
    (E3, moving_spec(1, UK_EXCHANGE, ql.ModifiedPreceding, 0.22, A365)),
    (ql.Date(31, ql.December, 2029), moving_spec(2, TARGET, ql.Following, 0.6, A365, SL, 0.005)),
    (ql.Date(28, ql.February, 2028), moving_spec(0, US_SOFR, ql.ModifiedFollowing, 0.011, THIRTY360_BB, N)),
]


def pairs_of(s):
    """The pairs' inputs on a structure, the option dates from its optionDateFromTenor."""
    out = []
    for option, time, swap, length, strike, extrapolate in PAIRS:
        t = s.maxTime() + 1.0 if time == "max+" else time
        out.append({"optionTenor": option, "optionDate": v.enc(s.optionDateFromTenor(period(option))),
                    "optionTime": t, "swapTenor": swap, "swapLength": length, "strike": v.enc(strike),
                    "extrapolate": extrapolate})
    return out


def section_summary(section, strike):
    """The section's summary, as the harness gives it (tools/cxx/src/termstructures/volatility/smilesectionspec.hpp)."""
    return [v.enc(section.exerciseDate()), section.exerciseTime(), int(section.volatilityType()), section.shift(),
            v.enc(section.atmLevel()), section.volatility(strike)]


def summary_or_throw(f, strike):
    try:
        section = f()
    except RuntimeError:
        return dict(v.THROWS)
    return v.enc(section_summary(section, strike))


def surface_case(k, evaluation_date, spec):
    st = v.settings(evaluation_date)
    s = make_surface(spec)
    pairs = pairs_of(s)
    expected = {"referenceDate": v.enc(s.referenceDate()), "maxDate": v.enc(s.maxDate()), "minStrike": s.minStrike(),
                "maxStrike": s.maxStrike(), "volatilityType": int(s.volatilityType())}

    def over_pairs(f):
        out = []
        for p in pairs:
            o, d, t = period(p["optionTenor"]), ql.Date(p["optionDate"]), p["optionTime"]
            sw, ln = period(p["swapTenor"]), p["swapLength"]
            x = NAN if p["strike"] == "NaN" else p["strike"]
            out.append(f(o, d, t, sw, ln, x, p["extrapolate"]))
        return out

    expected["volatilityDatePeriod"] = over_pairs(
        lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.volatility(d, sw, x, e)))
    expected["volatilityTimeTime"] = over_pairs(
        lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.volatility(t, ln, x, e)))
    expected["blackVarianceDatePeriod"] = over_pairs(
        lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.blackVariance(d, sw, x, e)))
    expected["blackVarianceTimeTime"] = over_pairs(
        lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.blackVariance(t, ln, x, e)))
    expected["shiftPeriodPeriod"] = over_pairs(lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.shift(o, sw, e)))
    expected["shiftDatePeriod"] = over_pairs(lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.shift(d, sw, e)))
    expected["shiftTimePeriod"] = over_pairs(lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.shift(t, sw, e)))
    expected["shiftPeriodTime"] = over_pairs(lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.shift(o, ln, e)))
    expected["shiftDateTime"] = over_pairs(lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.shift(d, ln, e)))
    expected["shiftTimeTime"] = over_pairs(lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.shift(t, ln, e)))
    expected["smileSectionDatePeriod"] = over_pairs(
        lambda o, d, t, sw, ln, x, e: summary_or_throw(lambda: s.smileSection(d, sw), x))
    expected["smileSectionTimeTime"] = over_pairs(
        lambda o, d, t, sw, ln, x, e: summary_or_throw(lambda: s.smileSection(t, ln), x))
    expected["smileVolatilityPeriodPeriod"] = over_pairs(
        lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.smileSection(o, sw).volatility(x)))
    expected["smileVolatilityTimePeriod"] = over_pairs(
        lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.smileSection(t, sw).volatility(x)))
    expected["smileVolatilityPeriodTime"] = over_pairs(
        lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.smileSection(o, ln).volatility(x)))
    expected["smileVolatilityDateTime"] = over_pairs(
        lambda o, d, t, sw, ln, x, e: v.attempt(lambda: s.smileSection(d, ln).volatility(x)))
    kind = "fixed" if "referenceDate" in spec else f"{spec['settlementDays']} days"
    return {"name": f"{kind} {evaluation_date.ISO()} {s.calendar().name()} {s.dayCounter().name()} "
                    f"{'Normal' if spec['volatilityType'] == N else 'ShiftedLognormal'} {spec['shift']!r} / {k}",
            "inputs": {"settings": st, "surface": spec, "pairs": pairs},
            "expected": expected}


def cases():
    return [surface_case(k, ev, spec) for k, (ev, spec) in enumerate(SURFACES)]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/swaptionconstantvol.hpp", cases)
