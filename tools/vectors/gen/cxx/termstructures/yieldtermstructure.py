# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's YieldTermStructure jumps (ql/termstructures/yieldtermstructure.hpp, .cpp), each output
computed by the C++ harness on QuantLib's InterpolatedDiscountCurve<LogLinear>: jumpDates() and jumpTimes(), which the
wheel doesn't bind, and discount(Date, bool), which applies the jumps.

- 10 jump sets on a log-linear discount curve from 15 January 2024 to 10 years (6 nodes, Actual/365 (Fixed)): one jump
  on a given date; two on given dates; three on QuantLib's default dates, 31 December of the reference date's year
  and of the years after it; one jump on the default date; a jump before the reference date and one on it, whose
  times QuantLib's discount skips (it applies a jump of positive time before the time it reads); a jump past the last
  node and one past every date read; jumps above 1; unsorted dates; and a jump on a date the discount is read at,
  which applies only after it.
- On a 30/360 curve from 31 January 2024 (6 nodes): two jumps on given dates, the end of a month among them.
- QuantLib's refusals: jumps and jump dates in different numbers, refused by the constructor ("mismatch between number
  of jumps (n) and jump dates (m)"); and a zero jump and a negative one, which QuantLib builds and whose discounts past
  them it refuses ("invalid 1st jump value: …").

Each case's outputs are whether its curve builds (QuantLib's refusal recorded as its throw), then its jump dates and
times and its discount at 20 dates, among them the jump dates, the days after them and dates past the last node,
extrapolated, each discount refused recorded as its throw. Each case's inputs hold its settings, "curve" ({"dates",
"discounts", "dayCounter", "jumps", "jumpDates"}, null jumps for none and null jump dates for QuantLib's default),
"discountDates" and "extrapolate". Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/YieldTermStructureCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

D = ql.Date
A365 = {"type": "Actual365Fixed"}
THIRTY360_BB = {"type": "Thirty360", "convention": int(ql.Thirty360.BondBasis)}


def nodes(reference, offsets, rate, dc):
    dates = [reference + n for n in offsets]
    return v.enc(dates), [1.0] + [math.exp(-rate * dc.yearFraction(reference, d)) for d in dates[1:]]


R1 = D(15, 1, 2024)
DATES1, DFS1 = nodes(R1, [0, 182, 365, 1096, 1826, 3653], 0.031, ql.Actual365Fixed())
R2 = D(31, 1, 2024)
DATES2, DFS2 = nodes(R2, [0, 91, 366, 731, 1827, 3653], 0.027, ql.Thirty360(ql.Thirty360.BondBasis))

# 20 dates each: the reference date, jump dates and the days after them, and 2 past the last node.
QUERIES1 = [R1, R1 + 1, D(30, 6, 2024), D(1, 7, 2024), D(31, 12, 2024), D(1, 1, 2025), D(15, 6, 2025),
            D(31, 12, 2025), D(1, 1, 2026), D(31, 12, 2026), D(1, 1, 2027), D(30, 6, 2028), D(31, 12, 2029),
            D(1, 1, 2030), D(15, 1, 2031), D(31, 12, 2032), D(1, 1, 2033), D(15, 1, 2034), D(15, 1, 2035),
            D(31, 12, 2040)]
QUERIES2 = [R2, R2 + 1, D(29, 2, 2024), D(1, 3, 2024), D(31, 5, 2024), D(30, 9, 2024), D(31, 12, 2024),
            D(1, 1, 2025), D(31, 1, 2025), D(28, 2, 2025), D(31, 8, 2025), D(31, 1, 2026), D(30, 6, 2027),
            D(31, 1, 2028), D(31, 12, 2028), D(31, 1, 2029), D(31, 7, 2031), D(31, 1, 2034), D(28, 2, 2034),
            D(31, 1, 2036)]


def curve(dates, dfs, dc, jumps, jump_dates):
    return {"dates": dates, "discounts": dfs, "dayCounter": dc, "jumps": jumps, "jumpDates": v.enc(jump_dates)}


def jump_sets():
    """(name, curve spec, discount dates) for the 13 cases."""
    def on1(jumps, jump_dates):
        return curve(DATES1, DFS1, A365, jumps, jump_dates)

    return [
        ("one jump on a given date", on1([0.999], [D(31, 12, 2024)]), QUERIES1),
        ("two jumps on given dates", on1([0.999, 0.9985], [D(30, 6, 2024), D(31, 12, 2025)]), QUERIES1),
        ("three jumps on default dates", on1([0.9995, 0.999, 0.9985], None), QUERIES1),
        ("one jump on the default date", on1([0.998], None), QUERIES1),
        ("jumps before and on the reference date", on1([0.99, 0.995, 0.999], [D(10, 1, 2024), R1, D(31, 12, 2026)]),
         QUERIES1),
        ("jumps past the last node and past every date read", on1([0.997, 0.996], [D(30, 6, 2034), D(1, 1, 2050)]),
         QUERIES1),
        ("jumps above 1", on1([1.002, 1.0005], [D(31, 12, 2024), D(31, 12, 2029)]), QUERIES1),
        ("unsorted jump dates", on1([0.999, 0.998, 0.997], [D(31, 12, 2032), D(30, 6, 2024), D(31, 12, 2026)]),
         QUERIES1),
        ("a jump on a date read", on1([0.9975], [D(1, 1, 2026)]), QUERIES1),
        ("no jumps", on1(None, None), QUERIES1),
        ("two jumps on given dates, 30/360", curve(DATES2, DFS2, THIRTY360_BB, [0.9993, 0.9991],
                                                   [D(29, 2, 2024), D(31, 12, 2024)]), QUERIES2),
        ("two jumps, one jump date", on1([0.999, 0.998], [D(31, 12, 2024)]), QUERIES1),
        ("a zero jump and a negative one", on1([0.0, -0.5], [D(31, 12, 2024), D(31, 12, 2029)]), QUERIES1),
    ]


def cases():
    out = []
    with cxx.connect() as H:
        for name, spec, queries in jump_sets():
            when = v.enc(queries)
            last = ql.Date(spec["dates"][-1])
            flags = [q > last for q in queries]
            with H.case_settings(spec["dates"][0]) as st:
                try:
                    jump_dates = H.call("YieldTermStructure::jumpDates", spec)
                except RuntimeError:
                    expected = {"built": dict(v.THROWS)}
                else:
                    expected = {"built": True, "jumpDates": jump_dates,
                                "jumpTimes": H.call("YieldTermStructure::jumpTimes", spec),
                                "discounts": [v.attempt(lambda d=d, x=x: H.call(
                                    "YieldTermStructure::discount(Date,bool)", spec, d, x))
                                    for d, x in zip(when, flags)]}
            out.append({"name": name, "inputs": {"settings": st, "curve": spec, "discountDates": when,
                                                 "extrapolate": flags},
                        "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    assert by_name["two jumps, one jump date"] == {"built": v.THROWS}
    assert by_name["three jumps on default dates"]["jumpDates"] == v.enc([D(31, 12, 2024), D(31, 12, 2025),
                                                                          D(31, 12, 2026)])
    refused = by_name["a zero jump and a negative one"]["discounts"]
    assert refused[4] != v.THROWS and refused[5] == v.THROWS


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yieldtermstructure.hpp", cases)
