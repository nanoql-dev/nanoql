# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Romania calendars, one case per market: the name, isWeekend for each weekday, and every holiday that falls on a
weekday from 1 January 1901 to 30 December 2199, the whole range QuantLib's holidayList can walk (it steps past
Date::maxDate() when its range ends on 31 December 2199, a documented divergence). The market is QuantLib's
Romania::Market integer. The case's "skip" input lists the dates QuantLib's master adds to both markets (#2919, taken
ahead of the pin as the upstream sweep's SWD14 rules), which the reader leaves out of nanoql's list: Orthodox Good
Friday from 2018, Epiphany and St John the Baptist (6 and 7 January) from 2024, but those that are no business day in
QuantLib 1.44 already (a weekend, or a Good Friday on May 1st). tests/NanoQL.Tests/Vectors/Time/
Calendars/RomaniaVectorTests.cs reads it."""
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
MARKETS = [("Public", 0), ("BVB", 1)]


def orthodox_easter_monday(y):
    """Orthodox Easter Monday: the Julian computus (Meeus), in the Gregorian calendar 13 days later to 2099 and 14
    from 2100, checked against the wheel's Easter Monday holiday."""
    a, b, c = y % 4, y % 7, y % 19
    d = (19 * c + 15) % 30
    e = (2 * a + 4 * b - d + 34) % 7
    month, day = divmod(d + e + 114, 31)
    em = ql.Date(day + 1, month, y) + (13 if y < 2100 else 14) + 1
    assert em.weekday() == ql.Monday and ql.Romania(ql.Romania.Public).isHoliday(em), y
    return em


def added_holidays():
    """The dates QuantLib's master adds to both markets (#2919) from 1901 to 2199: Orthodox Good Friday from 2018,
    Epiphany and St John the Baptist from 2024."""
    out = [orthodox_easter_monday(y) - 3 for y in range(2018, 2200)]
    out += [ql.Date(d, ql.January, y) for y in range(2024, 2200) for d in (6, 7)]
    return sorted(out)


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, market in MARKETS:
        c = ql.Romania(market) if market is not None else ql.Romania()
        start = FROM
        weekends = False
        skip = sorted(set(d.serialNumber() for d in added_holidays() if not c.isHoliday(d)))
        holidays = [d for d in c.holidayList(start, TO, weekends) if d.serialNumber() not in set(skip)]
        out.append({"name": "Romania" + ("." + name if name else ""),
                    "inputs": {"settings": st, "market": market, "from": v.enc(start), "to": v.enc(TO),
                               "weekdays": [int(w) for w in WEEKDAYS], "includeWeekEnds": weekends,
                               "skip": skip},
                    "expected": {"name": c.name(), "isWeekend": [c.isWeekend(w) for w in WEEKDAYS],
                                 "holidays": v.enc(holidays)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/calendars/romania.hpp", cases)
