# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ConstantOptionletVolatility (ql/termstructures/volatility/optionlet/constantoptionletvol.hpp),
through the OptionletVolatilityStructure and TermStructure interface it inherits: 12 surfaces, 6 with a fixed
reference date and 6 whose reference date is 0 to 3 business days after the evaluation date (QuantLib's moving
structure, which nanoql builds by ConstantOptionletVolatility.Create from the context), over four calendars, seven
business-day conventions, four day counters, both volatility types and displacements from 0 to 0.5, a zero volatility
among them.

Each surface gives one case: its reference date, maxDate, minStrike and maxStrike (the strike range), volatilityType
and displacement; and volatility and blackVariance at 10 dates (the reference date, nine later ones up to
Date::maxDate) and at 10 times (0, 1e-8, ..., the maximum time, and 1e-13 past it, inside QuantLib's close_enough),
each at 3 strikes, as [date or time][strike]. A refused value would be {"throws": true}; none is refused here: the
range and strike refusals are the harness's (tools/vectors/gen/cxx/termstructures/volatility/optionlet/
optionletvolatilitystructure.py). The evaluation date of a moving surface falls on a weekend or before a holiday for
some of them, so the calendar's advance from it is checked too.

A surface spec ("surface") is {"type": "ConstantOptionletVolatility", "referenceDate" or "settlementDays",
"calendar", "convention", "volatility", "dayCounter", "volatilityType", "displacement", "enableExtrapolation"}: the
convention and the volatility type as QuantLib's integers; calendars and day counters as in
tools/vectors/gen/indexes/interestrateindex.py, whose functions build them; enableExtrapolation, whether
enableExtrapolation() is called on the built structure. The surface functions here are shared by the harness
generators of cxx/termstructures/, which import this file. The wheel's ConstantOptionletVolatility is the proxy of the
C++ class's constructors, with no logic of its own. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/ConstantOptionletVolVectorTests.cs."""
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
THIRTY360_EU = {"type": "Thirty360", "convention": int(ql.Thirty360.European)}
SL, N = int(ql.ShiftedLognormal), int(ql.Normal)


def fixed_spec(reference, cal, convention, vol, dc, vol_type, displacement, extrapolate=False):
    return {"type": "ConstantOptionletVolatility", "referenceDate": v.enc(reference), "calendar": cal,
            "convention": int(convention), "volatility": vol, "dayCounter": dc, "volatilityType": vol_type,
            "displacement": displacement, "enableExtrapolation": extrapolate}


def moving_spec(days, cal, convention, vol, dc, vol_type, displacement, extrapolate=False):
    return {"type": "ConstantOptionletVolatility", "settlementDays": days, "calendar": cal,
            "convention": int(convention), "volatility": vol, "dayCounter": dc, "volatilityType": vol_type,
            "displacement": displacement, "enableExtrapolation": extrapolate}


def make_surface(spec):
    """The structure a spec describes, built with the wheel's ConstantOptionletVolatility on a fixed reference date
    or on settlement days from the evaluation date, which the caller has set."""
    cal = irx.calendar(spec["calendar"])
    dc = irx.day_counter(spec["dayCounter"])
    first = ql.Date(spec["referenceDate"]) if "referenceDate" in spec else spec["settlementDays"]
    s = ql.ConstantOptionletVolatility(first, cal, spec["convention"], spec["volatility"], dc,
                                       spec["volatilityType"], spec["displacement"])
    if spec["enableExtrapolation"]:
        s.enableExtrapolation()
    return s


