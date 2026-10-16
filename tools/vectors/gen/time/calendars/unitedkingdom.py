# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The United Kingdom calendars, one case per market (all three): the name, isWeekend for each weekday, and every
holiday that falls on a weekday from 1 January 1901 to 30 December 2199, the whole range holidayList can walk (it steps
past Date::maxDate() when its range ends on 31 December 2199). The market is QuantLib's UnitedKingdom::Market integer.
tests/NanoQL.Tests/Vectors/Time/Calendars/UnitedKingdomVectorTests.cs reads it."""
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
MARKETS = ["Settlement", "Exchange", "Metals"]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for m in MARKETS:
        market = getattr(ql.UnitedKingdom, m)
        c = ql.UnitedKingdom(market)
        out.append({"name": "UnitedKingdom." + m,
                    "inputs": {"settings": st, "market": int(market), "from": v.enc(FROM), "to": v.enc(TO),
                               "weekdays": [int(w) for w in WEEKDAYS]},
                    "expected": {"name": c.name(), "isWeekend": [c.isWeekend(w) for w in WEEKDAYS],
                                 "holidays": v.enc(list(c.holidayList(FROM, TO, False)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/calendars/unitedkingdom.hpp", cases)
