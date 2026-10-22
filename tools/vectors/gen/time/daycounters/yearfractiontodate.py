# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""yearFractionToDate, the "inverse" of a day counter: for each of testYearFraction2DateBulk's 28 day counters
(test-suite/daycounters.cpp:1316-1366), the dates the year fractions from a reference date to a target date reach, over
every 13th of the case's reference dates (1 January 2020 plus i days, the target i days further, i from -360 to 729),
with the year fractions themselves; and testYearFraction2DateRounding's two day counters (:1368-1386), the dates a year
fraction plus each of 21 offsets of 0 to 1 day reaches. Finite times only: a NaN time is a documented divergence. A
case's inputs give the day counter as {"type", "convention" or "includeLastDay"}, the reference dates and the times.
Read back by tests/NanoQL.Tests/Vectors/Time/DayCounters/YearFractionToDateVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

SPECS = (
    [{"type": "Actual365Fixed", "convention": int(ql.Actual365Fixed.Standard)},
     {"type": "Actual365Fixed", "convention": int(ql.Actual365Fixed.NoLeap)},
     {"type": "Actual360", "includeLastDay": False}, {"type": "Actual360", "includeLastDay": True},
     {"type": "Actual36525", "includeLastDay": False}, {"type": "Actual36525", "includeLastDay": True},
     {"type": "Actual364"},
     {"type": "Actual366", "includeLastDay": False}, {"type": "Actual366", "includeLastDay": True}]
    + [{"type": "ActualActual", "convention": int(getattr(ql.ActualActual, c))}
       for c in ("ISDA", "ISMA", "Bond", "Historical", "Actual365", "AFB", "Euro")]
    + [{"type": "Business252"}]
    + [{"type": "Thirty360", "convention": int(getattr(ql.Thirty360, c))}
       for c in ("USA", "BondBasis", "European", "EurobondBasis", "Italian", "German", "ISMA", "ISDA", "NASD")]
    + [{"type": "Thirty365"}, {"type": "SimpleDayCounter"}])


def build(spec):
    t = spec["type"]
    if t == "Actual365Fixed":
        return ql.Actual365Fixed(spec["convention"])
    if t in ("Actual360", "Actual36525", "Actual366"):
        return getattr(ql, t)(spec["includeLastDay"])
    if t in ("ActualActual", "Thirty360"):
        return getattr(ql, t)(spec["convention"])
    return getattr(ql, t)()


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for spec in SPECS:
        dc = build(spec)
        refs, times = [], []
        for i in range(-360, 730, 13):
            today = ql.Date(1, ql.January, 2020) + ql.Period(i, ql.Days)
            target = today + ql.Period(i, ql.Days)
            refs.append(today)
            times.append(dc.yearFraction(today, target))
        out.append({"name": f"bulk {dc.name()} {spec}",
                    "inputs": {"settings": st, "dayCounter": spec, "referenceDates": v.enc(refs),
                               "times": v.enc(times)},
                    "expected": {"dates": [v.attempt(lambda r=r, t=t: ql.yearFractionToDate(dc, r, t))
                                           for r, t in zip(refs, times)]}})
    d1, d2 = ql.Date(1, ql.February, 2023), ql.Date(17, ql.February, 2124)
    for spec in ({"type": "Thirty360", "convention": int(ql.Thirty360.USA)},
                 {"type": "Actual360", "includeLastDay": True}):
        dc = build(spec)
        t = dc.yearFraction(d1, d2)
        times = [t + (k * 0.05) / 360 for k in range(21)]
        out.append({"name": f"rounding {dc.name()}",
                    "inputs": {"settings": st, "dayCounter": spec, "referenceDates": v.enc([d1] * len(times)),
                               "times": v.enc(times)},
                    "expected": {"dates": [v.attempt(lambda x=x: ql.yearFractionToDate(dc, d1, x)) for x in times]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/daycounters/yearfractiontodate.hpp", cases)
