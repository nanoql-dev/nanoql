# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of ql/exercise.hpp the wheel doesn't expose, each output computed by the C++ harness:
EarlyExercise::payoffAtExpiry, which the wheel's American and Bermudan exercises take but don't give back, and
AmericanExercise(latestDate, payoffAtExpiry), which the wheel doesn't wrap and whose earliest date is
Date::minDate().

- "early" cases: American exercises between two dates, from the latest date only, Bermudan exercises and bare
  EarlyExercises of each type, each with and without the payoff at expiry: payoffAtExpiry, type, dates, lastDate and
  each date by dateAt(i). A bare EarlyExercise holds no dates, so its last date is refused ("no exercise date given").

Each case's input "exercise" is the spec tools/cxx/src/exercise.cpp reads. A refused value is {"throws": true}.
tests/NanoQL.Tests/Vectors/Cxx/ExerciseCxxVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

AMERICAN, BERMUDAN, EUROPEAN = 0, 1, 2
# Date::minDate() and Date::maxDate(), 1 January 1901 and 31 December 2199.
MIN_DATE, MAX_DATE = 367, 109574


def specs(H):
    """The exercises, each with a name; dates as serials."""
    def serial(d, m, y):
        return H.call("Date(Day,Month,Year)", d, m, y)

    today, year = serial(15, 10, 2026), serial(15, 10, 2027)
    first, last = MIN_DATE, MAX_DATE
    out = []
    for at_expiry in (False, True):
        for earliest, latest in ((today, year), (today, today), (first, last)):
            out.append((f"American {earliest} {latest} {at_expiry}",
                        {"type": "AmericanExercise", "earliestDate": earliest, "latestDate": latest,
                         "payoffAtExpiry": at_expiry}))
        for latest in (today, year, serial(29, 2, 2028), first, last):
            out.append((f"American up to {latest} {at_expiry}",
                        {"type": "AmericanExercise", "latestDate": latest, "payoffAtExpiry": at_expiry}))
        for dates in ([today], [year, today, serial(15, 4, 2027)], [last, first, last]):
            out.append((f"Bermudan {dates} {at_expiry}",
                        {"type": "BermudanExercise", "dates": dates, "payoffAtExpiry": at_expiry}))
        for t in (AMERICAN, BERMUDAN, EUROPEAN):
            out.append((f"EarlyExercise {t} {at_expiry}",
                        {"type": "EarlyExercise", "exerciseType": t, "payoffAtExpiry": at_expiry}))
    return out


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for name, spec in specs(H):
            dates = H.call("Exercise::dates", spec)
            out.append({"name": name, "inputs": {"settings": st, "kind": "early", "exercise": spec},
                        "expected": {
                            "payoffAtExpiry": H.call("EarlyExercise::payoffAtExpiry", spec),
                            "type": H.call("Exercise::type", spec),
                            "dates": dates,
                            "lastDate": v.attempt(lambda: H.call("Exercise::lastDate", spec)),
                            "dateAt": [H.call("Exercise::dateAt", spec, i) for i in range(len(dates))]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/exercise.hpp", cases)
