# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's piecewise zero-inflation curve (ql/termstructures/inflation/piecewisezeroinflationcurve.hpp)
bootstrapped on zero-coupon inflation swap helpers (ql/termstructures/inflation/inflationhelpers.hpp), through the
wheel's PiecewiseZeroInflation (Linear, IterativeBootstrap) and ZeroCouponInflationSwapHelper.

- "curve" cases: a UK RPI curve on helpers observed flat or linearly, with each pillar choice, at QuantLib's default
  accuracy, 1e-14, passed explicitly since the wheel's default is 1e-12: the curve's dates, times, data, baseDate,
  maxDate, zeroRate at 24 dates; each helper's pillar, earliest and latest dates and its implied quote on the curve;
  and, after setSeasonality, which bootstraps the curve again, its data and zeroRate at the same dates.

The stability filter runs on each case: the curve is rebuilt with its helpers' quotes moved one ulp up, then down,
together and each alone, and a solved output (a datum, a rate, an implied quote) is written only where every move
leaves it within 1e-12 x max(|a|, |b|, 1); a dropped one is written null and its index listed in the case's inputs
"dropped". The generator prints "dropped n of m outputs" per case and for the file. QuantLib re-bootstraps a curve
given a seasonality from its own nodes; nanoql builds it afresh, which differs by up to 7.9e-15
(tests/NanoQL.Tests/Divergences/SetSeasonalityColdStartTests.cs), within the tolerance.

A case's inputs hold its settings, the index spec and fixings (tools/vectors/gen/cashflows/indexedcashflow.py's,
the index with no curve), the helpers' {"quote", "maturity", "pillar", "customPillarDate"} with their shared lag,
calendar, convention, day counter and interpolation, the curve's reference and base dates, frequency, day counter and
accuracy, the seasonality (tools/vectors/gen/termstructures/inflation/seasonality.py's spec), the sample dates and
"dropped". Read back by tests/NanoQL.Tests/Vectors/TermStructures/PiecewiseZeroInflationCurveVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_indexedcashflow",
                                               os.path.join(v.GEN_ROOT, "cashflows", "indexedcashflow.py"))
icf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(icf)
cf = icf.cf
its = icf.its

TODAY = icf.TODAY
UK = cf.UK_SETTLEMENT
M = int(ql.Months)
STABLE = 1e-12
QUOTES = [(1, 0.0302), (2, 0.0298), (3, 0.0295), (5, 0.0291), (7, 0.029), (10, 0.0293), (15, 0.0297), (20, 0.0301),
          (30, 0.0303)]
SEASONALITY = its.seas.multiplicative(ql.Date(1, ql.January, 2024), ql.Monthly, its.FACTORS)

# (label, lag months, interpolation, pillar, day counter, seasonality)
CURVES = [
    ("flat", 3, ql.CPI.Flat, ql.Pillar.LastRelevantDate, cf.A365, SEASONALITY),
    ("linear, last relevant date", 3, ql.CPI.Linear, ql.Pillar.LastRelevantDate, cf.A365, SEASONALITY),
    ("linear, maturity date", 3, ql.CPI.Linear, ql.Pillar.MaturityDate, cf.THIRTY360_BB, None),
    ("flat, two-month lag", 2, ql.CPI.Flat, ql.Pillar.LastRelevantDate, cf.THIRTY360_BB, SEASONALITY),
]


def helper_specs(quotes):
    return [{"quote": q, "maturity": v.enc(TODAY + ql.Period(y, ql.Years)), "pillar": None, "customPillarDate": None}
            for y, q in quotes]


def build(inputs, quotes, index):
    lag = ql.Period(inputs["observationLag"][0], inputs["observationLag"][1])
    helpers = [ql.ZeroCouponInflationSwapHelper(ql.QuoteHandle(ql.SimpleQuote(q)), lag, ql.Date(h["maturity"]),
                                                cf.calendar(inputs["calendar"]), inputs["convention"],
                                                cf.day_counter(inputs["dayCounter"]), index, inputs["interpolation"],
                                                inputs["pillar"])
               for h, q in zip(inputs["helpers"], quotes)]
    curve = ql.PiecewiseZeroInflation(ql.Date(inputs["referenceDate"]), ql.Date(inputs["baseDate"]),
                                      inputs["frequency"], cf.day_counter(inputs["curveDayCounter"]), helpers, None,
                                      inputs["accuracy"])
    return curve, helpers


