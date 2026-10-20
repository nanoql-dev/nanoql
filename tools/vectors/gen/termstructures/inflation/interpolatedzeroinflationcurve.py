# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's interpolated zero-inflation curve (ql/termstructures/inflation/interpolatedzeroinflationcurve.hpp),
through the wheel's ZeroInflationCurve, QuantLib's InterpolatedZeroInflationCurve<Linear>.

- "curve" cases, one per curve: its dates, times, data, rates and nodes (each node's date and rate, as two arrays),
  baseDate and maxDate, and zeroRate at 37 dates from the base date to past the maximum date and at 12 times, each
  without and with the extrapolate argument, as [date or time][extrapolate]; a date before the base date or past the
  maximum date without extrapolation is refused. The curves span reference dates on and after the base date, monthly,
  quarterly and annual frequencies, five day counters, negative rates and a first rate of -100% or below, which
  QuantLib doesn't check.
- "construction" cases: one date, rates and dates of different counts, a rate after the first of -100% or below,
  unsorted dates, two dates at the same time under the day counter: {"throws": true}. No date at all isn't sampled:
  QuantLib reads the first date before checking the count and throws std::out_of_range, which the port refuses with
  QuantLib's own text instead (tests/NanoQL.Tests/Divergences/InflationCurveFirstDateTests.cs).

Curve specs as in tools/vectors/gen/termstructures/inflationtermstructure.py, whose functions build them. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/InterpolatedZeroInflationCurveVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_inflationtermstructure", os.path.join(v.GEN_ROOT, "termstructures", "inflationtermstructure.py"))
its = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(its)

THIRTY360_EU = {"type": "Thirty360", "convention": int(ql.Thirty360.European)}
KIND = "ZeroInflationCurve"
RATE = "zeroRate"


def nodes(base, months):
    return [base] + [base + ql.Period(m, ql.Months) for m in months]


# (label, evaluation date, reference date, dates, rates, frequency, day counter)
CURVES = [
    ("monthly", ql.Date(15, ql.June, 2024), ql.Date(15, ql.June, 2024), nodes(ql.Date(1, ql.April, 2024), (12, 24, 60, 120, 360)),
     [0.031, 0.031, 0.029, 0.027, 0.0265, 0.025], ql.Monthly, its.A365),
    ("monthly, test-suite nodes", ql.Date(13, ql.August, 2007), ql.Date(13, ql.August, 2007),
     nodes(ql.Date(1, ql.June, 2007), (12, 24, 36, 48, 60, 84, 120, 144, 180, 240, 300, 360, 480, 600)),
     [0.0293, 0.0293, 0.0295, 0.02965, 0.0298, 0.03, 0.0306, 0.03175, 0.03243, 0.03293, 0.03338, 0.03348, 0.03348,
      0.03308, 0.03228], ql.Monthly, its.THIRTY360_BB),
    ("quarterly, negative", ql.Date(31, ql.January, 2023), ql.Date(31, ql.January, 2023),
     nodes(ql.Date(1, ql.October, 2022), (3, 6, 12, 24, 48)),
     [-0.01, -0.008, -0.004, 0.0, 0.004, 0.009], ql.Quarterly, its.A360),
    ("annual, reference on the base date", ql.Date(1, ql.January, 2025), ql.Date(1, ql.January, 2025),
     nodes(ql.Date(1, ql.January, 2025), (12, 24, 36)),
     [0.02, 0.02, 0.022, 0.0215], ql.Annual, its.ACT_ISDA),
    ("monthly, close nodes", ql.Date(27, ql.January, 2022), ql.Date(27, ql.January, 2022),
     [ql.Date(1, ql.December, 2021), ql.Date(3, ql.February, 2022), ql.Date(10, ql.February, 2022),
      ql.Date(27, ql.February, 2022), ql.Date(27, ql.March, 2022), ql.Date(27, ql.April, 2022),
      ql.Date(27, ql.July, 2022), ql.Date(27, ql.January, 2023), ql.Date(27, ql.January, 2024),
      ql.Date(27, ql.January, 2027), ql.Date(27, ql.January, 2032)],
     [0.01, 0.01, 0.011, 0.012, 0.013, 0.015, 0.018, 0.02, 0.025, 0.03, 0.03], ql.Monthly,
     {"type": "Actual360"}),
    ("semiannual, first rate -100%", ql.Date(30, ql.September, 2024), ql.Date(30, ql.September, 2024),
     nodes(ql.Date(1, ql.January, 2024), (6, 18, 30)),
     [-1.0, 0.03, 0.028, 0.026], ql.Semiannual, THIRTY360_EU),
]

TIMES = [0.0, -0.25, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0, 30.0, 49.0, 50.0, 80.0]

REFUSED = [
    ("one date", [ql.Date(1, ql.April, 2024)], [0.03]),
    ("count mismatch", [ql.Date(1, ql.April, 2024), ql.Date(1, ql.April, 2025)], [0.03]),
    ("count mismatch, more rates", [ql.Date(1, ql.April, 2024), ql.Date(1, ql.April, 2025)], [0.03, 0.03, 0.03]),
    ("rate -100%", [ql.Date(1, ql.April, 2024), ql.Date(1, ql.April, 2025), ql.Date(1, ql.April, 2026)],
     [0.03, -1.0, 0.03]),
    ("rate below -100%", [ql.Date(1, ql.April, 2024), ql.Date(1, ql.April, 2025)], [0.03, -1.5]),
    ("unsorted", [ql.Date(1, ql.April, 2024), ql.Date(1, ql.April, 2026), ql.Date(1, ql.April, 2025)],
     [0.03, 0.03, 0.03]),
    ("same date", [ql.Date(1, ql.April, 2024), ql.Date(1, ql.April, 2025), ql.Date(1, ql.April, 2025)],
     [0.03, 0.03, 0.03]),
    ("same time", [ql.Date(1, ql.April, 2024), ql.Date(30, ql.July, 2024), ql.Date(31, ql.July, 2024)],
     [0.03, 0.03, 0.03]),
]


def curve_cases(kind, rate_name):
    out = []
    for k, (label, evaluation_date, reference, dates, rates, frequency, dc) in enumerate(CURVES):
        with v.case_settings(evaluation_date) as st:
            spec = its.curve_spec(kind, reference, dates, rates, frequency, dc)
            c = its.make_curve(spec)
            rate = getattr(c, rate_name)
            sample = its.rate_dates(c)
            node_list = c.nodes()
            out.append({
                "name": f"curve {label} / {k}",
                "inputs": {"settings": st, "kind": "curve", "curve": spec, "dates": v.enc(sample), "times": TIMES},
                "expected": {
                    "dates": v.enc(list(c.dates())),
                    "times": list(c.times()),
                    "data": list(c.data()),
                    "rates": list(c.rates()),
                    "nodeDates": v.enc([n[0] for n in node_list]),
                    "nodeRates": [n[1] for n in node_list],
                    "baseDate": v.enc(c.baseDate()),
                    "maxDate": v.enc(c.maxDate()),
                    "rateAtDates": [[v.attempt(lambda d=d, e=e: rate(d, e)) for e in (False, True)] for d in sample],
                    "rateAtTimes": [[v.attempt(lambda t=t, e=e: rate(t, e)) for e in (False, True)] for t in TIMES]}})
    return out


def construction_cases(kind):
    out = []
    for k, (label, dates, rates) in enumerate(REFUSED):
        with v.case_settings(ql.Date(15, ql.June, 2024)) as st:
            dc = THIRTY360_EU if label == "same time" else its.A365
            spec = its.curve_spec(kind, ql.Date(15, ql.June, 2024), dates, rates, ql.Monthly, dc)
            out.append({"name": f"construction {label} / {k}",
                        "inputs": {"settings": st, "kind": "construction", "curve": spec},
                        "expected": {"construction": v.attempt(lambda spec=spec: its.make_curve(spec) and 0)}})
    return out


def cases():
    return curve_cases(KIND, RATE) + construction_cases(KIND)


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/inflation/interpolatedzeroinflationcurve.hpp", cases)
