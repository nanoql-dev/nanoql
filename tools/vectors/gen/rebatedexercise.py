# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's RebatedExercise (ql/rebatedexercise.hpp, .cpp), each output computed by the wheel on the
evaluation date 15 January 2025: the Bermudan exercises with a rebate per date that the wheel builds (its constructor
from a rebate vector, the only one it binds), their type, dates and last date; and the constructor's refusals, a rebate
vector on a European exercise and rebates not one per date, as {"throws": true}. The rebates and their payment dates are
the harness's (cxx/pricingengines/swaption/basketgeneratingengine.py), the wheel binding neither. Each case's input
"exercise" is that generator's spec; tests/NanoQL.Tests/Vectors/RebatedExerciseVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.January, 2025)
CAL = ql.TARGET()


def calendar(spec):
    return ql.TARGET() if spec["type"] == "TARGET" else ql.NullCalendar()


def make_exercise(spec):
    if spec["type"] == "EuropeanExercise":
        return ql.EuropeanExercise(ql.Date(spec["date"]))
    return ql.BermudanExercise([ql.Date(d) for d in spec["dates"]], spec["payoffAtExpiry"])


def exercises():
    first = CAL.advance(TODAY, 1, ql.Years)
    dates = [v.enc(CAL.advance(first, i, ql.Years)) for i in range(4)]
    bermudan = {"type": "BermudanExercise", "dates": dates, "payoffAtExpiry": False}

    def spec(exercise, rebates, days=2, cal=None, convention=ql.Following):
        return {"type": "RebatedExercise", "exercise": exercise, "rebates": rebates, "rebateSettlementDays": days,
                "rebatePaymentCalendar": cal or {"type": "TARGET"}, "rebatePaymentConvention": int(convention)}

    return [
        ("Bermudan, a rebate per date", spec(bermudan, [1000.0, -500.0, 2500.0, 0.0])),
        ("Bermudan, two dates, the null calendar", spec(
            {"type": "BermudanExercise", "dates": dates[:2], "payoffAtExpiry": True}, [10.0, 20.0], 0,
            {"type": "NullCalendar"}, ql.ModifiedFollowing)),
        ("refused: a rebate vector on a European exercise",
         spec({"type": "EuropeanExercise", "date": dates[0]}, [1.0])),
        ("refused: rebates not one per date", spec(bermudan, [1.0, 2.0])),
    ]


def case(name, spec):
    with v.case_settings(TODAY) as st:
        def build():
            return ql.RebatedExercise(make_exercise(spec["exercise"]), spec["rebates"], spec["rebateSettlementDays"],
                                      calendar(spec["rebatePaymentCalendar"]), spec["rebatePaymentConvention"])
        try:
            e = build()
        except RuntimeError:
            return {"name": name, "inputs": {"settings": st, "exercise": spec}, "expected": {"construct": dict(v.THROWS)}}
        expected = {"type": int(e.type()), "dates": [d.serialNumber() for d in e.dates()],
                    "lastDate": e.lastDate().serialNumber()}
        return {"name": name, "inputs": {"settings": st, "exercise": spec}, "expected": expected}


def cases():
    return [case(name, spec) for name, spec in exercises()]


if __name__ == "__main__":
    v.main(__file__, "ql/rebatedexercise.hpp", cases)
