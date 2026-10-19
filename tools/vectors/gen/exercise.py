# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the exercises of ql/exercise.hpp, as the wheel builds them: EuropeanExercise(date),
AmericanExercise(earliestDate, latestDate, payoffAtExpiry), BermudanExercise(dates, payoffAtExpiry) and the bare
Exercise(type), which holds no dates.

- "exercise" cases: 31 exercises, European on 8 dates (Date::minDate and maxDate, a leap day and a weekend among
  them), American on 10 pairs (equal dates, the date range's ends) and Bermudan on 10 lists (unsorted, with
  duplicates, a single date), each with and without the payoff at expiry, and a bare exercise of each type; for each,
  its type, dates and last date, and each date by date(i) and dateAt(i). The bare exercises' last date is refused
  ("no exercise date given"). An index past the dates isn't read: date(i) reads past the vector, undefined, and
  dateAt(i) throws std::out_of_range, which the wheel raises as IndexError.
- "refused" cases: the constructors' refusals, an American exercise whose earliest date is after its latest, and a
  Bermudan exercise with no dates.

Each case's input "exercise" is the spec tools/cxx/src/exercise.cpp reads: {"type", ...} with the constructor's
arguments under QuantLib's names, dates as serials. A refused value is {"throws": true}. Read back by
tests/NanoQL.Tests/Vectors/ExerciseVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

MIN, MAX = ql.Date.minDate(), ql.Date.maxDate()
EUROPEAN = [ql.Date(15, ql.October, 2026), ql.Date(16, ql.June, 2025), ql.Date(29, ql.February, 2028),
            ql.Date(17, ql.October, 2026), ql.Date(31, ql.December, 2199), ql.Date(1, ql.January, 1901), MIN, MAX]
AMERICAN = [
    (ql.Date(15, ql.October, 2026), ql.Date(15, ql.October, 2027), False),
    (ql.Date(15, ql.October, 2026), ql.Date(15, ql.October, 2027), True),
    (ql.Date(15, ql.October, 2026), ql.Date(15, ql.October, 2026), False),
    (ql.Date(29, ql.February, 2028), ql.Date(28, ql.February, 2029), True),
    (MIN, ql.Date(15, ql.October, 2026), False),
    (MIN, MAX, True),
    (ql.Date(15, ql.October, 2026), MAX, False),
    (ql.Date(1, ql.January, 2030), ql.Date(2, ql.January, 2030), True),
    (ql.Date(17, ql.October, 2026), ql.Date(18, ql.October, 2026), False),
    (MAX, MAX, True)]
BERMUDAN = [
    ([ql.Date(15, ql.October, 2026)], False),
    ([ql.Date(15, ql.October, 2026)], True),
    ([ql.Date(15, ql.October, 2026), ql.Date(15, ql.April, 2027), ql.Date(15, ql.October, 2027)], False),
    ([ql.Date(15, ql.October, 2027), ql.Date(15, ql.October, 2026), ql.Date(15, ql.April, 2027)], True),
    ([ql.Date(15, ql.April, 2027), ql.Date(15, ql.April, 2027), ql.Date(15, ql.October, 2026)], False),
    ([MAX, MIN, ql.Date(29, ql.February, 2028)], True),
    ([ql.Date(16, ql.June, 2025) + 30 * i for i in range(12)], False),
    ([ql.Date(16, ql.June, 2025) + 30 * (11 - i) for i in range(12)], True),
    ([ql.Date(31, ql.December, 2026), ql.Date(31, ql.December, 2026)], False),
    ([ql.Date(1, ql.March, 2030), ql.Date(28, ql.February, 2030), ql.Date(29, ql.February, 2032),
      ql.Date(1, ql.January, 2030)], True)]
TYPES = [("American", ql.Exercise.American), ("Bermudan", ql.Exercise.Bermudan), ("European", ql.Exercise.European)]


def described(st, name, spec, e):
    """An exercise's type, dates, last date, and each date by date(i) and dateAt(i)."""
    n = len(e.dates())
    return {"name": name, "inputs": {"settings": st, "kind": "exercise", "exercise": spec},
            "expected": {"type": int(e.type()), "dates": v.enc(list(e.dates())), "lastDate": v.attempt(e.lastDate),
                         "date": [v.enc(e.date(i)) for i in range(n)], "dateAt": [v.enc(e.dateAt(i)) for i in range(n)]}}


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for d in EUROPEAN:
        out.append(described(st, f"European {d.ISO()}", {"type": "EuropeanExercise", "date": v.enc(d)},
                             ql.EuropeanExercise(d)))
    for earliest, latest, at_expiry in AMERICAN:
        spec = {"type": "AmericanExercise", "earliestDate": v.enc(earliest), "latestDate": v.enc(latest),
                "payoffAtExpiry": at_expiry}
        out.append(described(st, f"American {earliest.ISO()} {latest.ISO()} {at_expiry}", spec,
                             ql.AmericanExercise(earliest, latest, at_expiry)))
    for k, (dates, at_expiry) in enumerate(BERMUDAN):
        spec = {"type": "BermudanExercise", "dates": v.enc(dates), "payoffAtExpiry": at_expiry}
        out.append(described(st, f"Bermudan {k} ({len(dates)} dates) {at_expiry}", spec,
                             ql.BermudanExercise(dates, at_expiry)))
    for tn, t in TYPES:
        out.append(described(st, f"bare {tn}", {"type": "Exercise", "exerciseType": int(t)}, ql.Exercise(t)))
    refused = [
        ("American after its latest date", {"type": "AmericanExercise", "earliestDate": v.enc(EUROPEAN[0] + 1),
                                            "latestDate": v.enc(EUROPEAN[0]), "payoffAtExpiry": False},
         lambda: ql.AmericanExercise(EUROPEAN[0] + 1, EUROPEAN[0], False)),
        ("American after its latest date, at expiry", {"type": "AmericanExercise", "earliestDate": v.enc(MAX),
                                                       "latestDate": v.enc(MIN), "payoffAtExpiry": True},
         lambda: ql.AmericanExercise(MAX, MIN, True)),
        ("Bermudan without dates", {"type": "BermudanExercise", "dates": [], "payoffAtExpiry": False},
         lambda: ql.BermudanExercise([], False)),
        ("Bermudan without dates, at expiry", {"type": "BermudanExercise", "dates": [], "payoffAtExpiry": True},
         lambda: ql.BermudanExercise([], True))]
    for name, spec, build in refused:
        out.append({"name": f"refused: {name}", "inputs": {"settings": st, "kind": "refused", "exercise": spec},
                    "expected": {"exercise": v.attempt(lambda b=build: b() and None)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/exercise.hpp", cases)
