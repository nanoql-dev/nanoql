# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Schedule and MakeSchedule (ql/time/schedule.hpp): seeded rule-based schedules across effective
dates, maturities, tenors, calendars, conventions, every date-generation rule, end of month, first and next-to-last
dates (many of them throw); schedules with a null effective date, placed from the evaluation date and the termination
or next-to-last date; previousDate, nextDate, after and until, and previousDate and nextDate with a null reference date
under several evaluation dates; MakeSchedule, forwards, backwards and with a rule; and CDS schedules whose effective
date is a 20th falling on a weekend, which add a stub before it.

MakeSchedule is called through the wheel's _MakeSchedule, the proxy of the C++ class QuantLib::MakeSchedule, whose
schedule() method is MakeSchedule's conversion to Schedule. (The wheel's Python-side MakeSchedule function only calls
_MakeSchedule's methods in turn and adds no logic.)"""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402
from QuantLib.QuantLib import _MakeSchedule  # noqa: E402

import vectors as v  # noqa: E402

UNITS = [ql.Days, ql.Weeks, ql.Months, ql.Years]
RULES = ["Backward", "Forward", "Zero", "ThirdWednesday", "ThirdWednesdayInclusive", "Twentieth", "TwentiethIMM",
         "OldCDS", "CDS", "CDS2015"]


def dates_of(make):
    return v.attempt(lambda: [d.serialNumber() for d in make().dates()])


def regular_of(make):
    def flags():
        s = make()
        return [bool(x) for x in s.isRegular()] if s.hasIsRegular() else None
    return v.attempt(flags)


def outputs(make):
    return {"dates": dates_of(make), "isRegular": regular_of(make)}


