# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""30/365: the name, and the day count and year fraction from each of 233 start dates (every 61st day of 2000-2030,
plus month ends, leap days and mid-months of 2000, 2023-2025 and 2100) to 13 end dates 0 to 1826 days later and 11
whole-month steps, 1 to 60 months later and 1 to 12 earlier. Read back by
tests/NanoQL.Tests/Vectors/Time/DayCounters/Thirty365VectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

OFFSETS = [0, 1, 15, 29, 30, 31, 59, 90, 182, 365, 366, 731, 1826]
MONTHS = [1, 2, 3, 6, 12, 18, 24, 60, -1, -6, -12]


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


def make():
    return ql.Thirty365()


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    dc = make()
    out = []
    for s in first_dates():
        d1 = ql.Date(s)
        d2s = [d1 + k for k in OFFSETS] + [d1 + ql.Period(m, ql.Months) for m in MONTHS]
        out.append({"name": f"{dc.name()} {s}",
                    "inputs": {"settings": st, "d1": v.enc(d1), "d2": v.enc(d2s)},
                    "expected": {"name": v.attempt(dc.name),
                                 "dayCount": [v.attempt(lambda x=x: dc.dayCount(d1, x)) for x in d2s],
                                 "yearFraction": [v.attempt(lambda x=x: dc.yearFraction(d1, x)) for x in d2s]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/daycounters/thirty365.hpp", cases)
