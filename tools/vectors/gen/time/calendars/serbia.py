# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Serbia calendars, one case per market: the name, isWeekend for each weekday, and every holiday that falls on a
weekday from 1 January 1901 to 30 December 2199, the whole range QuantLib's holidayList can walk (it steps past
Date::maxDate() when its range ends on 31 December 2199, a documented divergence). The dates a documented divergence
changes are listed as the case's skip input and left out of the holidays; the reader leaves them out of nanoql's list
too. The market is QuantLib's Serbia::Market integer. tests/NanoQL.Tests/Vectors/Time/Calendars/SerbiaVectorTests.cs
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
MARKETS = [("BSE", 0)]


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


def orthodox_easter_sunday(y):
    """Orthodox Easter Sunday: Meeus's Julian computus, moved to the Gregorian calendar (13 days until 2099, 14
    after)."""
    a, b, c = y % 4, y % 7, y % 19
    d = (19 * c + 15) % 30
    e = (2 * a + 4 * b - d + 34) % 7
    month = (d + e + 114) // 31
    day = (d + e + 114) % 31 + 1
    return ql.Date(day, month, y) + (y // 100 - y // 400 - 2)


def skipped(market, c):
    """The dates the fix changes: the Western and the Orthodox Good Friday and Easter Monday, every year."""
    out = []
    for y in range(1901, 2200):
        for easter in (easter_sunday(y), orthodox_easter_sunday(y)):
            out += [easter - 2, easter + 1]
    return out


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, market in MARKETS:
        c = ql.Serbia(market) if market is not None else ql.Serbia()
        start = FROM
        weekends = False
        skip = sorted(set(d.serialNumber() for d in skipped(market, c)))
        holidays = [d for d in c.holidayList(start, TO, weekends) if d.serialNumber() not in set(skip)]
        out.append({"name": "Serbia" + ("." + name if name else ""),
                    "inputs": {"settings": st, "market": market, "from": v.enc(start), "to": v.enc(TO),
                               "weekdays": [int(w) for w in WEEKDAYS], "includeWeekEnds": weekends,
                               "skip": skip},
                    "expected": {"name": c.name(), "isWeekend": [c.isWeekend(w) for w in WEEKDAYS],
                                 "holidays": v.enc(holidays)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/calendars/serbia.hpp", cases)
