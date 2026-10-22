# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Actual/366, with and without includeLastDay: the name, and the day count and year fraction from each of 233 start
dates (every 61st day of 2000-2030, plus month ends, leap days and mid-months of 2000, 2023-2025 and 2100) to 13 end
dates 0 to 1826 days later. Read back by tests/NanoQL.Tests/Vectors/Time/DayCounters/Actual366VectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

OFFSETS = [0, 1, 15, 29, 30, 31, 59, 90, 182, 365, 366, 731, 1826]


def first_dates():
    ds = set()
    d = ql.Date(1, ql.January, 2000)
    while d <= ql.Date(31, ql.December, 2030):
        ds.add(d.serialNumber())
        d = d + 61
    for y in (2000, 2023, 2024, 2025, 2100):
        for (dd, m) in ((28, ql.February), (31, ql.January), (30, ql.April), (31, ql.March), (31, ql.August),
                        (30, ql.November), (31, ql.December), (30, ql.January), (15, ql.June)):
            ds.add(ql.Date(dd, m, y).serialNumber())
        if ql.Date.isLeap(y):
            ds.add(ql.Date(29, ql.February, y).serialNumber())
    return sorted(ds)


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for include_last_day, label in ((False, "Actual366"), (True, "Actual366(inc)")):
        dc = ql.Actual366(include_last_day)
        for s in first_dates():
            d1 = ql.Date(s)
            d2s = [d1 + k for k in OFFSETS]
            out.append({"name": f"{label} {s}",
                        "inputs": {"settings": st, "includeLastDay": include_last_day, "d1": v.enc(d1),
                                   "d2": v.enc(d2s)},
                        "expected": {"name": v.attempt(dc.name),
                                     "dayCount": [v.attempt(lambda x=x: dc.dayCount(d1, x)) for x in d2s],
                                     "yearFraction": [v.attempt(lambda x=x: dc.yearFraction(d1, x)) for x in d2s]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/daycounters/actual366.hpp", cases)
