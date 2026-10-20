# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's default-probability term structure (ql/termstructures/defaulttermstructure.hpp), through
the wheel's FlatHazardRate, HazardRateCurve (backward-flat hazard rates), DefaultDensityCurve (linear densities) and
SurvivalProbabilityCurve (linear probabilities).

- "curve" cases: on each curve, at a sample of dates past its reference date (the last ones beyond its maximum date,
  read with extrapolation): survivalProbability, defaultProbability, defaultDensity and hazardRate by date and by
  time, defaultProbability between the reference date's first anniversary and the date, and between their times;
  the same date beyond the maximum date without extrapolation, refused; and the default probability between a date
  and an earlier one, and a time and an earlier one, refused.

This file also holds the credit generators' shared inputs: the WeekendsOnly calendar beside
tools/vectors/gen/cashflows/cashflows.py's, a credit curve spec {"type": "flat", "referenceDate", "hazardRate",
"dayCounter"}, {"type": "flatSettlement", "settlementDays", "calendar", "hazardRate", "dayCounter"} or
{"type": "HazardRateCurve" | "DefaultDensityCurve" | "SurvivalProbabilityCurve", "dates", "data", "dayCounter"}, and
the discount curve spec of tools/vectors/gen/indexes/interestrateindex.py. A refused value is {"throws": true}. Read
back by tests/NanoQL.Tests/Vectors/TermStructures/DefaultTermStructureVectorTests.cs, whose inputs
tests/NanoQL.Tests/Vectors/CreditInputs.cs builds."""
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


cf = _load("wheel_cashflows", "cashflows/cashflows.py")
irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")

TODAY = ql.Date(15, ql.January, 2024)  # a Monday
A360, A365, THIRTY360_BB, ACT_ISDA = cf.A360, cf.A365, cf.THIRTY360_BB, cf.ACT_ISDA
TARGET = cf.TARGET
WEEKENDS_ONLY = {"type": "WeekendsOnly"}
INTERPOLATED = {"HazardRateCurve": ql.HazardRateCurve, "DefaultDensityCurve": ql.DefaultDensityCurve,
                "SurvivalProbabilityCurve": ql.SurvivalProbabilityCurve}


A360_INC = {"type": "Actual360", "includeLastDay": True}


def calendar(spec):
    return ql.WeekendsOnly() if spec["type"] == "WeekendsOnly" else cf.calendar(spec)


def day_counter(spec):
    """cashflows.py's day counters, and Actual/360 including the last day."""
    if spec["type"] == "Actual360" and spec.get("includeLastDay"):
        return ql.Actual360(True)
    return cf.day_counter(spec)


def flat_spec(reference, h, dc=A365):
    return {"type": "flat", "referenceDate": v.enc(reference), "hazardRate": h, "dayCounter": dc}


def flat_settlement_spec(days, cal, h, dc=A365):
    return {"type": "flatSettlement", "settlementDays": days, "calendar": cal, "hazardRate": h, "dayCounter": dc}


def curve_spec(kind, dates, data, dc=A365):
    return {"type": kind, "dates": v.enc(dates), "data": data, "dayCounter": dc}


def credit_curve(spec):
    """The wheel's credit curve a spec describes."""
    t = spec["type"]
    if t == "flat":
        return ql.FlatHazardRate(ql.Date(spec["referenceDate"]), ql.QuoteHandle(ql.SimpleQuote(spec["hazardRate"])),
                                 day_counter(spec["dayCounter"]))
    if t == "flatSettlement":
        return ql.FlatHazardRate(spec["settlementDays"], calendar(spec["calendar"]),
                                 ql.QuoteHandle(ql.SimpleQuote(spec["hazardRate"])), day_counter(spec["dayCounter"]))
    return INTERPOLATED[t]([ql.Date(d) for d in spec["dates"]], spec["data"], day_counter(spec["dayCounter"]))


def credit_handle(spec):
    return ql.DefaultProbabilityTermStructureHandle(credit_curve(spec))


def discount_spec(reference, forward, dc=A365, compounding=ql.Continuous, frequency=ql.Annual):
    return irx.curve_spec(reference, forward, dc, compounding, frequency)


def discount_curve_spec(dates, dfs, dc=A365):
    """A log-linear discount curve on nodes: the wheel's DiscountCurve."""
    return {"type": "DiscountCurve", "dates": v.enc(dates), "dfs": dfs, "dayCounter": dc}


def discount_handle(spec):
    """A discount spec's handle: interestrateindex.py's flat forward, or a log-linear DiscountCurve."""
    if spec is not None and spec.get("type") == "DiscountCurve":
        return ql.YieldTermStructureHandle(ql.DiscountCurve([ql.Date(d) for d in spec["dates"]], spec["dfs"],
                                                            day_counter(spec["dayCounter"])))
    return irx.handle(spec)