def outputs(inputs, quotes, index):
    """The solved outputs of a build: the curve's data, rates and the helpers' implied quotes, then with the
    seasonality."""
    curve, helpers = build(inputs, quotes, index)
    dates = [ql.Date(d) for d in inputs["dates"]]
    out = {"data": list(curve.data()), "rates": [v.attempt(lambda d=d: curve.zeroRate(d)) for d in dates],
           "impliedQuotes": [h.impliedQuote() for h in helpers]}
    if inputs["seasonality"] is not None:
        curve.setSeasonality(its.seas.make_seasonality(inputs["seasonality"]))
        out["seasonalData"] = list(curve.data())
        out["seasonalRates"] = [v.attempt(lambda d=d: curve.zeroRate(d)) for d in dates]
    return out, curve, helpers


def stable(base, moved):
    if isinstance(base, dict) or isinstance(moved, dict):
        return base == moved
    return abs(base - moved) <= STABLE * max(abs(base), abs(moved), 1.0)


def moves(inputs):
    out = [[math.nextafter(x, math.inf) for x in inputs], [math.nextafter(x, -math.inf) for x in inputs]]
    for i in range(len(inputs)):
        for direction in (math.inf, -math.inf):
            moved = list(inputs)
            moved[i] = math.nextafter(inputs[i], direction)
            if moved not in out:
                out.append(moved)
    return out


def curve_cases():
    out = []
    total = [0, 0]
    for k, (label, lag, interp, pillar, dc, seasonality) in enumerate(CURVES):
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(icf.UKRPI_NO_CURVE, icf.UK_FIXINGS)
            quotes = [q for _, q in QUOTES]
            sample = [TODAY + 30 * n + (n % 4) for n in range(0, 24 * 15, 15)]
            inputs = {"settings": st, "index": icf.UKRPI_NO_CURVE, "fixings": icf.UK_FIXINGS,
                      "helpers": helper_specs(QUOTES), "observationLag": [lag, M], "calendar": UK,
                      "convention": int(ql.ModifiedFollowing), "dayCounter": dc, "interpolation": int(interp),
                      "pillar": int(pillar), "referenceDate": v.enc(TODAY),
                      "baseDate": v.enc(index.lastFixingDate()), "frequency": int(ql.Monthly),
                      "curveDayCounter": dc, "accuracy": 1e-14, "seasonality": seasonality,
                      "dates": v.enc(sample)}
            base, curve, helpers = outputs(inputs, quotes, index)
            moved = [outputs(inputs, m, index)[0] for m in moves(quotes)]
            dropped = {}
            for name, values in base.items():
                keep = [all(stable(values[i], m[name][i]) for m in moved) for i in range(len(values))]
                dropped[name] = [i for i, kept in enumerate(keep) if not kept]
                base[name] = [x if kept else None for x, kept in zip(values, keep)]
                total[0] += len(dropped[name])
                total[1] += len(values)
            n = sum(len(d) for d in dropped.values())
            print(f"curve {label}: dropped {n} of {sum(len(x) for x in base.values())} outputs")
            inputs["dropped"] = dropped
            # the curve rebuilt as built, before setSeasonality
            curve, helpers = build(inputs, quotes, index)
            expected = {"dates": v.enc(list(curve.dates())), "times": list(curve.times()),
                        "baseDate": v.enc(curve.baseDate()), "maxDate": v.enc(curve.maxDate()),
                        "pillarDates": v.enc([h.pillarDate() for h in helpers]),
                        "earliestDates": v.enc([h.earliestDate() for h in helpers]),
                        "latestDates": v.enc([h.latestDate() for h in helpers])}
            expected.update(base)
            out.append({"name": f"curve {label} / {k}", "inputs": inputs, "expected": expected})
    print(f"termstructures/inflation/piecewisezeroinflationcurve: dropped {total[0]} of {total[1]} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/inflation/piecewisezeroinflationcurve.hpp", curve_cases)
