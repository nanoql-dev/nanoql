# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Russia calendars, one case per market: the name, isWeekend for each weekday, and every holiday that falls on a
weekday from 1 January 1901 to 30 December 2199, the whole range QuantLib's holidayList can walk (it steps past
Date::maxDate() when its range ends on 31 December 2199, a documented divergence). The Moscow exchange's range starts on
1 January 2012, where QuantLib's data start: its calendar refuses every earlier year. The Moscow exchange's list holds
its weekend days too (includeWeekEnds = true), so its working weekends, weekend days that are business days, are read by
their absence. The market is QuantLib's Russia::Market integer.
tests/NanoQL.Tests/Vectors/Time/Calendars/RussiaVectorTests.cs reads it."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

FROM, TO = ql.Date(1, ql.January, 1901), ql.Date(30, ql.December, 2199)
MOEX_FROM = ql.Date(1, ql.January, 2012)
WEEKDAYS = [ql.Sunday, ql.Monday, ql.Tuesday, ql.Wednesday, ql.Thursday, ql.Friday, ql.Saturday]
MARKETS = [("Settlement", 0), ("MOEX", 1)]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, market in MARKETS:
        c = ql.Russia(market) if market is not None else ql.Russia()
        start = MOEX_FROM if market == int(ql.Russia.MOEX) else FROM
        weekends = market == int(ql.Russia.MOEX)
        skip = []
        holidays = [d for d in c.holidayList(start, TO, weekends) if d.serialNumber() not in set(skip)]
        out.append({"name": "Russia" + ("." + name if name else ""),
                    "inputs": {"settings": st, "market": market, "from": v.enc(start), "to": v.enc(TO),
                               "weekdays": [int(w) for w in WEEKDAYS], "includeWeekEnds": weekends,
                               "skip": skip},
                    "expected": {"name": c.name(), "isWeekend": [c.isWeekend(w) for w in WEEKDAYS],
                                 "holidays": v.enc(holidays)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/calendars/russia.hpp", cases)
