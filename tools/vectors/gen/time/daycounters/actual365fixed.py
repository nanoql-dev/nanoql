# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Actual/365 (Fixed) under its three conventions. Standard and NoLeap: the name, and the day count and year fraction
from each of 233 start dates (every 61st day of 2000-2030, plus month ends, leap days and mid-months of 2000,
2023-2025 and 2100) to 13 end dates 0 to 1826 days later. Canadian: the name, and the year fraction from every second
start date to the start date itself, up to 8 end dates inside a 6-month and a 12-month reference period starting on it,
the reference period's end, and 15 days past it, so both of the convention's formulas are reached. Read back by
tests/NanoQL.Tests/Vectors/Time/DayCounters/Actual365FixedVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

OFFSETS = [0, 1, 15, 29, 30, 31, 59, 90, 182, 365, 366, 731, 1826]
REFERENCE_OFFSETS = [1, 15, 30, 59, 90, 120, 150, 181]


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


def dc_cases(label, convention, st):
    dc = ql.Actual365Fixed(convention)
    out = []
    for s in first_dates():
        d1 = ql.Date(s)
        d2s = [d1 + k for k in OFFSETS]
        out.append({"name": f"{label} {s}",
                    "inputs": {"settings": st, "convention": int(convention), "d1": v.enc(d1), "d2": v.enc(d2s)},
                    "expected": {"name": v.attempt(dc.name),
                                 "dayCount": [v.attempt(lambda x=x: dc.dayCount(d1, x)) for x in d2s],
                                 "yearFraction": [v.attempt(lambda x=x: dc.yearFraction(d1, x)) for x in d2s]}})
    return out


def ref_cases(label, convention, st, months):
    """End dates inside a reference period of `months` months starting at the start date."""
    dc = ql.Actual365Fixed(convention)
    out = []
    for s in first_dates()[::2]:
        d1 = ql.Date(s)
        ref_end = d1 + ql.Period(months, ql.Months)
        d2s = [d1] + [d1 + k for k in REFERENCE_OFFSETS if d1 + k <= ref_end] + [ref_end, ref_end + 15]
        out.append({"name": f"{label} ref{months}M {s}",
                    "inputs": {"settings": st, "convention": int(convention), "d1": v.enc(d1), "d2": v.enc(d2s),
                               "refPeriodStart": v.enc(d1), "refPeriodEnd": v.enc(ref_end)},
                    "expected": {"name": v.attempt(dc.name),
                                 "yearFraction": [v.attempt(lambda x=x: dc.yearFraction(d1, x, d1, ref_end))
                                                  for x in d2s]}})
    return out


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    A = ql.Actual365Fixed
    return (dc_cases("Actual365Fixed.Standard", A.Standard, st)
            + dc_cases("Actual365Fixed.NoLeap", A.NoLeap, st)
            + ref_cases("Actual365Fixed.Canadian", A.Canadian, st, 6)
            + ref_cases("Actual365Fixed.Canadian", A.Canadian, st, 12))


if __name__ == "__main__":
    v.main(__file__, "ql/time/daycounters/actual365fixed.hpp", cases)
