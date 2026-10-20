# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackConstantVol (ql/termstructures/volatility/equityfx/blackconstantvol.hpp): a constant
Black volatility by reference date and by settlement days.

- Fixed cases: four structures (two reference dates, volatilities 0, 18% and 65%, three day counters, two calendars):
  the reference date, the maximum date, the minimum and maximum strikes, the calendar's name, the business-day
  convention (Following), the day counter's name, and the volatility and variance at six times and five dates.
- Moving cases: a structure 0 or 2 TARGET business days after each of two evaluation dates, QuantLib's moving
  structure, which nanoql builds by BlackConstantVol.Create from the context: the reference date and the variance at
  a date.

The structure specs are tools/vectors/gen/termstructures/volatility/equityfx/blackvoltermstructure.py's. The wheel's
calls, by QuantLib-SWIG 1.44's volatilities.i: BlackConstantVol(referenceDate, calendar, volatility, dayCounter) and
BlackConstantVol(settlementDays, calendar, volatility, dayCounter), the C++ constructors on a number, and the base's
members. Read back by tests/NanoQL.Tests/Vectors/TermStructures/BlackConstantVolVectorTests.cs."""
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
    (ql.Date(15, ql.October, 2026), 0.18, bvts.A365, bvts.NULL_CALENDAR),
    (ql.Date(29, ql.February, 2024), 0.65, bvts.A360, bvts.TARGET),
    (ql.Date(15, ql.October, 2026), 0.0, bvts.ACT_ISDA, bvts.TARGET),
    (ql.Date(29, ql.February, 2024), 0.18, bvts.A365, bvts.TARGET),
]
TIMES = [0.0, 0.25, 1.0, 3.5, 10.0, 50.0]
DATE_OFFSETS = [0, 1, 180, 365, 3653]
STRIKES = [50.0, 100.0, 1e6]
EVALUATION_DATES = [ql.Date(15, ql.October, 2026), ql.Date(31, ql.December, 2027)]


def cases():
    out = []
    for reference, vol, dc, cal in FIXED:
        st = v.settings(ql.Date(15, ql.October, 2026))
        spec = bvts.vol_spec_constant(reference, vol, dc, cal)
        s = bvts.black_vol(spec)
        out.append({
            "name": f"{reference.ISO()}, {vol}, {dc['type']}",
            "inputs": {"settings": st, "structure": spec, "times": TIMES,
                       "dates": [(reference + d).serialNumber() for d in DATE_OFFSETS], "strikes": STRIKES},
            "expected": v.enc({
                "referenceDate": s.referenceDate(),
                "maxDate": s.maxDate(),
                "minStrike": s.minStrike(),
                "maxStrike": s.maxStrike(),
                "calendar": s.calendar().name(),
                "businessDayConvention": int(s.businessDayConvention()),
                "dayCounter": s.dayCounter().name(),
                "blackVol": [[s.blackVol(t, k) for k in STRIKES] for t in TIMES],
                "blackVariance": [[s.blackVariance(t, k) for k in STRIKES] for t in TIMES],
                "blackVolDate": [s.blackVol(reference + d, 100.0) for d in DATE_OFFSETS],
                "blackVarianceDate": [s.blackVariance(reference + d, 100.0) for d in DATE_OFFSETS]})})
    for evaluation in EVALUATION_DATES:
        for days in (0, 2):
            st = v.settings(evaluation)
            spec = bvts.vol_spec_constant(None, 0.25, bvts.A365, bvts.TARGET, settlement_days=days)
            s = bvts.black_vol(spec)
            out.append({
                "name": f"{days} settlement days from {evaluation.ISO()}",
                "inputs": {"settings": st, "structure": spec, "date": (evaluation + 400).serialNumber()},
                "expected": v.enc({"referenceDate": s.referenceDate(),
                                   "blackVariance": s.blackVariance(evaluation + 400, 100.0)})})
    v.settings(ql.Date(15, ql.October, 2026))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/blackconstantvol.hpp", cases)
