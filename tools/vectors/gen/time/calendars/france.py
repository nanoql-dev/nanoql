# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The France calendars, one case per market: the name, isWeekend for each weekday, and every holiday that falls on a
weekday from 1 January 1901 to 30 December 2199, the whole range QuantLib's holidayList can walk (it steps past
Date::maxDate() when its range ends on 31 December 2199, a documented divergence). The dates a documented divergence
changes are listed as the case's skip input and left out of the holidays; the reader leaves them out of nanoql's list
too. The market is QuantLib's France::Market integer. tests/NanoQL.Tests/Vectors/Time/Calendars/FranceVectorTests.cs
reads it."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

FROM, TO = ql.Date(1, ql.January, 1901), ql.Date(30, ql.December, 2199)
WEEKDAYS = [ql.Sunday, ql.Monday, ql.Tuesday, ql.Wednesday, ql.Thursday, ql.Friday, ql.Saturday]
MARKETS = [("Settlement", 0), ("Exchange", 1)]


def easter_sunday(y):
    """Western Easter Sunday by the anonymous Gregorian computus."""
    a, b, c = y % 19, y // 100, y % 100
    d, e = b // 4, b % 4
    g = (8 * b + 13) // 25
    h = (19 * a + b - d - g + 15) % 30
    i, k = c // 4, c % 4
    l_ = (32 + 2 * e + 2 * i - h - k) % 7
    m = (a + 11 * h + 19 * l_) // 433
    month = (h + l_ - 7 * m + 90) // 25
    day = (h + l_ - 7 * m + 33 * month + 19) % 32
    return ql.Date(day, month, y)


def skipped(market, c):
    """The dates the fix changes, in the settlement calendar: QuantLib's May 10th and 21st, and Ascension and Whit
    Monday (Easter Monday plus 38 and 49 days), every year; the exchange calendar is unchanged."""
    if market != int(ql.France.Settlement):
        return []
    out = []
    for y in range(1901, 2200):
        em = easter_sunday(y) + 1
        out += [ql.Date(10, ql.May, y), ql.Date(21, ql.May, y), em + 38, em + 49]
    return out


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, market in MARKETS:
        c = ql.France(market) if market is not None else ql.France()
        weekends = False
        skip = sorted(set(d.serialNumber() for d in skipped(market, c)))
        holidays = [d for d in c.holidayList(FROM, TO, weekends) if d.serialNumber() not in set(skip)]
        out.append({"name": "France" + ("." + name if name else ""),
                    "inputs": {"settings": st, "market": market, "from": v.enc(FROM), "to": v.enc(TO),
                               "weekdays": [int(w) for w in WEEKDAYS], "includeWeekEnds": weekends,
                               "skip": skip},
                    "expected": {"name": c.name(), "isWeekend": [c.isWeekend(w) for w in WEEKDAYS],
                                 "holidays": v.enc(holidays)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/calendars/france.hpp", cases)