NODES = [TODAY, TODAY + ql.Period(1, ql.Years), TODAY + ql.Period(3, ql.Years), TODAY + ql.Period(5, ql.Years),
         TODAY + ql.Period(10, ql.Years)]
CURVES = [
    ("flat 2% A365", flat_spec(TODAY, 0.02)),
    ("flat 3.5% A360", flat_spec(TODAY, 0.035, A360)),
    ("hazard rates, backward flat", curve_spec("HazardRateCurve", NODES, [0.01, 0.01, 0.015, 0.02, 0.025])),
    ("default densities, linear", curve_spec("DefaultDensityCurve", NODES, [0.01, 0.012, 0.014, 0.013, 0.011])),
    ("survival probabilities, linear", curve_spec("SurvivalProbabilityCurve", NODES, [1.0, 0.985, 0.95, 0.91, 0.8],
                                                  THIRTY360_BB)),
]
SAMPLE = [17, 200, 365, 512, 1000, 1461, 2000, 3000, 3653, 4000, 5000]


def curve_cases():
    out = []
    for label, spec in CURVES:
        with v.case_settings(TODAY) as st:
            curve = credit_curve(spec)
            start = TODAY + ql.Period(1, ql.Years)
            for k, days in enumerate(SAMPLE):
                d = TODAY + days
                beyond = d > curve.maxDate()
                t = curve.timeFromReference(d)
                t0 = curve.timeFromReference(start)
                expected = {
                    "time": t,
                    "survivalProbability": v.attempt(lambda: curve.survivalProbability(d, True)),
                    "defaultProbability": v.attempt(lambda: curve.defaultProbability(d, True)),
                    "defaultDensity": v.attempt(lambda: curve.defaultDensity(d, True)),
                    "hazardRate": v.attempt(lambda: curve.hazardRate(d, True)),
                    "survivalProbabilityTime": v.attempt(lambda: curve.survivalProbability(t, True)),
                    "defaultProbabilityTime": v.attempt(lambda: curve.defaultProbability(t, True)),
                    "defaultDensityTime": v.attempt(lambda: curve.defaultDensity(t, True)),
                    "hazardRateTime": v.attempt(lambda: curve.hazardRate(t, True)),
                    "defaultProbabilityBetween": v.attempt(lambda: curve.defaultProbability(start, d, True)),
                    "defaultProbabilityBetweenTimes": v.attempt(lambda: curve.defaultProbability(t0, t, True)),
                    "unextrapolated": v.attempt(lambda: curve.survivalProbability(d)),
                    "reversed": v.attempt(lambda: curve.defaultProbability(d, start - 1, True)),
                    "reversedTimes": v.attempt(lambda: curve.defaultProbability(t, t - 0.25, True)),
                }
                out.append({"name": f"curve {label}, {days} days{' (beyond)' if beyond else ''} / {k}",
                            "inputs": {"settings": st, "curve": spec, "date": v.enc(d), "start": v.enc(start)},
                            "expected": expected})
    return out


DATA = {"HazardRateCurve": "hazardRates", "DefaultDensityCurve": "defaultDensities",
        "SurvivalProbabilityCurve": "survivalProbabilities"}


def interpolated_cases(kind, sets, refused):
    """The interpolated credit curves' cases, shared by their three generators: on each node set ("curve" cases) the
    dates, times, data and maximum date, and at the nodes, between them and past the last (read with extrapolation)
    the survival probability, hazard rate and default density; and each refused set ("construction" cases)."""
    out = []
    for k, (label, dates, data, dc) in enumerate(sets):
        with v.case_settings(TODAY) as st:
            spec = curve_spec(kind, dates, data, dc)
            curve = credit_curve(spec)
            sample = sorted(set(dates + [dates[i] + (dates[i + 1] - dates[i]) // 2 for i in range(len(dates) - 1)]
                                + [dates[-1] + 400]))
            expected = {"dates": v.enc(list(curve.dates())), "times": [curve.timeFromReference(d) for d in dates],
                        "data": list(getattr(curve, DATA[kind])()), "maxDate": v.enc(curve.maxDate()),
                        "survivalProbability": [v.attempt(lambda d=d: curve.survivalProbability(d, True))
                                                for d in sample],
                        "hazardRate": [v.attempt(lambda d=d: curve.hazardRate(d, True)) for d in sample],
                        "defaultDensity": [v.attempt(lambda d=d: curve.defaultDensity(d, True)) for d in sample]}
            out.append({"name": f"curve {label} / {k}",
                        "inputs": {"settings": st, "curve": spec, "sample": v.enc(sample)}, "expected": expected})
    for k, (label, dates, data) in enumerate(refused):
        with v.case_settings(TODAY) as st:
            spec = curve_spec(kind, dates, data)
            out.append({"name": f"construction {label} / {k}", "inputs": {"settings": st, "curve": spec},
                        "expected": {"construction": v.attempt(lambda: credit_curve(spec) and 0)}})
    return out


def make_cases():
    return curve_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/defaulttermstructure.hpp", make_cases)
