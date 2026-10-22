# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Israel calendars, one case per market: the name, isWeekend for each weekday, and every holiday that falls on a
weekday from 22 January 1901 to 30 December 2199, the whole range QuantLib's holidayList can walk (it steps past
Date::maxDate() when its range ends on 31 December 2199, a documented divergence). The range starts on 22 January 1901
because QuantLib's rules read neighbouring days and throw before it (a documented divergence). The Tel Aviv stock
exchange's list holds its weekends too (includeWeekEnds = true): its weekend changed on 5 January 2026, which
isWeekend(Weekday) can't express, so its list without weekends is a documented divergence. The market is QuantLib's
Israel::Market integer. tests/NanoQL.Tests/Vectors/Time/Calendars/IsraelVectorTests.cs reads it."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

FROM, TO = ql.Date(22, ql.January, 1901), ql.Date(30, ql.December, 2199)
WEEKDAYS = [ql.Sunday, ql.Monday, ql.Tuesday, ql.Wednesday, ql.Thursday, ql.Friday, ql.Saturday]
MARKETS = [("TASE", 1), ("SHIR", 2), ("Telbor", 3)]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, market in MARKETS:
        c = ql.Israel(market) if market is not None else ql.Israel()
        weekends = market == int(ql.Israel.TASE)
        skip = []
        holidays = [d for d in c.holidayList(FROM, TO, weekends) if d.serialNumber() not in set(skip)]
        out.append({"name": "Israel" + ("." + name if name else ""),
                    "inputs": {"settings": st, "market": market, "from": v.enc(FROM), "to": v.enc(TO),
                               "weekdays": [int(w) for w in WEEKDAYS], "includeWeekEnds": weekends,
                               "skip": skip},
                    "expected": {"name": c.name(), "isWeekend": [c.isWeekend(w) for w in WEEKDAYS],
                                 "holidays": v.enc(holidays)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/calendars/israel.hpp", cases)
