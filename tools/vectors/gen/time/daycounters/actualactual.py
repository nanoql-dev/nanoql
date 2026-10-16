# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Actual/Actual under all seven conventions.

- ISDA, Historical, Actual365, AFB and Euro: the name, and the day count and year fraction from every second of 233
  start dates (every 61st day of 2000-2030, plus month ends, leap days and mid-months of 2000, 2023-2025 and 2100) to
  13 end dates 0 to 1826 days later; ISDA and AFB again from every fourth start date to five earlier end dates.
- ISMA and Bond without a schedule or reference dates: the name, and the day count and year fraction from every fourth
  start date to the same 13 end dates, so periods under 16 days take the one-year reference.
- ISMA and Bond without a schedule: the name and the year fraction from every second start date to up to 8 end dates
  inside a 6-month and a 12-month reference period starting on it; and from every fourth start date with a 6-month
  reference period around it, or starting 3 months after it, to end dates before, inside and after it (some of the
  latter throw).
- ISMA and Bond with a schedule: the name and the year fraction for 120 seeded pairs of dates inside each of five
  unadjusted schedules on a null calendar: 6-, 12- and 3-month regular ones, ordered pairs; a 6-month one with a
  long first and a short last period, pairs in both orders; and a weekly one, whose periods are under 16 days.

The schedules are built with ql.Schedule, the wheel's proxy of C++ QuantLib::Schedule's rule-based constructor,
called directly. Read back by tests/NanoQL.Tests/Vectors/Time/DayCounters/ActualActualVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

OFFSETS = [0, 1, 15, 29, 30, 31, 59, 90, 182, 365, 366, 731, 1826]
REFERENCE_OFFSETS = [1, 15, 30, 59, 90, 120, 150, 181]
EARLIER_OFFSETS = [1, 59, 366, 731, 1826]
AROUND_OFFSETS = [-1, 1, 45, 91, 200, 400, 731, 1826]
AFTER_OFFSETS = [1, 45, 120, 200, 274, 400]

# (tenor length, tenor unit, effective date, termination date, first date, next-to-last date, pairs in both orders)
SCHEDULES = [
    (6, ql.Months, ql.Date(15, ql.February, 2020), ql.Date(15, ql.August, 2030), None, None, False),
    (12, ql.Months, ql.Date(31, ql.January, 2021), ql.Date(31, ql.July, 2031), None, None, False),
    (3, ql.Months, ql.Date(30, ql.November, 2022), ql.Date(30, ql.May, 2027), None, None, False),
    (6, ql.Months, ql.Date(10, ql.January, 2017), ql.Date(15, ql.January, 2026), ql.Date(31, ql.August, 2017),
     ql.Date(31, ql.August, 2025), True),
    (1, ql.Weeks, ql.Date(4, ql.January, 2027), ql.Date(29, ql.March, 2027), None, None, False),
]
UNIT_LETTERS = {ql.Weeks: "W", ql.Months: "M"}


def first_dates():
    ds = set()
    d = ql.Date(1, ql.January, 2000)
    while d <= ql.Date(31, ql.December, 2030):
        ds.add(d.serialNumber())
        d = d + 61
    for y in (2000, 2023, 2024, 2025, 2100):
        for (dd, m) in ((28, ql.February), (31, ql.January), (30, ql.April), (31, ql.March), (31, ql.August),
                        (30, ql.November), (31, ql.December), (30, ql.January), (15, ql.June)):
            ds.add(ql.Date(dd, m, y).serialNumber())
        if ql.Date.isLeap(y):
            ds.add(ql.Date(29, ql.February, y).serialNumber())
    return sorted(ds)


def dc_cases(name, st, step=2):
    convention = getattr(ql.ActualActual, name)
    dc = ql.ActualActual(convention)
    out = []
    for s in first_dates()[::step]:
        d1 = ql.Date(s)
        d2s = [d1 + k for k in OFFSETS]
        out.append({"name": f"ActualActual.{name} {s}",
                    "inputs": {"settings": st, "convention": int(convention), "d1": v.enc(d1), "d2": v.enc(d2s)},
                    "expected": {"name": v.attempt(dc.name),
                                 "dayCount": [v.attempt(lambda x=x: dc.dayCount(d1, x)) for x in d2s],
                                 "yearFraction": [v.attempt(lambda x=x: dc.yearFraction(d1, x)) for x in d2s]}})
    return out


def earlier_cases(name, st):
    convention = getattr(ql.ActualActual, name)
    dc = ql.ActualActual(convention)
    out = []
    for s in first_dates()[::4]:
        d1 = ql.Date(s)
        d2s = [d1 - k for k in EARLIER_OFFSETS]
        out.append({"name": f"ActualActual.{name} earlier {s}",
                    "inputs": {"settings": st, "convention": int(convention), "d1": v.enc(d1), "d2": v.enc(d2s)},
                    "expected": {"name": v.attempt(dc.name),
                                 "yearFraction": [v.attempt(lambda x=x: dc.yearFraction(d1, x)) for x in d2s]}})
    return out


