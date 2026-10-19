# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BMAIndex (ql/indexes/bmaindex.hpp), 56 cases on the evaluation date Wednesday 15 January
2025.

- 1 case of the index's conventions: its name, family name, tenor ([length, units]), fixing days, currency, day
  counter's and fixing calendar's names, whether its forwarding curve is empty, and isValidFixingDate for every date
  of 2023 to 2025 (a Wednesday that is a business day, or the first business day after a run of holidays starting on
  a Wednesday: Juneteenth 2024, Christmas Day 2024 and New Year's Day 2025 are Wednesday holidays).
- 50 cases of dates, each at a date of 2023 to 2025 (holidays and weekend days among them) and a span from it of 0 to
  60 days, or reversed by more than a week: fixingDate, valueDate (which QuantLib refuses for a date that isn't a valid
  fixing date), maturityDate, and fixingSchedule from the date to the span's end (refused when reversed).
- 3 cases of forecasts, on two flat curves and with none: fixing at 20 dates around the evaluation date, a refused
  value for a date that isn't a valid fixing date, a past date with no stored fixing, and every date with no curve.
- 2 cases replacing QuantLib's testFixingObservability (test-suite/indexes.cpp), whose point is that a fixing added
  through one BMA index is seen by another: a past fixing read through a second index before it is added (refused,
  missing), and after it is added through the first; a fixing on the evaluation date stored through the first and
  read through the second, and forecast with forecastTodaysFixing; and the refusal of a fixing on a date that isn't
  a valid fixing date.

A refused value is {"throws": true}. The fixings of the last cases are added in the case, and cleared after it: their
inputs hold them ("addedFixing", "todaysFixing", as [serial, value]) and the invalid date ("invalidDate"), not
inputs.settings. A curve is a flat forward spec of indexes/interestrateindex.py, null for QuantLib's empty handle.
Read back by tests/NanoQL.Tests/Vectors/Indexes/BMAIndexVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

TODAY = irx.TODAY
FIRST, LAST = ql.Date(1, 1, 2023), ql.Date(31, 12, 2025)
CURVES = [irx.curve_spec(TODAY, 0.0285), irx.curve_spec(TODAY, 0.034, irx.A360, ql.Simple)]
# Around the evaluation date: past Wednesdays (8 January, Christmas Day 2024, a holiday, and 26 December after it),
# the evaluation date, later Wednesdays and the days after them, and holidays (Inauguration and Washington's Birthday
# weeks, 4 July 2025 on a Friday, and Christmas Eve 2025 on a Wednesday).
FORECAST_DATES = [ql.Date(25, 12, 2024), ql.Date(26, 12, 2024), ql.Date(8, 1, 2025), TODAY, ql.Date(16, 1, 2025),
                  ql.Date(22, 1, 2025), ql.Date(23, 1, 2025), ql.Date(29, 1, 2025), ql.Date(19, 2, 2025),
                  ql.Date(16, 4, 2025), ql.Date(18, 4, 2025), ql.Date(2, 7, 2025), ql.Date(4, 7, 2025),
                  ql.Date(3, 9, 2025), ql.Date(26, 11, 2025), ql.Date(27, 11, 2025), ql.Date(24, 12, 2025),
                  ql.Date(31, 12, 2025), ql.Date(7, 1, 2026), ql.Date(14, 1, 2026)]


def bma(curve):
    return ql.BMAIndex(irx.handle(curve))


def empty(handle):
    try:
        handle.currentLink()
    except RuntimeError:
        return True
    return False


def conventions_case():
    with v.case_settings(TODAY) as st:
        idx = bma(CURVES[0])
        days = [FIRST + n for n in range(LAST - FIRST + 1)]
        return {"name": "conventions", "inputs": {"settings": st, "curve": CURVES[0], "from": v.enc(FIRST),
                                                  "to": v.enc(LAST)},
                "expected": {"name": idx.name(), "familyName": idx.familyName(),
                             "tenor": [idx.tenor().length(), int(idx.tenor().units())],
                             "fixingDays": idx.fixingDays(), "currency": idx.currency().code(),
                             "dayCounter": idx.dayCounter().name(), "fixingCalendar": idx.fixingCalendar().name(),
                             "forwardingTermStructureEmpty": empty(idx.forwardingTermStructure()),
                             "isValidFixingDate": [idx.isValidFixingDate(d) for d in days]}}


def date_cases():
    rnd = random.Random(v.SEED)
    holidays = [ql.Date(4, 7, 2023), ql.Date(25, 12, 2024), ql.Date(20, 1, 2025), ql.Date(18, 4, 2025),
                ql.Date(1, 1, 2024), ql.Date(27, 11, 2025), ql.Date(29, 3, 2024)]
    out = []
    for k in range(50):
        if k < len(holidays):
            d = holidays[k]
        else:
            d = FIRST + rnd.randrange(LAST - FIRST - 70)
        span = -rnd.randrange(8, 20) if k % 13 == 12 else rnd.randrange(0, 61)
        end = d + span
        with v.case_settings(TODAY) as st:
            idx = bma(None)
            out.append({"name": f"dates {k}: {d.ISO()} to {end.ISO()}",
                        "inputs": {"settings": st, "date": v.enc(d), "end": v.enc(end)},
                        "expected": {
                            "fixingDate": v.attempt(lambda: idx.fixingDate(d)),
                            "valueDate": v.attempt(lambda: idx.valueDate(d)),
                            "maturityDate": v.attempt(lambda: idx.maturityDate(d)),
                            "fixingSchedule": v.attempt(lambda: list(idx.fixingSchedule(d, end).dates()))}})
    return out


def forecast_cases():
    out = []
    for k, curve in enumerate(CURVES + [None]):
        with v.case_settings(TODAY) as st:
            idx = bma(curve)
            out.append({"name": f"forecast {k}", "inputs": {"settings": st, "curve": curve,
                                                             "dates": v.enc(FORECAST_DATES)},
                        "expected": {"fixing": [v.attempt(lambda d=d: idx.fixing(d)) for d in FORECAST_DATES]}})
    return out


def observability_cases():
    out = []
    for k, (curve, past) in enumerate([(CURVES[0], ql.Date(8, 1, 2025)), (CURVES[1], ql.Date(26, 12, 2024))]):
        added = (past, 0.0312 + 0.001 * k)
        todays = (TODAY, 0.0297 - 0.001 * k)
        invalid = ql.Date(9, 1, 2025)
        with v.case_settings(TODAY) as st:
            writer, reader = bma(None), bma(curve)
            before = v.attempt(lambda: reader.fixing(added[0]))
            writer.addFixing(*added)
            after = v.attempt(lambda: reader.fixing(added[0]))
            writer.addFixing(*todays)
            stored = v.attempt(lambda: reader.fixing(TODAY))
            forecast = v.attempt(lambda: reader.fixing(TODAY, True))
            refused = v.attempt(lambda: writer.addFixing(invalid, 0.03))
            out.append({"name": f"observability {k}",
                        "inputs": {"settings": st, "curve": curve, "addedFixing": [v.enc(added[0]), added[1]],
                                   "todaysFixing": [v.enc(todays[0]), todays[1]], "invalidDate": v.enc(invalid)},
                        "expected": {"fixingBefore": before, "fixingAfter": after, "todaysStoredFixing": stored,
                                     "todaysForecastFixing": forecast, "invalidFixing": refused}})
    return out


def cases():
    out = [conventions_case()] + date_cases() + forecast_cases() + observability_cases()
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    first = by_name["observability 0"]
    assert first["fixingBefore"] == v.THROWS and first["fixingAfter"] == 0.0312, first
    assert first["todaysStoredFixing"] == 0.0297 and first["todaysForecastFixing"] != 0.0297, first
    assert first["invalidFixing"] == v.THROWS, first
    assert all(x == v.THROWS for x in by_name["forecast 2"]["fixing"])


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/bmaindex.hpp", cases)