# (evaluation date, spec, strikes): six fixed reference dates (a leap day, a Saturday and a year end among them),
# then six moving ones, from a Friday, a Saturday, Christmas Eve, the day before a leap day and a year end.
SURFACES = [
    (ql.Date(15, ql.January, 2025),
     fixed_spec(ql.Date(15, ql.January, 2025), TARGET, ql.Following, 0.20, A365, SL, 0.0), [0.01, 0.03, 0.05]),
    (ql.Date(29, ql.February, 2024),
     fixed_spec(ql.Date(29, ql.February, 2024), UK_EXCHANGE, ql.ModifiedFollowing, 0.35, A360, SL, 0.02),
     [-0.015, 0.0, 0.045]),
    (ql.Date(30, ql.October, 2026),
     fixed_spec(ql.Date(31, ql.October, 2026), US_SOFR, ql.Preceding, 0.0085, THIRTY360_BB, N, 0.0),
     [-0.01, 0.02, 0.06]),
    (ql.Date(30, ql.June, 2027),
     fixed_spec(ql.Date(30, ql.June, 2027), NULL_CALENDAR, ql.ModifiedPreceding, 0.012, ACT_ISDA, N, 0.005),
     [-0.005, 0.0125, 0.1]),
    (ql.Date(31, ql.December, 2025),
     fixed_spec(ql.Date(31, ql.December, 2025), TARGET, ql.Unadjusted, 0.0, A365, SL, 0.5), [-0.4, 0.035, 1.5]),
    (ql.Date(15, ql.March, 2030),
     fixed_spec(ql.Date(15, ql.March, 2030), UK_EXCHANGE, ql.HalfMonthModifiedFollowing, 1.25, THIRTY360_EU, SL,
                1e-4), [0.0, 0.025, 0.25]),
    (ql.Date(15, ql.January, 2025),
     moving_spec(0, TARGET, ql.Following, 0.2, A365, SL, 0.0), [0.01, 0.03, 0.05]),
    (ql.Date(17, ql.January, 2025),
     moving_spec(2, TARGET, ql.ModifiedFollowing, 0.18, A360, SL, 0.01), [-0.005, 0.02, 0.08]),
    (ql.Date(18, ql.January, 2025),
     moving_spec(0, US_SOFR, ql.Following, 0.009, A365, N, 0.0), [-0.02, 0.04, 0.07]),
    (ql.Date(24, ql.December, 2026),
     moving_spec(3, UK_EXCHANGE, ql.Preceding, 0.3, THIRTY360_BB, SL, 0.03), [-0.02, 0.015, 0.2]),
    (ql.Date(28, ql.February, 2028),
     moving_spec(1, NULL_CALENDAR, ql.Unadjusted, 0.0075, ACT_ISDA, N, 0.02), [-0.03, 0.0, 0.03]),
    (ql.Date(31, ql.December, 2029),
     moving_spec(2, TARGET, ql.Nearest, 0.6, A365, SL, 0.0), [0.005, 0.04, 0.4]),
]
DATE_OFFSETS = [0, 1, 7, 31, 92, 183, 365, 731, 3653]
TIMES = [0.0, 1e-8, 0.25, 0.5, 1.0, 2.5, 10.0, 30.0]


def surface_cases():
    out = []
    for k, (evaluation_date, spec, strikes) in enumerate(SURFACES):
        st = v.settings(evaluation_date)
        s = make_surface(spec)
        ref = s.referenceDate()
        dates = [ref + n for n in DATE_OFFSETS] + [ql.Date.maxDate()]
        times = TIMES + [s.maxTime(), s.maxTime() + 1e-13]
        kind = "fixed" if "referenceDate" in spec else f"{spec['settlementDays']} days"
        out.append({
            "name": f"{kind} {evaluation_date.ISO()} {s.calendar().name()} {s.dayCounter().name()} "
                    f"{'Normal' if spec['volatilityType'] == N else 'ShiftedLognormal'} {spec['displacement']!r} / {k}",
            "inputs": {"settings": st, "surface": spec, "dates": v.enc(dates), "times": times, "strikes": strikes},
            "expected": {
                "referenceDate": v.enc(ref),
                "maxDate": v.enc(s.maxDate()),
                "minStrike": s.minStrike(),
                "maxStrike": s.maxStrike(),
                "volatilityType": int(s.volatilityType()),
                "displacement": s.displacement(),
                "volatilityAtDates": [[v.attempt(lambda d=d, x=x: s.volatility(d, x)) for x in strikes] for d in dates],
                "volatilityAtTimes": [[v.attempt(lambda t=t, x=x: s.volatility(t, x)) for x in strikes] for t in times],
                "blackVarianceAtDates": [[v.attempt(lambda d=d, x=x: s.blackVariance(d, x)) for x in strikes]
                                         for d in dates],
                "blackVarianceAtTimes": [[v.attempt(lambda t=t, x=x: s.blackVariance(t, x)) for x in strikes]
                                         for t in times]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/optionlet/constantoptionletvol.hpp", surface_cases)
