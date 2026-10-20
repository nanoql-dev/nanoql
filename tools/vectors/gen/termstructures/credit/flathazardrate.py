# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's flat hazard rate (ql/termstructures/credit/flathazardrate.hpp), through the wheel's
FlatHazardRate on a quote, which nanoql takes as a value.

- "date" cases: the curve on a reference date (a weekday, a Saturday), at hazard rates of 0, 2% and 15%, Actual/365
  (Fixed), Actual/360 or 30/360 (Bond Basis).
- "settlement" cases: the curve the given business days after the evaluation date (0, 1 or 2) on TARGET or
  WeekendsOnly, the evaluation date a Monday, a Saturday or Good Friday.

Each case gives the reference date, the maximum date, and at a sample of times the hazard rate, the survival and
default probabilities and the default density, and the survival probability a year after the reference date. A
case's inputs hold its settings and the curve spec of tools/vectors/gen/termstructures/defaulttermstructure.py. Read
back by tests/NanoQL.Tests/Vectors/TermStructures/Credit/FlatHazardRateVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_defaulttermstructure",
                                               os.path.join(v.GEN_ROOT, "termstructures", "defaulttermstructure.py"))
dts = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dts)

TODAY = dts.TODAY
TIMES = [0.0, 0.25, 1.0, 2.5, 10.0, 30.0]
BY_DATE = [(TODAY, 0.02, dts.A365), (ql.Date(20, ql.January, 2024), 0.15, dts.A360),
           (TODAY, 0.0, dts.THIRTY360_BB)]
EVALUATIONS = [TODAY, ql.Date(16, ql.March, 2024), ql.Date(29, ql.March, 2024)]


def outputs(curve):
    ref = curve.referenceDate()
    return {"referenceDate": v.enc(ref), "maxDate": v.enc(curve.maxDate()),
            "hazardRate": [curve.hazardRate(t) for t in TIMES],
            "survivalProbability": [curve.survivalProbability(t) for t in TIMES],
            "defaultProbability": [curve.defaultProbability(t) for t in TIMES],
            "defaultDensity": [curve.defaultDensity(t) for t in TIMES],
            "survivalAfterAYear": curve.survivalProbability(ref + ql.Period(1, ql.Years))}


def make_cases():
    out = []
    for k, (ref, h, dc) in enumerate(BY_DATE):
        with v.case_settings(TODAY) as st:
            spec = dts.flat_spec(ref, h, dc)
            out.append({"name": f"date {ref.ISO()} {h} / {k}", "inputs": {"settings": st, "curve": spec},
                        "expected": outputs(dts.credit_curve(spec))})
    k = 0
    for ev in EVALUATIONS:
        for days in (0, 1, 2):
            for cal in (dts.TARGET, dts.WEEKENDS_ONLY):
                with v.case_settings(ev) as st:
                    spec = dts.flat_settlement_spec(days, cal, 0.03)
                    out.append({"name": f"settlement {ev.ISO()} {days} days on {cal['type']} / {k}",
                                "inputs": {"settings": st, "curve": spec}, "expected": outputs(dts.credit_curve(spec))})
                k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/credit/flathazardrate.hpp", make_cases)
