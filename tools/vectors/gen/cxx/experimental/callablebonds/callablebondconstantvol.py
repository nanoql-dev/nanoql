# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CallableBondConstantVolatility (ql/experimental/callablebonds/callablebondconstantvol.hpp,
.cpp) and the members it inherits from CallableBondVolatilityStructure (callablebondvolstructure.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness.

Seven structures under 16 September 2015: CallableBondConstantVolatility by reference date (the evaluation date, a
date before it and one two years on; Actual/365, Actual/360 and Thirty/360), with QuantLib's empty calendar and
Following convention, and by settlement days on TARGET (0 and 2), whose reference date moves with the evaluation date;
and two of the harness's own subclass of CallableBondVolatilityStructure, which overrides only what the base leaves
abstract (tools/cxx/src/experimental/callablebonds/callablebondspec.hpp), so that the base's defaults answer: the
maximum bond length from the maximum tenor (30 years, and 18 months on Thirty/360), the volatility by date through the
date conversion, and the range checks of a bounded length, tenor and strike. For each: the inspectors (reference
date, day counter, maximum date and time, maximum bond tenor and length, strike limits, business-day convention and
settlement days, null where QuantLib throws "not provided"); volatility and blackVariance by time and bond length at
eight points (a negative time and a negative length among them, refused), by option date and bond tenor at six (a
date before the reference date and a zero, a negative and a 101-year tenor among them, refused unless extrapolating),
and by option and bond tenors at five (the empty calendar refuses tenors in days, and its Following convention any
adjusted date; TARGET computes them), each with extrapolate false and true; the smile sections by date and by tenors
at a strike of 4% (tools/cxx/src/termstructures/volatility/smilesectionspec.hpp's summary: exercise date, exercise
time, volatility type, shift, ATM level and volatility); convertDates (refused for a tenor ending on or before the date);
and optionDateFromTenor. A refused value is {"throws": true}. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/CallableBonds/CallableBondConstantVolCxxVectorTests.cs, with
tests/NanoQL.Tests/Vectors/CallableBondInputs.cs."""
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
TODAY = D(16, 9, 2015)
A365, A360 = {"type": "Actual365Fixed"}, {"type": "Actual360"}
THIRTY360_BB = {"type": "Thirty360", "convention": int(ql.Thirty360.BondBasis)}
TARGET = {"type": "TARGET"}
DAYS, WEEKS, MONTHS, YEARS = int(ql.Days), int(ql.Weeks), int(ql.Months), int(ql.Years)

STRUCTURES = [
    ("by date, the evaluation date, Actual/365", {"referenceDate": TODAY.serialNumber(), "volatility": 0.2,
                                                  "dayCounter": A365}),
    ("by date, a year back, Actual/360", {"referenceDate": (TODAY - 365).serialNumber(), "volatility": 0.35,
                                          "dayCounter": A360}),
    ("by date, two years on, Thirty/360", {"referenceDate": (TODAY + 731).serialNumber(), "volatility": 0.05,
                                           "dayCounter": THIRTY360_BB}),
    ("moving, no settlement days", {"settlementDays": 0, "calendar": TARGET, "volatility": 0.25,
                                    "dayCounter": A365}),
    ("moving, two settlement days", {"settlementDays": 2, "calendar": TARGET, "volatility": 1e-10,
                                     "dayCounter": A360}),
    ("the base's defaults, a 30-year tenor and strikes in [0, 0.06]",
     {"type": "HarnessCallableBondVolatility", "referenceDate": TODAY.serialNumber(), "volatility": 0.15,
      "dayCounter": A365, "maxBondTenor": [30, YEARS], "minStrike": 0.0, "maxStrike": 0.06}),
    ("the base's defaults, an 18-month tenor, Thirty/360",
     {"type": "HarnessCallableBondVolatility", "referenceDate": (TODAY + 10).serialNumber(), "volatility": 0.3,
      "dayCounter": THIRTY360_BB, "maxBondTenor": [18, MONTHS], "minStrike": -0.01, "maxStrike": 0.05}),
]

TIME_POINTS = [(0.0, 1.0, 0.03), (0.5, 4.5, 0.05), (2.0, 0.0, 100.0), (10.0, 20.0, -0.02), (30.0, 99.0, 1e6),
               (1e-8, 1e-8, 0.0), (-0.1, 1.0, 0.03), (1.0, -0.5, 0.03)]
DATE_POINTS = [(TODAY + 30, [5, YEARS]), (TODAY + 400, [18, MONTHS]), (TODAY + 3650, [100, YEARS]),
               (TODAY - 10, [2, YEARS]), (TODAY + 90, [0, YEARS]), (TODAY + 90, [101, YEARS]),
               (TODAY + 90, [-1, YEARS])]
TENOR_POINTS = [([6, MONTHS], [5, YEARS]), ([1, YEARS], [10, YEARS]), ([2, WEEKS], [3, MONTHS]),
                ([10, DAYS], [1, YEARS]), ([0, DAYS], [2, YEARS])]


def cases():
    out = []
    with cxx.connect() as H:
        for name, vol in STRUCTURES:
            with H.case_settings(TODAY.serialNumber()) as st:
                def on(method, *args):
                    return v.attempt(lambda: H.call(f"CallableBondVolatilityStructure::{method}", vol, *args))

                def inspector(method):
                    try:
                        return v.result(lambda: H.call(f"CallableBondVolatilityStructure::{method}", vol))
                    except RuntimeError:
                        return dict(v.THROWS)

                expected = {k: inspector(k) for k in ("referenceDate", "dayCounter", "maxDate", "maxTime",
                                                      "maxBondTenor", "maxBondLength", "minStrike", "maxStrike",
                                                      "businessDayConvention", "settlementDays")}
                for kind, points, tag in (("Time,Time,Rate", TIME_POINTS, "ByTime"),
                                          ("Date,Period,Rate", DATE_POINTS, "ByDate"),
                                          ("Period,Period,Rate", TENOR_POINTS, "ByTenor")):
                    for member in ("volatility", "blackVariance"):
                        for extrapolate in (False, True):
                            key = f"{member}{tag}{'Extrapolated' if extrapolate else ''}"
                            expected[key] = [on(f"{member}({kind})", *encoded(kind, p), extrapolate) for p in points]
                expected["smileSectionByDate"] = [on("smileSection(Date,Period)", d.serialNumber(), t, 0.04)
                                                  for d, t in DATE_POINTS]
                expected["smileSectionByTenor"] = [on("smileSection(Period,Period)", o, t, 0.04)
                                                   for o, t in TENOR_POINTS]
                expected["convertDates"] = [on("convertDates", d.serialNumber(), t) for d, t in DATE_POINTS]
                expected["optionDateFromTenor"] = [on("optionDateFromTenor", o) for o, _ in TENOR_POINTS]
                out.append({"name": name,
                            "inputs": {"settings": st, "volatility": vol,
                                       "timePoints": [list(p) for p in TIME_POINTS],
                                       "datePoints": [[d.serialNumber(), t] for d, t in DATE_POINTS],
                                       "tenorPoints": [[o, t] for o, t in TENOR_POINTS], "strike": 0.04},
                            "expected": expected})
    return out


def encoded(kind, p):
    if kind == "Date,Period,Rate":
        d, t = p
        return [d.serialNumber(), t, 0.04]
    if kind == "Period,Period,Rate":
        o, t = p
        return [o, t, 0.04]
    return list(p)


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/callablebonds/callablebondconstantvol.hpp", cases)
