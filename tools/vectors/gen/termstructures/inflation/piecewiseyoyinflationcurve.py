# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's piecewise year-on-year inflation curve
(ql/termstructures/inflation/piecewiseyoyinflationcurve.hpp) bootstrapped on year-on-year inflation swap helpers
(ql/termstructures/inflation/inflationhelpers.hpp), through the wheel's PiecewiseYoYInflation (Linear,
IterativeBootstrap) and YearOnYearInflationSwapHelper.

- "curve" cases: a curve on a UK RPI ratio index's helpers, observed flat or linearly, with each pillar choice, at
  QuantLib's default accuracy, 1e-12: the curve's dates, times, data, baseDate, maxDate, yoyRate at 24 dates; each
  helper's pillar, earliest and latest dates and its implied quote on the curve; and, after setSeasonality, which
  bootstraps the curve again, its data and yoyRate at the same dates. A linear helper on the ratio index observes its
  coupons where QuantLib's test of its ratio branch and nanoql's agree, all of them forecast.

The stability filter runs on each case as on the zero curve's
(tools/vectors/gen/termstructures/inflation/piecewisezeroinflationcurve.py): a solved output is written only where
every move of the helpers' quotes by one ulp leaves it within 1e-12 x max(|a|, |b|, 1), a dropped one null and its
index in the case's inputs "dropped"; the generator prints "dropped n of m outputs".

A case's inputs hold its settings, the index spec and fixings (tools/vectors/gen/cashflows/indexedcashflow.py's, the
ratio index with no curve), the nominal curve, the helpers' {"quote", "maturity"} with their shared lag, calendar,
convention, day counter, interpolation and pillar, the curve's reference and base dates, base rate, frequency, day
counter and accuracy, the seasonality, the sample dates and "dropped". Read back by
tests/NanoQL.Tests/Vectors/TermStructures/PiecewiseYoYInflationCurveVectorTests.cs."""
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


icf = _load("wheel_indexedcashflow", "cashflows", "indexedcashflow.py")
pz = _load("wheel_piecewisezero", "termstructures", "inflation", "piecewisezeroinflationcurve.py")
cf = icf.cf
ii = icf.ii
its = icf.its

TODAY = icf.TODAY
UK = cf.UK_SETTLEMENT
M = int(ql.Months)
RATIO = ii.ratio(ii.preset("UKRPI"))
QUOTES = [(1, 0.0305), (2, 0.03), (3, 0.0296), (5, 0.0291), (7, 0.0289), (10, 0.029), (15, 0.0293), (20, 0.0296)]

# (label, lag months, interpolation, pillar, day counter, seasonality)
CURVES = [
    ("flat", 2, ql.CPI.Flat, ql.Pillar.LastRelevantDate, cf.THIRTY360_BB, pz.SEASONALITY),
    ("linear, last relevant date", 3, ql.CPI.Linear, ql.Pillar.LastRelevantDate, cf.A365, pz.SEASONALITY),
    ("linear, maturity date", 3, ql.CPI.Linear, ql.Pillar.MaturityDate, cf.A365, None),
]


def build(inputs, quotes, index):
    lag = ql.Period(inputs["observationLag"][0], inputs["observationLag"][1])
    nominal = icf.nominal_curve(inputs["nominalCurve"])
    helpers = [ql.YearOnYearInflationSwapHelper(ql.QuoteHandle(ql.SimpleQuote(q)), lag, ql.Date(h["maturity"]),
                                                cf.calendar(inputs["calendar"]), inputs["convention"],
                                                cf.day_counter(inputs["dayCounter"]), index, inputs["interpolation"],
                                                nominal, inputs["pillar"])
               for h, q in zip(inputs["helpers"], quotes)]
    curve = ql.PiecewiseYoYInflation(ql.Date(inputs["referenceDate"]), ql.Date(inputs["baseDate"]),
                                     inputs["baseYoYRate"], inputs["frequency"],
                                     cf.day_counter(inputs["curveDayCounter"]), helpers, None, inputs["accuracy"])
    return curve, helpers


def outputs(inputs, quotes, index):
    curve, helpers = build(inputs, quotes, index)
    dates = [ql.Date(d) for d in inputs["dates"]]
    out = {"data": list(curve.data()), "rates": [v.attempt(lambda d=d: curve.yoyRate(d)) for d in dates],
           "impliedQuotes": [h.impliedQuote() for h in helpers]}
    if inputs["seasonality"] is not None:
        curve.setSeasonality(its.seas.make_seasonality(inputs["seasonality"]))
        out["seasonalData"] = list(curve.data())
        out["seasonalRates"] = [v.attempt(lambda d=d: curve.yoyRate(d)) for d in dates]
    return out


def curve_cases():
    out = []
    total = [0, 0]
    for k, (label, lag, interp, pillar, dc, seasonality) in enumerate(CURVES):
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(RATIO, icf.UK_FIXINGS)
            quotes = [q for _, q in QUOTES]
            sample = [TODAY + 30 * n + (n % 4) for n in range(0, 24 * 10, 10)]
            inputs = {"settings": st, "index": RATIO, "fixings": icf.UK_FIXINGS, "nominalCurve": icf.NOMINAL,
                      "helpers": [{"quote": q, "maturity": v.enc(TODAY + ql.Period(y, ql.Years))} for y, q in QUOTES],
                      "observationLag": [lag, M], "calendar": UK, "convention": int(ql.ModifiedFollowing),
                      "dayCounter": dc, "interpolation": int(interp), "pillar": int(pillar),
                      "referenceDate": v.enc(TODAY), "baseDate": v.enc(index.underlyingIndex().lastFixingDate()),
                      "baseYoYRate": QUOTES[0][1], "frequency": int(ql.Monthly), "curveDayCounter": dc,
                      "accuracy": 1e-12, "seasonality": seasonality, "dates": v.enc(sample)}
            base = outputs(inputs, quotes, index)
            moved = [outputs(inputs, m, index) for m in pz.moves(quotes)]
            dropped = {}
            for name, values in base.items():
                keep = [all(pz.stable(values[i], m[name][i]) for m in moved) for i in range(len(values))]
                dropped[name] = [i for i, kept in enumerate(keep) if not kept]
                base[name] = [x if kept else None for x, kept in zip(values, keep)]
                total[0] += len(dropped[name])
                total[1] += len(values)
            n = sum(len(d) for d in dropped.values())
            print(f"curve {label}: dropped {n} of {sum(len(x) for x in base.values())} outputs")
            inputs["dropped"] = dropped
            curve, helpers = build(inputs, quotes, index)
            expected = {"dates": v.enc(list(curve.dates())), "times": list(curve.times()),
                        "baseDate": v.enc(curve.baseDate()), "maxDate": v.enc(curve.maxDate()),
                        "pillarDates": v.enc([h.pillarDate() for h in helpers]),
                        "earliestDates": v.enc([h.earliestDate() for h in helpers]),
                        "latestDates": v.enc([h.latestDate() for h in helpers])}
            expected.update(base)
            out.append({"name": f"curve {label} / {k}", "inputs": inputs, "expected": expected})
    print(f"termstructures/inflation/piecewiseyoyinflationcurve: dropped {total[0]} of {total[1]} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/inflation/piecewiseyoyinflationcurve.hpp", curve_cases)
