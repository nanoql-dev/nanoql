# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Joint calendars: five joins of two, three and four calendars under both rules, and a join of five calendars built
from a vector, each with its name, isWeekend for each weekday, and every holiday that falls on a weekday (by the joint
calendar's own weekend) from 1 January 1990 to 30 December 2060. Each case's inputs.calendar describes the joint
calendar: {"type": "JointCalendar", "calendars": [...], "rule": <JointCalendarRule integer>}, its components as
{"type": <class>} with a "market" integer for UnitedStates and UnitedKingdom. The generator builds each QuantLib
calendar from that description: a join of two, three or four calendars through the constructor taking that many, and
any other through the constructor taking a std::vector<Calendar> (the wheel's CalendarVector, passed a list).
tests/NanoQL.Tests/Vectors/Time/Calendars/JointCalendarVectorTests.cs reads it."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

FROM, TO = ql.Date(1, ql.January, 1990), ql.Date(30, ql.December, 2060)
WEEKDAYS = [ql.Sunday, ql.Monday, ql.Tuesday, ql.Wednesday, ql.Thursday, ql.Friday, ql.Saturday]

TARGET = {"type": "TARGET"}
WEEKENDS_ONLY = {"type": "WeekendsOnly"}


def us(market):
    return {"type": "UnitedStates", "market": int(market)}


def uk(market):
    return {"type": "UnitedKingdom", "market": int(market)}


def joint(rule, *calendars):
    return {"type": "JointCalendar", "calendars": list(calendars), "rule": int(rule)}


def make(spec):
    t = spec["type"]
    if t == "TARGET":
        return ql.TARGET()
    if t == "UnitedStates":
        return ql.UnitedStates(spec["market"])
    if t == "UnitedKingdom":
        return ql.UnitedKingdom(spec["market"])
    if t == "WeekendsOnly":
        return ql.WeekendsOnly()
    if t == "JointCalendar":
        calendars = [make(s) for s in spec["calendars"]]
        if len(calendars) in (2, 3, 4):
            return ql.JointCalendar(*calendars, spec["rule"])
        return ql.JointCalendar(calendars, spec["rule"])
    raise ValueError(t)


JOINTS = [
    ("TARGET+UK.Settlement/JoinHolidays",
     joint(ql.JoinHolidays, TARGET, uk(ql.UnitedKingdom.Settlement))),
    ("TARGET+UK.Settlement/JoinBusinessDays",
     joint(ql.JoinBusinessDays, TARGET, uk(ql.UnitedKingdom.Settlement))),
    ("US.Settlement+US.NYSE/JoinHolidays",
     joint(ql.JoinHolidays, us(ql.UnitedStates.Settlement), us(ql.UnitedStates.NYSE))),
    ("US.Settlement+UK.Exchange+TARGET/JoinBusinessDays",
     joint(ql.JoinBusinessDays, us(ql.UnitedStates.Settlement), uk(ql.UnitedKingdom.Exchange), TARGET)),
    ("US.SOFR+UK.Metals+TARGET+WeekendsOnly/JoinHolidays",
     joint(ql.JoinHolidays, us(ql.UnitedStates.SOFR), uk(ql.UnitedKingdom.Metals), TARGET, WEEKENDS_ONLY)),
    # Five calendars, as in QuantLib's own testJointCalendars, so only the vector constructor takes them.
    ("TARGET+UK.Settlement+US.NYSE+US.GovernmentBond+UK.Exchange/JoinHolidays",
     joint(ql.JoinHolidays, TARGET, uk(ql.UnitedKingdom.Settlement), us(ql.UnitedStates.NYSE),
           us(ql.UnitedStates.GovernmentBond), uk(ql.UnitedKingdom.Exchange))),
]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name, spec in JOINTS:
        c = make(spec)
        out.append({"name": name,
                    "inputs": {"settings": st, "calendar": spec, "from": v.enc(FROM), "to": v.enc(TO),
                               "weekdays": [int(w) for w in WEEKDAYS]},
                    "expected": {"name": c.name(), "isWeekend": [c.isWeekend(w) for w in WEEKDAYS],
                                 "holidays": v.enc(list(c.holidayList(FROM, TO, False)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/calendars/jointcalendar.hpp", cases)
