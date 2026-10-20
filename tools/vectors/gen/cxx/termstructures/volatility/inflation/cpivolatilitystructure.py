# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CPI volatility structures (ql/termstructures/volatility/inflation/cpivolatilitystructure.hpp),
on QuantLib's ConstantCPIVolatility, each output computed by the C++ harness: the wheel binds no CPI volatility
structure.

One case per surface, each built from settlement days after the evaluation date (QuantLib's only form, which nanoql
builds by ConstantCPIVolatility.Create from the context), over four calendars, observation lags of 2 and 3 months, a
year and ten weeks, monthly and quarterly frequencies, interpolated and not: its referenceDate, maxDate, the
observation lag (length and units), frequency, indexIsInterpolated, baseDate, minStrike, maxStrike and baseLevel
(refused: no base level is set); volatility, totalVariance and timeFromBase at 12 dates (the reference date, dates
before the base date, which QuantLib refuses, and later ones up to Date::maxDate), each with the surface's own lag
(null, QuantLib's default Period(-1, Days)) and with an explicit lag, at 3 strikes (the domain's ends among them), as
[date][lag][strike]; volatility and totalVariance at 6 option tenors; and volatility on a time at 6 times (a negative
one among them, which the time form doesn't check), as [time][strike]. One surface is built with enableExtrapolation()
called.

A surface spec is {"type": "ConstantCPIVolatility", "volatility", "settlementDays", "calendar", "convention",
"dayCounter", "observationLag", "frequency", "indexIsInterpolated", "enableExtrapolation"}, as
tools/cxx/src/termstructures/volatility/inflation/inflationvolspec.hpp reads it. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/CPIVolatilitySurfaceCxxVectorTests.cs."""
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
    "wheel_yoyinflationoptionletvolatilitystructure",
    os.path.join(v.GEN_ROOT, "termstructures", "volatility", "inflation",
                 "yoyinflationoptionletvolatilitystructure.py"))
yoy = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(yoy)
irx = yoy.irx

M, D, W, Y = yoy.M, yoy.D, yoy.W, yoy.Y
MAX = 1.7976931348623157e308


def surface_spec(vol, days, cal, convention, dc, lag, frequency, interpolated, extrapolate=False):
    return {"type": "ConstantCPIVolatility", "volatility": vol, "settlementDays": days, "calendar": cal,
            "convention": int(convention), "dayCounter": dc, "observationLag": lag, "frequency": int(frequency),
            "indexIsInterpolated": interpolated, "enableExtrapolation": extrapolate}


SURFACES = [
    (ql.Date(15, ql.June, 2024), surface_spec(0.01, 2, irx.TARGET, ql.Following, irx.A365, [3, M], ql.Monthly, False)),
    (ql.Date(15, ql.June, 2024),
     surface_spec(0.0125, 0, irx.UK_EXCHANGE, ql.ModifiedFollowing, irx.A360, [2, M], ql.Monthly, True)),
    (ql.Date(29, ql.February, 2024),
     surface_spec(0.2, 1, irx.US_SOFR, ql.Preceding, irx.THIRTY360_BB, [3, M], ql.Quarterly, False, True)),
    (ql.Date(31, ql.December, 2025),
     surface_spec(0.0, 3, irx.NULL_CALENDAR, ql.Unadjusted, irx.A365, [1, Y], ql.Quarterly, True)),
    (ql.Date(18, ql.January, 2025),
     surface_spec(0.0075, 2, irx.TARGET, ql.ModifiedFollowing, irx.A365, [10, W], ql.Monthly, False)),
]
OFFSETS = yoy.OFFSETS
STRIKES = [0.025, -MAX, MAX]
TENORS = yoy.TENORS
TIMES = [0.0, -0.5, 0.25, 1.0, 10.0, 100.0]
LAGS = [None, [1, M]]
NAME = "CPIVolatilitySurface::"


def surface_case(H, k, evaluation_date, spec):
    with H.case_settings(v.enc(evaluation_date)) as st:
        ref = H.call(NAME + "referenceDate", spec)
        dates = [ref + n for n in OFFSETS] + [v.enc(ql.Date.maxDate())]

        def call(method, *args):
            return v.attempt(lambda: H.call(NAME + method, spec, *args))

        def grid(method):
            return [[[call(method, d, x, g, False) for x in STRIKES] for g in LAGS] for d in dates]

        lag = H.call(NAME + "observationLag", spec)
        expected = {
            "referenceDate": ref,
            "maxDate": H.call(NAME + "maxDate", spec),
            "observationLagLength": lag[0],
            "observationLagUnits": lag[1],
            "frequency": H.call(NAME + "frequency", spec),
            "indexIsInterpolated": H.call(NAME + "indexIsInterpolated", spec),
            "baseDate": H.call(NAME + "baseDate", spec),
            "minStrike": H.call(NAME + "minStrike", spec),
            "maxStrike": H.call(NAME + "maxStrike", spec),
            "baseLevel": call("baseLevel"),
            "volatility": grid("volatility(Date)"),
            "totalVariance": grid("totalVariance(Date)"),
            "timeFromBase": [[call("timeFromBase", d, g) for g in LAGS] for d in dates],
            "volatilityAtTenors": [[call("volatility(Period)", p, x, None, False) for x in STRIKES] for p in TENORS],
            "totalVarianceAtTenors": [[call("totalVariance(Period)", p, x, None, False) for x in STRIKES]
                                      for p in TENORS],
            "volatilityAtTimes": [[call("volatility(Time)", t, x) for x in STRIKES] for t in TIMES]}
        return {"name": f"surface {evaluation_date.ISO()} / {k}",
                "inputs": {"settings": st, "surface": spec, "dates": dates, "lags": LAGS, "strikes": STRIKES,
                           "tenors": TENORS, "times": TIMES},
                "expected": expected}


def cases():
    with cxx.connect() as H:
        return [surface_case(H, k, ev, spec) for k, (ev, spec) in enumerate(SURFACES)]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/inflation/cpivolatilitystructure.hpp", cases)
