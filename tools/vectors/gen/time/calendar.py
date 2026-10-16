# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The calendar interface (ql/time/calendar.hpp) on four calendars: TARGET, UnitedStates(Settlement),
UnitedKingdom(Exchange) and the joint calendar of TARGET and UnitedStates(Settlement) joining holidays.

- "date" cases, every third day from 1 December 2025 to 1 February 2027 for each calendar: isBusinessDay, isHoliday,
  isStartOfMonth, startOfMonth, isEndOfMonth, endOfMonth; adjust under each of the seven business-day conventions;
  advance by ten periods, two of them zero, each under Following without and ModifiedFollowing with the end-of-month
  rule; and
  businessDaysBetween to five spans, each including the first date only and the last date only.
- An "edited" case: a TARGET calendar with one holiday added and one removed, its business days over 92 days from
  1 October 2026, its added and removed holidays and its name. In QuantLib the edit changes every TARGET instance,
  so the generator restores the calendar with resetAddedAndRemovedHolidays() at once: no later case may see it.
- "lists" cases for each calendar: holidayList with weekends and businessDayList from 1 December 2026 to
  31 January 2027.

Each case's inputs.calendar describes its calendar: {"type": <class>}, with a "market" integer for UnitedStates and
UnitedKingdom, and for the joint calendar {"type": "JointCalendar", "calendars": [...], "rule": <integer>}. The
generator builds each QuantLib calendar from that description. Conventions and time units are QuantLib's integers.
tests/NanoQL.Tests/Vectors/Time/CalendarVectorTests.cs reads it."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TARGET = {"type": "TARGET"}
US_SETTLEMENT = {"type": "UnitedStates", "market": int(ql.UnitedStates.Settlement)}
UK_EXCHANGE = {"type": "UnitedKingdom", "market": int(ql.UnitedKingdom.Exchange)}
CALENDARS = [
    ("TARGET", TARGET),
    ("UnitedStates.Settlement", US_SETTLEMENT),
    ("UnitedKingdom.Exchange", UK_EXCHANGE),
    ("TARGET+US.Settlement/JoinHolidays",
     {"type": "JointCalendar", "calendars": [TARGET, US_SETTLEMENT], "rule": int(ql.JoinHolidays)}),
]

CONVENTIONS = [ql.Following, ql.ModifiedFollowing, ql.Preceding, ql.ModifiedPreceding, ql.Unadjusted,
               ql.HalfMonthModifiedFollowing, ql.Nearest]
ADVANCES = [(1, ql.Days), (-1, ql.Days), (5, ql.Days), (1, ql.Weeks), (1, ql.Months), (3, ql.Months),
            (-1, ql.Months), (1, ql.Years), (0, ql.Days), (0, ql.Months)]
ADVANCE_CONVENTIONS = [(ql.Following, False), (ql.ModifiedFollowing, True)]
SPANS = [0, 1, 7, 30, -5]
INCLUSIONS = [(True, False), (False, True)]


def make(spec):
    t = spec["type"]
    if t == "TARGET":
        return ql.TARGET()
    if t == "UnitedStates":
        return ql.UnitedStates(spec["market"])
    if t == "UnitedKingdom":
        return ql.UnitedKingdom(spec["market"])
    if t == "JointCalendar":
        return ql.JointCalendar(*[make(s) for s in spec["calendars"]], spec["rule"])
    raise ValueError(t)


def date_case(st, name, spec, c, d):
    return {"name": f"{name} {d.serialNumber()}",
            "inputs": {"settings": st, "kind": "date", "calendar": spec, "date": v.enc(d),
                       "conventions": [int(x) for x in CONVENTIONS],
                       "advances": [[n, int(u)] for n, u in ADVANCES],
                       "advanceConventions": [[int(x), e] for x, e in ADVANCE_CONVENTIONS],
                       "businessDaysBetween": SPANS,
                       "inclusions": [[f, la] for f, la in INCLUSIONS]},
            "expected": {"isBusinessDay": c.isBusinessDay(d), "isHoliday": c.isHoliday(d),
                         "isStartOfMonth": c.isStartOfMonth(d), "startOfMonth": v.enc(c.startOfMonth(d)),
                         "isEndOfMonth": c.isEndOfMonth(d), "endOfMonth": v.enc(c.endOfMonth(d)),
                         "adjust": [v.enc(c.adjust(d, x)) for x in CONVENTIONS],
                         "advance": [[v.attempt(lambda n=n, u=u, x=x, e=e: c.advance(d, n, u, x, e))
                                      for x, e in ADVANCE_CONVENTIONS] for n, u in ADVANCES],
                         "businessDaysBetween": [[c.businessDaysBetween(d, d + k, f, la) for f, la in INCLUSIONS]
                                                 for k in SPANS]}}


def edited_case(st):
    t = ql.TARGET()
    add, rem = ql.Date(15, ql.October, 2026), ql.Date(25, ql.December, 2026)
    window = [ql.Date(1, ql.October, 2026) + i for i in range(92)]
    try:
        t.addHoliday(add)
        t.removeHoliday(rem)
        return {"name": "TARGET edited",
                "inputs": {"settings": st, "kind": "edited", "calendar": TARGET, "added": [v.enc(add)],
                           "removed": [v.enc(rem)], "dates": v.enc(window)},
                "expected": {"isBusinessDay": [t.isBusinessDay(x) for x in window],
                             "addedHolidays": v.enc(list(t.addedHolidays())),
                             "removedHolidays": v.enc(list(t.removedHolidays())),
                             "name": t.name()}}
    finally:
        # QuantLib's TARGET instances share their holiday sets: undo the edit for every later case.
        t.resetAddedAndRemovedHolidays()


def lists_case(st, name, spec, c):
    a, b = ql.Date(1, ql.December, 2026), ql.Date(31, ql.January, 2027)
    return {"name": f"{name} lists",
            "inputs": {"settings": st, "kind": "lists", "calendar": spec, "from": v.enc(a), "to": v.enc(b)},
            "expected": {"holidayListWithWeekends": v.enc(list(c.holidayList(a, b, True))),
                         "businessDayList": v.enc(list(c.businessDayList(a, b)))}}


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    calendars = [(name, spec, make(spec)) for name, spec in CALENDARS]
    out = []
    for name, spec, c in calendars:
        d = ql.Date(1, ql.December, 2025)
        while d <= ql.Date(1, ql.February, 2027):
            out.append(date_case(st, name, spec, c, d))
            d = d + 3
    out.append(edited_case(st))
    for name, spec, c in calendars:
        out.append(lists_case(st, name, spec, c))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/calendar.hpp", cases)
