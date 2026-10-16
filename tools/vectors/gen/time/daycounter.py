# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Day-counter equality (ql/time/daycounter.hpp): 23 day counters, every convention of Actual/360, Actual/365 (Fixed),
30/360 and Actual/Actual, with Actual/Actual (ISMA) and (Bond) also given a schedule, compared pairwise with == and
!=, which QuantLib defines by name. Each case's inputs.dayCounter and inputs.others describe day counters as
{"type", "convention"} (Actual/360 as {"type", "includeLastDay"}), with "schedule": true for the 6-month unadjusted
schedule on a null calendar from 15 February 2020 to 15 August 2030. Read back by
tests/NanoQL.Tests/Vectors/Time/DayCounterVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def schedule():
    return ql.Schedule(ql.Date(15, ql.February, 2020), ql.Date(15, ql.August, 2030), ql.Period(6, ql.Months),
                       ql.NullCalendar(), ql.Unadjusted, ql.Unadjusted, ql.DateGeneration.Backward, False)


def specs():
    out = [{"type": "Actual360", "includeLastDay": False}, {"type": "Actual360", "includeLastDay": True}]
    out += [{"type": "Actual365Fixed", "convention": c} for c in range(3)]
    out += [{"type": "Thirty360", "convention": c} for c in range(9)]
    out += [{"type": "ActualActual", "convention": c} for c in range(7)]
    out += [{"type": "ActualActual", "convention": c, "schedule": True} for c in (0, 1)]
    return out


def build(spec):
    if spec["type"] == "Actual360":
        return ql.Actual360(spec["includeLastDay"])
    if spec["type"] == "Actual365Fixed":
        return ql.Actual365Fixed(spec["convention"])
    if spec["type"] == "Thirty360":
        return ql.Thirty360(spec["convention"])
    if spec.get("schedule"):
        return ql.ActualActual(spec["convention"], schedule())
    return ql.ActualActual(spec["convention"])


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    all_specs = specs()
    out = []
    for i, spec in enumerate(all_specs):
        dc = build(spec)
        others = [build(o) for o in all_specs]
        out.append({"name": f"{i} {dc.name()}" + (" with schedule" if spec.get("schedule") else ""),
                    "inputs": {"settings": st, "dayCounter": spec, "others": all_specs},
                    "expected": {"name": dc.name(),
                                 "equal": [dc == o for o in others],
                                 "notEqual": [dc != o for o in others]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/daycounter.hpp", cases)
