# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year optionlet volatility structures
(ql/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.hpp), through
ConstantYoYOptionletVolatility and the YoYOptionletVolatilitySurface interface it inherits.

One case per surface, each built from settlement days after the evaluation date (QuantLib's only form, which nanoql
builds by ConstantYoYOptionletVolatility.Create from the context), over four calendars, observation lags of 2 and 3
months and of a quarter, monthly and quarterly frequencies, interpolated and not, and several strike ranges: its
referenceDate, maxDate, the observation lag (length and units), frequency, indexIsInterpolated, baseDate, minStrike,
maxStrike and baseLevel (refused: no base level is set); volatility, totalVariance and timeFromBase at 12 dates (the
reference date, dates before the base date, which QuantLib refuses, and later ones up to Date::maxDate), each with
the surface's own lag (QuantLib's default Period(-1, Days)) and with an explicit lag, at 4 strikes (two outside the
range, refused unless extrapolating), as [date][lag][strike]; and volatility and totalVariance at 6 option tenors.
The wheel binds neither the surface's volatility type and displacement nor the time form of volatility; the harness
covers them (tools/vectors/gen/cxx/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.py).

A surface spec is {"type": "ConstantYoYOptionletVolatility", "volatility", "settlementDays", "calendar",
"convention", "dayCounter", "observationLag" ([length, units]), "frequency", "indexIsInterpolated", "minStrike",
"maxStrike", "volatilityType", "displacement", "enableExtrapolation"}, the calendars and day counters as in
tools/vectors/gen/indexes/interestrateindex.py; the volatility type and displacement are QuantLib's defaults here, the
only ones the wheel's constructor takes. The surface functions here are shared by the harness generators, which import
this file. Read back by tests/NanoQL.Tests/Vectors/TermStructures/YoYOptionletVolatilitySurfaceVectorTests.cs."""
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

M, D, W, Y = int(ql.Months), int(ql.Days), int(ql.Weeks), int(ql.Years)
SL = int(ql.ShiftedLognormal)


def surface_spec(vol, days, cal, convention, dc, lag, frequency, interpolated, min_strike=-1.0, max_strike=100.0,
                 vol_type=SL, displacement=0.0, extrapolate=False):
    return {"type": "ConstantYoYOptionletVolatility", "volatility": vol, "settlementDays": days, "calendar": cal,
            "convention": int(convention), "dayCounter": dc, "observationLag": lag, "frequency": int(frequency),
            "indexIsInterpolated": interpolated, "minStrike": min_strike, "maxStrike": max_strike,
            "volatilityType": vol_type, "displacement": displacement, "enableExtrapolation": extrapolate}


def make_surface(spec):
    """The wheel's surface a spec describes, built on the evaluation date the caller has set."""
    s = ql.ConstantYoYOptionletVolatility(
        spec["volatility"], spec["settlementDays"], irx.calendar(spec["calendar"]), spec["convention"],
        irx.day_counter(spec["dayCounter"]), ql.Period(*spec["observationLag"]), spec["frequency"],
        spec["indexIsInterpolated"], spec["minStrike"], spec["maxStrike"])
    if spec["enableExtrapolation"]:
        s.enableExtrapolation()
    return s


# (evaluation date, spec)
SURFACES = [
    (ql.Date(15, ql.June, 2024),
     surface_spec(0.01, 2, irx.TARGET, ql.Following, irx.A365, [3, M], ql.Monthly, False)),
    (ql.Date(15, ql.June, 2024),
     surface_spec(0.0125, 0, irx.UK_EXCHANGE, ql.ModifiedFollowing, irx.A360, [2, M], ql.Monthly, True,
                  -0.02, 0.08)),
    (ql.Date(29, ql.February, 2024),
     surface_spec(0.2, 1, irx.US_SOFR, ql.Preceding, irx.THIRTY360_BB, [3, M], ql.Quarterly, False, -0.5, 0.5,
                  extrapolate=True)),
    (ql.Date(31, ql.December, 2025),
     surface_spec(0.0, 3, irx.NULL_CALENDAR, ql.Unadjusted, irx.A365, [1, Y], ql.Quarterly, True)),
    (ql.Date(18, ql.January, 2025),
     surface_spec(0.0075, 2, irx.TARGET, ql.ModifiedFollowing, irx.A365, [10, W], ql.Monthly, False, 0.0, 0.05)),
]
OFFSETS = [0, -1, -45, -200, 1, 30, 92, 183, 365, 1096, 3653]
STRIKES_INSIDE = [0.0, 0.025]
TENORS = [[0, D], [1, M], [6, M], [1, Y], [5, Y], [30, Y]]
NO_LAG = None


def lag_period(lag):
    return ql.Period(-1, ql.Days) if lag is None else ql.Period(*lag)


def surface_cases():
    out = []
    for k, (evaluation_date, spec) in enumerate(SURFACES):
        with v.case_settings(evaluation_date) as st:
            s = make_surface(spec)
            ref = s.referenceDate()
            dates = [ref + n for n in OFFSETS] + [ql.Date.maxDate()]
            lags = [NO_LAG, [1, M]]
            strikes = STRIKES_INSIDE + [spec["minStrike"] - 0.5, spec["maxStrike"] + 0.5]
            lag = s.observationLag()

            def grid(f):
                return [[[v.attempt(lambda d=d, g=g, x=x: f(d, x, lag_period(g))) for x in strikes] for g in lags]
                        for d in dates]

            out.append({
                "name": f"surface {evaluation_date.ISO()} {s.calendar().name()} {s.dayCounter().name()} / {k}",
                "inputs": {"settings": st, "surface": spec, "dates": v.enc(dates), "lags": lags, "strikes": strikes,
                           "tenors": TENORS},
                "expected": {
                    "referenceDate": v.enc(ref),
                    "maxDate": v.enc(s.maxDate()),
                    "observationLagLength": lag.length(),
                    "observationLagUnits": int(lag.units()),
                    "frequency": int(s.frequency()),
                    "indexIsInterpolated": s.indexIsInterpolated(),
                    "baseDate": v.enc(s.baseDate()),
                    "minStrike": s.minStrike(),
                    "maxStrike": s.maxStrike(),
                    "baseLevel": v.attempt(s.baseLevel),
                    "volatility": grid(lambda d, x, g: s.volatility(d, x, g)),
                    "totalVariance": grid(lambda d, x, g: s.totalVariance(d, x, g)),
                    "timeFromBase": [[v.attempt(lambda d=d, g=g: s.timeFromBase(d, lag_period(g))) for g in lags]
                                     for d in dates],
                    "volatilityAtTenors": [[v.attempt(lambda p=p, x=x: s.volatility(ql.Period(*p), x))
                                            for x in strikes] for p in TENORS],
                    "totalVarianceAtTenors": [[v.attempt(lambda p=p, x=x: s.totalVariance(ql.Period(*p), x))
                                               for x in strikes] for p in TENORS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.hpp",
           surface_cases)
