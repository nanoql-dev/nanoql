# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's inflation term structures (ql/termstructures/inflationtermstructure.hpp): the free functions
inflationPeriod and inflationYearFraction, and the base classes' members, reached through the wheel's interpolated
curves.

- "periods" cases: inflationPeriod at 31 dates (month starts and ends, leap days, a year's first and last days) for
  each of QuantLib's 13 frequencies, as [date][frequency] of {"start", "end"} pairs flattened to two outputs, the
  frequencies QuantLib doesn't handle refused; and inflationYearFraction from two start dates to 16 of the dates, with
  and without interpolation, for the six frequencies it handles and three day counters.
- "members" cases, one per curve of CURVES (zero-inflation and year-on-year, with a fixed reference date, on monthly,
  quarterly and semiannual frequencies): the base class's referenceDate, baseDate, frequency, baseRate (refused for a
  zero curve, which has none), hasSeasonality and maxDate; zeroRate or yoyRate at the base date, one day before it
  (refused), at the maximum date and past it, with and without the extrapolate argument, as built and with
  enableExtrapolation() called; the time forms at the base time, before it (refused), at the maximum time and past it;
  and setSeasonality, QuantLib's setter, on a copy of the curve, the rates at 37 dates after it, then with the
  seasonality removed.

A curve spec is {"type": "ZeroInflationCurve" or "YoYInflationCurve", "referenceDate", "dates", "rates",
"frequency", "dayCounter", "seasonality", "enableExtrapolation"}: the wheel's ZeroInflationCurve or YoYInflationCurve
(QuantLib's InterpolatedZeroInflationCurve<Linear> and InterpolatedYoYInflationCurve<Linear>), the seasonality a
spec of tools/vectors/gen/termstructures/inflation/seasonality.py or null, the day counter as in
tools/vectors/gen/indexes/interestrateindex.py; enableExtrapolation, whether enableExtrapolation() is called on the
built curve. The curve functions here are shared by the other inflation generators, which import this file. Read
back by tests/NanoQL.Tests/Vectors/TermStructures/InflationTermStructureVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, *path):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


irx = _load("wheel_interestrateindex", "indexes", "interestrateindex.py")
seas = _load("wheel_seasonality", "termstructures", "inflation", "seasonality.py")

A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}

FREQUENCIES = [ql.NoFrequency, ql.Once, ql.Annual, ql.Semiannual, ql.EveryFourthMonth, ql.Quarterly, ql.Bimonthly,
               ql.Monthly, ql.EveryFourthWeek, ql.Biweekly, ql.Weekly, ql.Daily, ql.OtherFrequency]
HANDLED = [ql.Annual, ql.Semiannual, ql.EveryFourthMonth, ql.Quarterly, ql.Bimonthly, ql.Monthly]


def curve_spec(kind, reference, dates, rates, frequency, dc, seasonality=None, extrapolate=False):
    return {"type": kind, "referenceDate": v.enc(reference), "dates": v.enc(dates), "rates": list(rates),
            "frequency": int(frequency), "dayCounter": dc, "seasonality": seasonality,
            "enableExtrapolation": extrapolate}


def make_curve(spec):
    """The curve a spec describes, built with the wheel's linear interpolated curve."""
    cls = ql.ZeroInflationCurve if spec["type"] == "ZeroInflationCurve" else ql.YoYInflationCurve
    args = [ql.Date(spec["referenceDate"]), [ql.Date(d) for d in spec["dates"]], spec["rates"], spec["frequency"],
            irx.day_counter(spec["dayCounter"])]
    s = seas.make_seasonality(spec["seasonality"])
    c = cls(*args) if s is None else cls(*args, s)
    if spec["enableExtrapolation"]:
        c.enableExtrapolation()
    return c


