# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Business/252 on four calendars (QuantLib's default, Brazil's settlement calendar; BOVESPA; TARGET; the New York Stock
Exchange): the name, and the day count and year fraction from each of 107 start dates (every 183rd day of 2000-2030,
plus month ends, leap days and mid-months of 2000, 2023-2025 and 2100) to 16 end dates from 400 days before to 30 years
after, across months and years, so QuantLib's walk over partial months, whole months and whole years is read.

QuantLib caches each month's count by the calendar's name and never invalidates it (business252.cpp:31-68); no case
edits a calendar, so every count here is the calendar's own. A case's inputs give the calendar as {"calendar": "Brazil"
| "TARGET" | "UnitedStates", "market": QuantLib's Market integer or null}, and "default": true for Business252() itself.
Read back by tests/NanoQL.Tests/Vectors/Time/DayCounters/Business252VectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

OFFSETS = [-400, -45, -1, 0, 1, 15, 31, 59, 90, 182, 365, 366, 731, 1826, 3653, 10958]
CALENDARS = [
    ("Brazil", None, True),
    ("Brazil", int(ql.Brazil.Exchange), False),
    ("TARGET", None, False),
    ("UnitedStates", int(ql.UnitedStates.NYSE), False),
]


def first_dates():
    ds = set()
    d = ql.Date(1, ql.January, 2000)
    while d <= ql.Date(31, ql.December, 2030):
        ds.add(d.serialNumber())
        d = d + 183
    for y in (2000, 2023, 2024, 2025, 2100):
        for (dd, m) in ((28, ql.February), (31, ql.January), (30, ql.April), (31, ql.March), (31, ql.August),
                        (30, ql.November), (31, ql.December), (30, ql.January), (15, ql.June)):
            ds.add(ql.Date(dd, m, y).serialNumber())
        if ql.Date.isLeap(y):
            ds.add(ql.Date(29, ql.February, y).serialNumber())
    return sorted(ds)


def day_counter(name, market, default):
    if default:
        return ql.Business252()
    if name == "Brazil":
        return ql.Business252(ql.Brazil(market))
    if name == "TARGET":
        return ql.Business252(ql.TARGET())
    return ql.Business252(ql.UnitedStates(market))


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, market, default in CALENDARS:
        dc = day_counter(name, market, default)
        for s in first_dates():
            d1 = ql.Date(s)
            d2s = [d1 + k for k in OFFSETS]
            out.append({"name": f"{dc.name()} {s}",
                        "inputs": {"settings": st, "calendar": name, "market": market, "default": default,
                                   "d1": v.enc(d1), "d2": v.enc(d2s)},
                        "expected": {"name": v.attempt(dc.name),
                                     "dayCount": [v.attempt(lambda x=x: dc.dayCount(d1, x)) for x in d2s],
                                     "yearFraction": [v.attempt(lambda x=x: dc.yearFraction(d1, x)) for x in d2s]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/daycounters/business252.hpp", cases)
