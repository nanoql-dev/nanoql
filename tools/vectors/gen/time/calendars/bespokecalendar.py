# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Bespoke calendars (ql/time/calendars/bespokecalendar.hpp), one case each, built by QuantLib's members in order: the
weekend days added first, then holidays added and removed. Each gives the name, isWeekend for each weekday, the added
and removed holidays QuantLib records, every holiday of 2026 (weekends included), and the business days between eight
pairs of dates. Each case builds its own calendar, so QuantLib's linked copies don't arise. A case's inputs give the
name, the weekdays (QuantLib's Weekday integers), and the dates added and removed. Read back by
tests/NanoQL.Tests/Vectors/Time/Calendars/BespokeCalendarVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

WEEKDAYS = [ql.Sunday, ql.Monday, ql.Tuesday, ql.Wednesday, ql.Thursday, ql.Friday, ql.Saturday]
FROM, TO = ql.Date(1, ql.January, 2026), ql.Date(31, ql.December, 2026)
SPANS = [(ql.Date(1, 1, 2026), ql.Date(1, 1, 2027)), (ql.Date(15, 3, 2026), ql.Date(15, 9, 2026)),
         (ql.Date(6, 6, 2026), ql.Date(8, 6, 2026)), (ql.Date(1, 1, 2026), ql.Date(1, 1, 2026)),
         (ql.Date(31, 12, 2026), ql.Date(1, 1, 2026)), (ql.Date(1, 1, 1901), ql.Date(31, 12, 1901)),
         (ql.Date(4, 7, 2026), ql.Date(25, 12, 2026)), (ql.Date(1, 1, 2199), ql.Date(31, 12, 2199))]
D = ql.Date
CALENDARS = [
    ("", [], [], []),
    ("A", [ql.Sunday], [], []),
    ("Sat-Sun", [ql.Saturday, ql.Sunday], [D(1, 1, 2026), D(25, 12, 2026)], []),
    ("Fri-Sat", [ql.Friday, ql.Saturday], [D(1, 1, 2026), D(4, 7, 2026)], [D(3, 7, 2026)]),
    ("Weekend removed", [ql.Saturday, ql.Sunday], [D(6, 6, 2026)], [D(6, 6, 2026), D(7, 6, 2026)]),
    ("Every day", WEEKDAYS, [D(1, 1, 2026)], [D(1, 1, 2026), D(2, 1, 2026)]),
    ("Thu", [ql.Thursday], [D(14, 5, 2026), D(15, 5, 2026), D(16, 5, 2026)], [D(21, 5, 2026)]),
    ("Holidays only", [], [D(1, 1, 2026), D(2, 1, 2026), D(31, 12, 2026)], [D(2, 1, 2026)]),
]


def build(name, weekend, added, removed):
    c = ql.BespokeCalendar(name)
    for w in weekend:
        c.addWeekend(w)
    for d in added:
        c.addHoliday(d)
    for d in removed:
        c.removeHoliday(d)
    return c


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, weekend, added, removed in CALENDARS:
        c = build(name, weekend, added, removed)
        out.append({"name": f"BespokeCalendar '{name}'",
                    "inputs": {"settings": st, "calendarName": name, "weekend": [int(w) for w in weekend],
                               "added": v.enc(added), "removed": v.enc(removed),
                               "weekdays": [int(w) for w in WEEKDAYS], "from": v.enc(FROM), "to": v.enc(TO),
                               "spans": [v.enc(list(s)) for s in SPANS]},
                    "expected": {"name": c.name(),
                                 "isWeekend": [c.isWeekend(w) for w in WEEKDAYS],
                                 "addedHolidays": v.enc(list(c.addedHolidays())),
                                 "removedHolidays": v.enc(list(c.removedHolidays())),
                                 "holidays": v.enc(list(c.holidayList(FROM, TO, True))),
                                 "businessDaysBetween": [c.businessDaysBetween(a, b) for a, b in SPANS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/calendars/bespokecalendar.hpp", cases)