def rate_dates(c):
    """37 dates from the curve's base date to past its maximum date."""
    base, last = c.baseDate(), c.maxDate()
    span = last - base
    return [base + (span * n) // 32 + (n % 5) for n in range(33)] + [last, last + 1, last + 40, last + 400]


def monthly_nodes(base, years):
    return [base] + [base + ql.Period(n, ql.Years) for n in years]


# (label, evaluation date, curve spec)
CURVES = [
    ("zero monthly", ql.Date(15, ql.June, 2024),
     curve_spec("ZeroInflationCurve", ql.Date(15, ql.June, 2024),
                monthly_nodes(ql.Date(1, ql.April, 2024), (1, 2, 3, 5, 7, 10, 20, 30)),
                [0.035, 0.035, 0.034, 0.033, 0.032, 0.0315, 0.031, 0.0305, 0.03], ql.Monthly, A365)),
    ("zero quarterly", ql.Date(28, ql.February, 2025),
     curve_spec("ZeroInflationCurve", ql.Date(28, ql.February, 2025),
                monthly_nodes(ql.Date(1, ql.October, 2024), (1, 2, 5, 10)),
                [0.021, 0.022, 0.024, 0.026, 0.0255], ql.Quarterly, THIRTY360_BB)),
    ("zero semiannual, negative", ql.Date(31, ql.July, 2023),
     curve_spec("ZeroInflationCurve", ql.Date(31, ql.July, 2023),
                monthly_nodes(ql.Date(1, ql.January, 2023), (1, 3, 6)),
                [-0.004, -0.002, 0.001, 0.0035], ql.Semiannual, A360, extrapolate=True)),
    ("yoy monthly", ql.Date(15, ql.June, 2024),
     curve_spec("YoYInflationCurve", ql.Date(15, ql.June, 2024),
                monthly_nodes(ql.Date(1, ql.April, 2024), (1, 2, 3, 5, 10, 15)),
                [0.031, 0.0305, 0.03, 0.029, 0.028, 0.027, 0.0265], ql.Monthly, A365)),
    ("yoy quarterly", ql.Date(29, ql.February, 2024),
     curve_spec("YoYInflationCurve", ql.Date(29, ql.February, 2024),
                monthly_nodes(ql.Date(1, ql.October, 2023), (1, 2, 4, 8)),
                [0.045, 0.04, 0.034, 0.029, 0.025], ql.Quarterly, ACT_ISDA, extrapolate=True)),
]

FACTORS = seas.TEST_FACTORS


def seasonal_curves():
    """(label, evaluation date, curve spec) of curves with seasonalities, which the seasonality's generator reads; a
    multi-year seasonality inconsistent with its curve among them."""
    out = []
    zero = CURVES[0][2]
    yoy = CURVES[3][2]
    for label, s in (
            ("monthly", seas.multiplicative(ql.Date(1, ql.January, 2024), ql.Monthly, FACTORS)),
            ("monthly, month-end base", seas.multiplicative(ql.Date(31, ql.January, 2007), ql.Monthly, FACTORS)),
            ("quarterly", seas.multiplicative(ql.Date(1, ql.February, 2023), ql.Quarterly, [1.002, 0.997, 1.001, 1.0])),
            ("two-year, consistent", seas.multiplicative(ql.Date(1, ql.January, 2024), ql.Monthly, FACTORS + FACTORS)),
            ("two-year, inconsistent", seas.multiplicative(ql.Date(1, ql.January, 2024), ql.Monthly,
                                                           FACTORS + list(reversed(FACTORS)))),
            ("daily", seas.multiplicative(ql.Date(1, ql.January, 2022), ql.Daily, seas.ramp(365, 0.999, 0.000005))),
            ("Kerkhof", seas.kerkhof(ql.Date(1, ql.June, 2023), FACTORS))):
        out.append((f"zero, {label}", CURVES[0][1], dict(zero, seasonality=s, enableExtrapolation=True)))
        if label in ("monthly", "two-year, inconsistent", "Kerkhof"):
            out.append((f"yoy, {label}", CURVES[3][1], dict(yoy, seasonality=s, enableExtrapolation=True)))
    return out


PERIOD_DATES = [ql.Date(1, ql.January, 2024), ql.Date(31, ql.January, 2024), ql.Date(29, ql.February, 2024),
                ql.Date(1, ql.March, 2024), ql.Date(31, ql.March, 2024), ql.Date(30, ql.April, 2024),
                ql.Date(15, ql.May, 2024), ql.Date(30, ql.June, 2024), ql.Date(1, ql.July, 2024),
                ql.Date(31, ql.August, 2024), ql.Date(30, ql.September, 2024), ql.Date(1, ql.October, 2024),
                ql.Date(30, ql.November, 2024), ql.Date(31, ql.December, 2024), ql.Date(28, ql.February, 2025),
                ql.Date(1, ql.March, 2025), ql.Date(17, ql.August, 2025), ql.Date(31, ql.December, 2025),
                ql.Date(29, ql.February, 2000), ql.Date(1, ql.January, 1901), ql.Date(31, ql.December, 2199),
                ql.Date(15, ql.January, 1999), ql.Date(14, ql.April, 2031), ql.Date(31, ql.May, 2040),
                ql.Date(1, ql.June, 2040), ql.Date(30, ql.June, 2050), ql.Date(1, ql.November, 2075),
                ql.Date(31, ql.October, 2026), ql.Date(1, ql.September, 2026), ql.Date(31, ql.July, 2027),
                ql.Date(2, ql.February, 2028)]
YEAR_FRACTION_STARTS = [ql.Date(1, ql.April, 2024), ql.Date(31, ql.January, 2024)]
YEAR_FRACTION_DATES = PERIOD_DATES[::2]


def period_cases():
    out = []
    with v.case_settings(ql.Date(15, ql.June, 2024)) as st:
        expected = {"start": [], "end": []}
        for d in PERIOD_DATES:
            starts, ends = [], []
            for f in FREQUENCIES:
                try:
                    p = ql.inflationPeriod(d, f)
                    starts.append(v.enc(p[0]))
                    ends.append(v.enc(p[1]))
                except RuntimeError:
                    starts.append(dict(v.THROWS))
                    ends.append(dict(v.THROWS))
            expected["start"].append(starts)
            expected["end"].append(ends)
        out.append({"name": "periods / 0",
                    "inputs": {"settings": st, "kind": "periods", "dates": v.enc(PERIOD_DATES),
                               "frequencies": [int(f) for f in FREQUENCIES]},
                    "expected": expected})
    for k, dc in enumerate([A365, A360, THIRTY360_BB]):
        with v.case_settings(ql.Date(15, ql.June, 2024)) as st:
            day_counter = irx.day_counter(dc)
            fractions = [[[[v.attempt(lambda s=s, d=d, f=f, i=i: ql.inflationYearFraction(f, i, day_counter, s, d))
                            for i in (False, True)] for f in HANDLED] for d in YEAR_FRACTION_DATES]
                         for s in YEAR_FRACTION_STARTS]
            out.append({"name": f"year fractions {day_counter.name()} / {k}",
                        "inputs": {"settings": st, "kind": "yearFractions", "dayCounter": dc,
                                   "starts": v.enc(YEAR_FRACTION_STARTS), "dates": v.enc(YEAR_FRACTION_DATES),
                                   "frequencies": [int(f) for f in HANDLED]},
                        "expected": {"inflationYearFraction": fractions}})
    return out


def member_cases():
    out = []
    for k, (label, evaluation_date, spec) in enumerate(CURVES):
        with v.case_settings(evaluation_date) as st:
            zero = spec["type"] == "ZeroInflationCurve"
            c = make_curve(spec)
            rate = c.zeroRate if zero else c.yoyRate
            base, last = c.baseDate(), c.maxDate()
            range_dates = [base, base - 1, last, last + 1, last + 365]
            base_time, max_time = c.timeFromReference(base), c.maxTime()
            range_times = [base_time, base_time - 0.01, max_time, max_time + 0.01, max_time + 5.0]
            expected = {
                "referenceDate": v.enc(c.referenceDate()),
                "baseDate": v.enc(base),
                "frequency": int(c.frequency()),
                "baseRate": v.attempt(c.baseRate),
                "hasSeasonality": c.hasSeasonality(),
                "maxDate": v.enc(last),
                "rateAtDates": [[v.attempt(lambda d=d, e=e: rate(d, e)) for e in (False, True)] for d in range_dates],
                "rateAtTimes": [[v.attempt(lambda t=t, e=e: rate(t, e)) for e in (False, True)] for t in range_times]}
            dates = rate_dates(c)
            s = seas.multiplicative(ql.Date(1, ql.January, 2024), ql.Monthly, FACTORS)
            copy = make_curve(dict(spec, enableExtrapolation=True))
            copy_rate = copy.zeroRate if zero else copy.yoyRate
            copy.setSeasonality(seas.make_seasonality(s))
            expected["hasSeasonalityAfterSet"] = copy.hasSeasonality()
            expected["rateAfterSet"] = [v.attempt(lambda d=d: copy_rate(d)) for d in dates]
            copy.setSeasonality(None)
            expected["hasSeasonalityAfterUnset"] = copy.hasSeasonality()
            expected["rateAfterUnset"] = [v.attempt(lambda d=d: copy_rate(d)) for d in dates]
            out.append({"name": f"members {label} / {k}",
                        "inputs": {"settings": st, "kind": "members", "curve": spec, "rangeDates": v.enc(range_dates),
                                   "rangeTimes": range_times, "seasonality": s, "dates": v.enc(dates)},
                        "expected": expected})
    return out


def cases():
    return period_cases() + member_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/inflationtermstructure.hpp", cases)
