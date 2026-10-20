# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LocalConstantVol (ql/termstructures/volatility/equityfx/localconstantvol.hpp), and through
it the LocalVolTermStructure base's localVol (ql/termstructures/volatility/equityfx/localvoltermstructure.hpp, .cpp).

- Fixed cases: four structures (two reference dates, volatilities 0, 15% and 80%, three day counters): the reference
  and maximum dates, the minimum and maximum strikes, the day counter's name, the calendar's (QuantLib's empty
  calendar's, "null"), the business-day convention, and the local volatility at five times and four levels, and at
  three dates; the refusal of a negative time and of a date before the reference date.
- Moving cases: a structure 0 or 2 TARGET business days after each of two evaluation dates, which nanoql builds by
  LocalConstantVol.Create from the context: its reference date and the local volatility at a date.

The wheel's calls, by QuantLib-SWIG 1.44's volatilities.i: LocalConstantVol(referenceDate, volatility, dayCounter)
and LocalConstantVol(settlementDays, calendar, volatility, dayCounter), the C++ constructors on a number, and localVol
by time and by date, the base's. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/LocalConstantVolVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bvts = _load("blackvoltermstructure", "termstructures/volatility/equityfx/blackvoltermstructure.py")
FIXED = [
    (ql.Date(15, ql.October, 2026), 0.15, bvts.A365),
    (ql.Date(29, ql.February, 2024), 0.80, bvts.A360),
    (ql.Date(15, ql.October, 2026), 0.0, bvts.ACT_ISDA),
    (ql.Date(29, ql.February, 2024), 0.15, bvts.A365),
]
TIMES = [0.0, 0.5, 1.0, 7.0, 99.0]
LEVELS = [1e-6, 50.0, 100.0, 1e8]
DATE_OFFSETS = [0, 365, 7300]
EVALUATION_DATES = [ql.Date(15, ql.October, 2026), ql.Date(31, ql.December, 2027)]


def cases():
    out = []
    for reference, vol, dc in FIXED:
        st = v.settings(ql.Date(15, ql.October, 2026))
        s = ql.LocalConstantVol(reference, vol, bvts.day_counter(dc))
        out.append({
            "name": f"{reference.ISO()}, {vol}, {dc['type']}",
            "inputs": {"settings": st, "referenceDate": reference.serialNumber(), "volatility": vol,
                       "dayCounter": dc, "times": TIMES, "levels": LEVELS,
                       "dates": [(reference + d).serialNumber() for d in DATE_OFFSETS]},
            "expected": v.enc({
                "referenceDate": s.referenceDate(),
                "maxDate": s.maxDate(),
                "minStrike": s.minStrike(),
                "maxStrike": s.maxStrike(),
                "dayCounter": s.dayCounter().name(),
                "calendarEmpty": s.calendar().empty(),
                "businessDayConvention": int(s.businessDayConvention()),
                "localVol": [[s.localVol(t, x) for x in LEVELS] for t in TIMES],
                "localVolDate": [s.localVol(reference + d, 100.0) for d in DATE_OFFSETS],
                "negativeTime": v.attempt(lambda: s.localVol(-0.5, 100.0)),
                "dateBeforeReference": v.attempt(lambda: s.localVol(reference - 1, 100.0))})})
    for evaluation in EVALUATION_DATES:
        for days in (0, 2):
            st = v.settings(evaluation)
            s = ql.LocalConstantVol(days, ql.TARGET(), 0.25, ql.Actual365Fixed())
            out.append({
                "name": f"{days} settlement days from {evaluation.ISO()}",
                "inputs": {"settings": st, "settlementDays": days, "calendar": bvts.TARGET, "volatility": 0.25,
                           "dayCounter": bvts.A365, "date": (evaluation + 400).serialNumber()},
                "expected": v.enc({"referenceDate": s.referenceDate(),
                                   "localVol": s.localVol(evaluation + 400, 100.0)})})
    v.settings(ql.Date(15, ql.October, 2026))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/localconstantvol.hpp", cases)