def cases():
    rnd = random.Random(v.SEED)
    us = ql.UnitedStates
    calendars = {"TARGET": ql.TARGET(), "UnitedStates.Settlement": us(us.Settlement), "NullCalendar": ql.NullCalendar()}
    conventions = [ql.Unadjusted, ql.Following, ql.ModifiedFollowing, ql.Preceding]
    tenors = [(1, 0), (4, 1), (1, 2), (3, 2), (6, 2), (1, 3), (0, 0)]
    effectives = [ql.Date(31, 1, 2026), ql.Date(29, 2, 2024), ql.Date(15, 10, 2026), ql.Date(20, 3, 2026),
                  ql.Date(17, 6, 2026), ql.Date(30, 11, 2025), ql.Date(1, 1, 2027), ql.Date(28, 2, 2026)]
    maturities = [(2, 3), (5, 3), (10, 3), (25, 2), (18, 2), (3, 1)]
    out = []
    for i in range(700):
        eff = rnd.choice(effectives)
        # the length and the unit are drawn from two independent picks
        mat = eff + ql.Period(rnd.choice(maturities)[0], UNITS[rnd.choice(maturities)[1]])
        tn = rnd.choice(tenors)
        if tn == (1, 0):
            mat = eff + ql.Period(3, ql.Months)  # daily schedules only over three months
        cn = rnd.choice(list(calendars))
        conv, tconv = rnd.choice(conventions), rnd.choice(conventions)
        rule = rnd.choice(RULES)
        eom = rnd.random() < 0.4
        first = rnd.choice([None, None, None, eff + ql.Period(rnd.choice([1, 2, 4]), ql.Months)])
        ntl = rnd.choice([None, None, None, mat - ql.Period(rnd.choice([1, 2, 5]), ql.Months)])
        st = v.settings(rnd.choice([ql.Date(15, ql.October, 2026), ql.Date(1, ql.March, 2025)]))

        def make(eff=eff, mat=mat, tn=tn, cn=cn, conv=conv, tconv=tconv, rule=rule, eom=eom, first=first, ntl=ntl):
            return ql.Schedule(eff, mat, ql.Period(tn[0], UNITS[tn[1]]), calendars[cn], conv, tconv,
                               getattr(ql.DateGeneration, rule), eom, first or ql.Date(), ntl or ql.Date())
        out.append({"name": f"schedule {i}",
                    "inputs": {"settings": st, "kind": "rule", "effectiveDate": eff.serialNumber(),
                               "terminationDate": mat.serialNumber(), "tenor": list(tn), "calendar": cn,
                               "convention": int(conv), "terminationDateConvention": int(tconv),
                               "rule": RULES.index(rule), "endOfMonth": eom,
                               "firstDate": first.serialNumber() if first else None,
                               "nextToLastDate": ntl.serialNumber() if ntl else None},
                    "expected": outputs(make)})
    # a null effective date: QuantLib places it from the evaluation date
    for i, (term, ev) in enumerate([(ql.Date(15, 6, 2030), ql.Date(15, 10, 2026)),
                                    (ql.Date(31, 12, 2028), ql.Date(1, 2, 2025)),
                                    (ql.Date(15, 6, 2030), ql.Date(15, 6, 2031))]):
        st = v.settings(ev)

        def make(term=term):
            return ql.Schedule(ql.Date(), term, ql.Period(6, ql.Months), ql.TARGET(), ql.Following, ql.Following,
                               ql.DateGeneration.Backward, False)
        out.append({"name": f"null effective date {i}",
                    "inputs": {"settings": st, "kind": "rule", "effectiveDate": None,
                               "terminationDate": term.serialNumber(), "tenor": [6, 2], "calendar": "TARGET",
                               "convention": int(ql.Following), "terminationDateConvention": int(ql.Following),
                               "rule": 0, "endOfMonth": False, "firstDate": None, "nextToLastDate": None},
                    "expected": outputs(make)})
    st = v.settings(ql.Date(15, ql.October, 2026))
    base = ql.Schedule(ql.Date(31, 1, 2026), ql.Date(31, 1, 2031), ql.Period(6, ql.Months), ql.TARGET(),
                       ql.ModifiedFollowing, ql.ModifiedFollowing, ql.DateGeneration.Backward, True)
    refs = [base.dates()[0] - 1, base.dates()[0], ql.Date(15, ql.October, 2026), base.dates()[3], base.dates()[-1],
            base.dates()[-1] + 1]
    out.append({"name": "navigation",
                "inputs": {"settings": st, "kind": "navigation", "refDates": [r.serialNumber() for r in refs]},
                "expected": {"previousDate": [v.enc(base.previousDate(r)) for r in refs],
                             "nextDate": [v.enc(base.nextDate(r)) for r in refs],
                             "afterDates": [dates_of(lambda r=r: base.after(r)) for r in refs],
                             "afterIsRegular": [regular_of(lambda r=r: base.after(r)) for r in refs],
                             "untilDates": [dates_of(lambda r=r: base.until(r)) for r in refs],
                             "untilIsRegular": [regular_of(lambda r=r: base.until(r)) for r in refs]}})
    for i in range(120):
        eff = rnd.choice(effectives)
        mat = eff + ql.Period(rnd.choice([2, 5, 7]), ql.Years)
        tn = rnd.choice(tenors[:-1])
        cn = rnd.choice(list(calendars))
        fwd = rnd.random() < 0.5
        eom = rnd.random() < 0.5
        st = v.settings(ql.Date(15, ql.October, 2026))

        def make(eff=eff, mat=mat, tn=tn, cn=cn, fwd=fwd, eom=eom):
            ms = _MakeSchedule()
            ms.fromDate(eff)
            ms.to(mat)
            ms.withTenor(ql.Period(tn[0], UNITS[tn[1]]))
            ms.withCalendar(calendars[cn])
            ms.withConvention(ql.ModifiedFollowing)
            if fwd:
                ms.forwards()
            else:
                ms.backwards()
            ms.endOfMonth(eom)
            return ms.schedule()
        out.append({"name": f"MakeSchedule {i}",
                    "inputs": {"settings": st, "kind": "make", "effectiveDate": eff.serialNumber(),
                               "terminationDate": mat.serialNumber(), "tenor": list(tn), "calendar": cn,
                               "convention": int(ql.ModifiedFollowing), "forwards": fwd, "endOfMonth": eom},
                    "expected": outputs(make)})
    out += later_cases()
    return out


