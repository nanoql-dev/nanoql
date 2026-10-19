# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ConstantCapFloorTermVolatility (ql/termstructures/volatility/capfloor/constantcapfloortermvol.hpp),
which the wheel doesn't bind, each output computed by the C++ harness: 6 structures on the evaluation date 15 January
2025, on a fixed reference date and on 0 to 3 settlement days, the TARGET and UK calendars, three conventions and two
day counters.

Each structure's outputs are its reference date, maximum date, minimum and maximum strikes, business-day convention,
settlement days (QuantLib's refusal for a fixed reference date, written as null, nanoql's answer), and its volatility by
tenor, by date and by time at 6 points each, inside and outside its range, with and without the extrapolation flag (a
refused value {"throws": true}: only a date before the reference date or a negative time is outside it). A structure is
{"referenceDate" or "settlementDays", "calendar", "convention", "volatility", "dayCounter"}, as
tools/cxx/src/termstructures/volatility/capfloor/constantcapfloortermvol.cpp reads it. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/ConstantCapFloorTermVolatilityCxxVectorTests.cs."""
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
TARGET = {"type": "TARGET"}
UK = {"type": "UnitedKingdom", "market": int(ql.UnitedKingdom.Exchange)}
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
D, M, Y = int(ql.Days), int(ql.Months), int(ql.Years)


def structure(volatility, reference=None, days=None, cal=TARGET, convention=ql.Following, dc=A365):
    out = {"referenceDate": v.enc(reference)} if reference is not None else {"settlementDays": days}
    out.update({"calendar": cal, "convention": int(convention), "volatility": volatility, "dayCounter": dc})
    return out


STRUCTURES = [
    ("fixed", structure(0.2, reference=ql.Date(17, 1, 2025))),
    ("no settlement days", structure(0.15, days=0)),
    ("two settlement days", structure(0.25, days=2)),
    ("UK, three settlement days, modified following", structure(0.18, days=3, cal=UK, convention=ql.ModifiedFollowing)),
    ("unadjusted, Actual/360", structure(0.3, days=1, convention=ql.Unadjusted, dc=A360)),
    ("zero volatility", structure(0.0, days=2)),
]


def case(H, name, spec):
    with H.case_settings(v.enc(TODAY)) as st:
        def call(method, *args):
            return v.attempt(lambda: H.call(f"ConstantCapFloorTermVolatility::{method}", spec, *args))

        ref = H.call("ConstantCapFloorTermVolatility::referenceDate", spec)
        by_tenor = [[[1, D], 0.03, False], [[6, M], -0.01, False], [[5, Y], 0.03, False], [[50, Y], 0.5, False],
                    [[18, M], 0.0, True], [[0, D], 0.03, False]]
        by_date = [[ref, 0.03, False], [ref + 100, 0.03, False], [ref + 10000, 0.1, False], [ref - 1, 0.03, False],
                   [ref - 1, 0.03, True], [ref + 1, -0.05, True]]
        by_time = [[0.0, 0.03, False], [0.5, 0.03, False], [30.0, 0.2, False], [-0.1, 0.03, False],
                   [-0.1, 0.03, True], [2.5, -1.0, False]]
        expected = {k: call(k) for k in ("referenceDate", "maxDate", "minStrike", "maxStrike", "businessDayConvention")}
        expected["settlementDays"] = v.result(lambda: H.call("ConstantCapFloorTermVolatility::settlementDays", spec))
        expected["volatilityByTenor"] = [call("volatility(Period)", *p) for p in by_tenor]
        expected["volatilityByDate"] = [call("volatility(Date)", *p) for p in by_date]
        expected["volatilityByTime"] = [call("volatility(Time)", *p) for p in by_time]
        return {"name": name, "inputs": {"settings": st, "structure": spec, "byTenor": by_tenor, "byDate": by_date,
                                         "byTime": by_time},
                "expected": expected}


def cases():
    with cxx.connect() as H:
        return [case(H, name, spec) for name, spec in STRUCTURES]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/capfloor/constantcapfloortermvol.hpp", cases)
