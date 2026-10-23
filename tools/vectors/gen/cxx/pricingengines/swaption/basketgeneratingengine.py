# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's RebatedExercise (ql/rebatedexercise.hpp, .cpp) that the wheel doesn't bind, the
exercise the basket-generating engines read (ql/pricingengines/swaption/basketgeneratingengine.cpp): its rebates,
rebate(index) and rebatePaymentDate(index), each output computed by the C++ harness on an exercise built inside the
call, on the evaluation date 15 January 2025. The exercises: a European exercise with one rebate paid two TARGET days
later; a Bermudan exercise with a rebate per date, paid two TARGET days later modified following; a Bermudan exercise
with the defaults (one rebate of 0, paid on the date on the null calendar); a Bermudan exercise on a Friday paid three
days later; and an American exercise. Each index from 0 to the last and one past, and QuantLib's refusals as {"throws":
true}: the American exercise's payment date, a rebate vector on a European exercise, and rebates not one per date. Left
out: the payment date of an index past the dates, which QuantLib reads unchecked (nanoql refuses it). Each case's input
"exercise" is the spec tools/cxx/src/pricingengines/swaption/basketgeneratingengine.cpp reads;
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/RebatedExerciseCxxVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.January, 2025)
CAL = ql.TARGET()
TARGET = {"type": "TARGET"}
NULL_CALENDAR = {"type": "NullCalendar"}


def annual(first, count):
    return [v.enc(CAL.advance(first, i, ql.Years)) for i in range(count)]


def rebated(exercise, days, calendar, convention, rebate=None, rebates=None):
    spec = {"type": "RebatedExercise", "exercise": exercise, "rebateSettlementDays": days,
            "rebatePaymentCalendar": calendar, "rebatePaymentConvention": int(convention)}
    if rebates is not None:
        spec["rebates"] = list(rebates)
    else:
        spec["rebate"] = rebate
    return spec


def exercises():
    first = CAL.advance(TODAY, 1, ql.Years)
    bermudan = {"type": "BermudanExercise", "dates": annual(first, 4), "payoffAtExpiry": False}
    friday = ql.Date(13, ql.March, 2026)
    return [
        ("European, one rebate", rebated({"type": "EuropeanExercise", "date": v.enc(first)}, 2, TARGET, ql.Following,
                                         rebate=1000.0)),
        ("Bermudan, a rebate per date", rebated(bermudan, 2, TARGET, ql.ModifiedFollowing,
                                                rebates=[1000.0, -500.0, 2500.0, 0.0])),
        ("Bermudan, the defaults", rebated(bermudan, 0, NULL_CALENDAR, ql.Following, rebate=0.0)),
        ("Bermudan on a Friday, three days", rebated(
            {"type": "BermudanExercise", "dates": [v.enc(friday), v.enc(friday + 7)], "payoffAtExpiry": False}, 3,
            TARGET, ql.Following, rebates=[10.0, 20.0])),
        ("American, one rebate", rebated({"type": "AmericanExercise", "earliestDate": v.enc(TODAY),
                                          "latestDate": v.enc(first), "payoffAtExpiry": False}, 2, TARGET,
                                         ql.Following, rebate=5.0)),
        ("refused: a rebate vector on a European exercise", rebated(
            {"type": "EuropeanExercise", "date": v.enc(first)}, 2, TARGET, ql.Following, rebates=[1.0])),
        ("refused: rebates not one per date", rebated(bermudan, 2, TARGET, ql.Following, rebates=[1.0, 2.0])),
    ]


def case(H, name, spec):
    with H.case_settings(v.enc(TODAY)) as st:
        rebates = v.attempt(lambda: H.call("RebatedExercise::rebates", spec))
        n = len(rebates) if isinstance(rebates, list) else 1
        expected = {
            "rebates": rebates,
            "rebate": [v.attempt(lambda i=i: H.call("RebatedExercise::rebate", spec, i)) for i in range(n + 1)],
            "rebatePaymentDate": [v.attempt(lambda i=i: H.call("RebatedExercise::rebatePaymentDate", spec, i))
                                  for i in range(n)],
        }
        return {"name": name, "inputs": {"settings": st, "exercise": spec}, "expected": expected}


def cases():
    with cxx.connect() as H:
        return [case(H, name, spec) for name, spec in exercises()]


if __name__ == "__main__":
    v.main(__file__, "ql/rebatedexercise.hpp", cases)
