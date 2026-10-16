# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""30/360 under all nine conventions: the name, and the day count and year fraction from every second of 233 start
dates (every 61st day of 2000-2030, plus month ends, leap days and mid-months of 2000, 2023-2025 and 2100) to 13 end
dates 0 to 1826 days later, with no termination date. German and ISDA again with three termination dates (end of
February in a leap and in non-leap years), from four start dates to the termination date, the day before it and two
other dates. Read back by tests/NanoQL.Tests/Vectors/Time/DayCounters/Thirty360VectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

OFFSETS = [0, 1, 15, 29, 30, 31, 59, 90, 182, 365, 366, 731, 1826]
CONVENTIONS = ["USA", "BondBasis", "European", "EurobondBasis", "Italian", "German", "ISMA", "ISDA", "NASD"]


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
    T = ql.Thirty360
    out = []
    for name in CONVENTIONS:
        convention = getattr(T, name)
        dc = T(convention)
        for s in first_dates()[::2]:
            d1 = ql.Date(s)
            d2s = [d1 + k for k in OFFSETS]
            out.append({"name": f"Thirty360.{name} {s}",
                        "inputs": {"settings": st, "convention": int(convention), "terminationDate": None,
                                   "d1": v.enc(d1), "d2": v.enc(d2s)},
                        "expected": {"name": v.attempt(dc.name),
                                     "dayCount": [v.attempt(lambda x=x: dc.dayCount(d1, x)) for x in d2s],
                                     "yearFraction": [v.attempt(lambda x=x: dc.yearFraction(d1, x)) for x in d2s]}})
    for name in ["German", "ISDA"]:
        convention = getattr(T, name)
        for term in (ql.Date(28, ql.February, 2025), ql.Date(29, ql.February, 2024), ql.Date(28, ql.February, 2027)):
            dc = T(convention, term)
            for s in (ql.Date(28, ql.February, 2023), ql.Date(31, ql.August, 2024), ql.Date(30, ql.November, 2024),
                      ql.Date(31, ql.January, 2024)):
                d2s = [term, term - 1, ql.Date(28, ql.February, 2026), ql.Date(31, ql.March, 2026)]
                out.append({"name": f"Thirty360.{name} termination {term.serialNumber()} {s.serialNumber()}",
                            "inputs": {"settings": st, "convention": int(convention), "terminationDate": v.enc(term),
                                       "d1": v.enc(s), "d2": v.enc(d2s)},
                            "expected": {"name": v.attempt(dc.name),
                                         "dayCount": [v.attempt(lambda x=x: dc.dayCount(s, x)) for x in d2s],
                                         "yearFraction": [v.attempt(lambda x=x: dc.yearFraction(s, x))
                                                          for x in d2s]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/daycounters/thirty360.hpp", cases)