def reference_case(label, convention, st, d1, d2s, ref_start, ref_end):
    dc = ql.ActualActual(convention)
    return {"name": label,
            "inputs": {"settings": st, "convention": int(convention), "d1": v.enc(d1), "d2": v.enc(d2s),
                       "refPeriodStart": v.enc(ref_start), "refPeriodEnd": v.enc(ref_end)},
            "expected": {"name": v.attempt(dc.name),
                         "yearFraction": [v.attempt(lambda x=x: dc.yearFraction(d1, x, ref_start, ref_end))
                                          for x in d2s]}}


def ref_cases(name, st, months):
    """End dates inside a reference period of `months` months starting at the start date."""
    convention = getattr(ql.ActualActual, name)
    out = []
    for s in first_dates()[::2]:
        d1 = ql.Date(s)
        ref_end = d1 + ql.Period(months, ql.Months)
        d2s = [d1 + k for k in REFERENCE_OFFSETS if d1 + k <= ref_end]
        out.append(reference_case(f"ActualActual.{name} ref{months}M {s}", convention, st, d1, d2s, d1, ref_end))
    return out


def shifted_ref_cases(name, st):
    """A 6-month reference period around the start date, or starting 3 months after it."""
    convention = getattr(ql.ActualActual, name)
    out = []
    for s in first_dates()[::4]:
        d1 = ql.Date(s)
        start, end = d1 - ql.Period(3, ql.Months), d1 + ql.Period(3, ql.Months)
        out.append(reference_case(f"ActualActual.{name} ref around {s}", convention, st, d1,
                                  [d1 + k for k in AROUND_OFFSETS], start, end))
        start, end = d1 + ql.Period(3, ql.Months), d1 + ql.Period(9, ql.Months)
        out.append(reference_case(f"ActualActual.{name} ref after {s}", convention, st, d1,
                                  [d1 + k for k in AFTER_OFFSETS], start, end))
    return out


def schedule_cases(st):
    rnd = random.Random(v.SEED)
    out = []
    for (n, unit, eff, mat, first, next_to_last, both_orders) in SCHEDULES:
        sch = ql.Schedule(eff, mat, ql.Period(n, unit), ql.NullCalendar(), ql.Unadjusted, ql.Unadjusted,
                          ql.DateGeneration.Backward, False, first if first is not None else ql.Date(),
                          next_to_last if next_to_last is not None else ql.Date())
        pairs = []
        for _ in range(120):
            a = eff + rnd.randrange(0, mat - eff)
            b = min(a + rnd.randrange(0, 900), mat)
            pairs.append((b, a) if both_orders and rnd.random() < 0.5 else (a, b))
        spec = {"effectiveDate": v.enc(eff), "terminationDate": v.enc(mat), "tenor": [n, int(unit)],
                "calendar": "NullCalendar", "convention": int(ql.Unadjusted),
                "terminationDateConvention": int(ql.Unadjusted), "rule": int(ql.DateGeneration.Backward),
                "endOfMonth": False, "firstDate": v.enc(first), "nextToLastDate": v.enc(next_to_last)}
        for name in ["ISMA", "Bond"]:
            convention = getattr(ql.ActualActual, name)
            dc = ql.ActualActual(convention, sch)
            irregular = " irregular" if first is not None else ""
            out.append({"name": f"ActualActual.{name} schedule {n}{UNIT_LETTERS[unit]}{irregular}",
                        "inputs": {"settings": st, "convention": int(convention), "schedule": spec,
                                   "d1": v.enc([p[0] for p in pairs]), "d2": v.enc([p[1] for p in pairs])},
                        "expected": {"name": v.attempt(dc.name),
                                     "yearFraction": [v.attempt(lambda p=p: dc.yearFraction(p[0], p[1]))
                                                      for p in pairs]}})
    return out


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for name in ["ISDA", "Historical", "Actual365", "AFB", "Euro"]:
        out += dc_cases(name, st)
    for name in ["ISMA", "Bond"]:
        out += dc_cases(name, st, step=4)
    for name in ["ISMA", "Bond"]:
        out += ref_cases(name, st, 6)
        out += ref_cases(name, st, 12)
    out += schedule_cases(st)
    for name in ["ISDA", "AFB"]:
        out += earlier_cases(name, st)
    for name in ["ISMA", "Bond"]:
        out += shifted_ref_cases(name, st)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/daycounters/actualactual.hpp", cases)