def rule_case(name, st, eff, mat, tn, cn, conv, tconv, rule, eom, first=None, ntl=None):
    calendars = {"TARGET": ql.TARGET(), "NullCalendar": ql.NullCalendar()}

    def make():
        return ql.Schedule(eff or ql.Date(), mat, ql.Period(tn[0], UNITS[tn[1]]), calendars[cn], conv, tconv,
                           getattr(ql.DateGeneration, rule), eom, first or ql.Date(), ntl or ql.Date())
    return {"name": name,
            "inputs": {"settings": st, "kind": "rule", "effectiveDate": v.enc(eff), "terminationDate": v.enc(mat),
                       "tenor": list(tn), "calendar": cn, "convention": int(conv), "terminationDateConvention": int(tconv),
                       "rule": RULES.index(rule), "endOfMonth": eom, "firstDate": v.enc(first),
                       "nextToLastDate": v.enc(ntl)},
            "expected": outputs(make)}


def later_cases():
    out = []
    # a null effective date placed from the next-to-last date, under evaluation dates before and after it
    for i, ev in enumerate([ql.Date(15, 10, 2026), ql.Date(1, 2, 2025), ql.Date(15, 6, 2031)]):
        out.append(rule_case(f"null effective date from next-to-last date {i}", v.settings(ev), None,
                             ql.Date(15, 6, 2030), (6, 2), "TARGET", ql.Following, ql.Following, "Backward", False,
                             ntl=ql.Date(15, 12, 2029)))
    # previousDate and nextDate with a null reference date: QuantLib reads the evaluation date
    base = ql.Schedule(ql.Date(31, 1, 2026), ql.Date(31, 1, 2031), ql.Period(6, ql.Months), ql.TARGET(),
                       ql.ModifiedFollowing, ql.ModifiedFollowing, ql.DateGeneration.Backward, True)
    for ev in [base.dates()[0] - 1, base.dates()[0], ql.Date(15, 10, 2026), base.dates()[3], base.dates()[-1],
               base.dates()[-1] + 1]:
        st = v.settings(ev)
        previous, following = base.previousDate(ql.Date()), base.nextDate(ql.Date())
        out.append({"name": f"navigation from the evaluation date {ev.serialNumber()}",
                    "inputs": {"settings": st, "kind": "navigationDefault"},
                    "expected": {"previousDate": v.enc(previous), "nextDate": v.enc(following)}})
    # MakeSchedule with a rule
    for i, (rule, tn) in enumerate([("Zero", (6, 2)), ("ThirdWednesday", (3, 2)), ("Twentieth", (3, 2)),
                                    ("TwentiethIMM", (3, 2)), ("OldCDS", (3, 2)), ("CDS", (3, 2)),
                                    ("CDS2015", (3, 2)), ("Forward", (6, 2)), ("Backward", (1, 3))]):
        eff, mat = ql.Date(17, 6, 2026), ql.Date(17, 6, 2031)
        st = v.settings(ql.Date(15, ql.October, 2026))

        def make(rule=rule, tn=tn, eff=eff, mat=mat):
            ms = _MakeSchedule()
            ms.fromDate(eff)
            ms.to(mat)
            ms.withTenor(ql.Period(tn[0], UNITS[tn[1]]))
            ms.withCalendar(ql.TARGET())
            ms.withConvention(ql.Following)
            ms.withRule(getattr(ql.DateGeneration, rule))
            return ms.schedule()
        out.append({"name": f"MakeSchedule with a rule {i}",
                    "inputs": {"settings": st, "kind": "make", "effectiveDate": eff.serialNumber(),
                               "terminationDate": mat.serialNumber(), "tenor": list(tn), "calendar": "TARGET",
                               "convention": int(ql.Following), "rule": RULES.index(rule), "endOfMonth": False},
                    "expected": outputs(make)})
    # CDS effective dates on a 20th that falls on a weekend: the adjusted 20th is later, so a stub comes first
    st = v.settings(ql.Date(15, ql.October, 2026))
    for eff in [ql.Date(20, 9, 2026), ql.Date(20, 6, 2026)]:
        for rule in ["CDS", "CDS2015"]:
            out.append(rule_case(f"{rule} weekend 20th {eff.serialNumber()}", st, eff, eff + ql.Period(5, ql.Years),
                                 (3, 2), "TARGET", ql.Following, ql.Unadjusted, rule, False))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/schedule.hpp", cases)
