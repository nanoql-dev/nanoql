# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InterpolatedPiecewiseForwardSpreadedTermStructure
(ql/termstructures/yield/piecewiseforwardspreadedtermstructure.hpp): spreads at dates, interpolated, added to an
original curve's instantaneous forward rate.

The six originals of tools/vectors/gen/termstructures/yield/zerospreadedtermstructure.py, under its evaluation date
Monday 16 June 2025, each under linear and backward-flat interpolation (the wheel's
PiecewiseLinearForwardSpreadedTermStructure and PiecewiseForwardSpreadedTermStructure) and three spread schedules:
two whose first spread is at the reference date (0.4%, 0.6%, 0 and 1.2% at the reference date, six months, two years
and thirty; -0.2% and 0.8% at the reference date and five years), and one of two spreads from a later date (1% at a
year and 1.5% at ten).

A case is one original under one schedule and interpolator. Its outputs are the curve's reference date, day counter
(its name), calendar (its name, or null), maximum date (the earlier of the original's and the last spread's) and
time, and whether it allows extrapolation (the original's flag); and at the query dates its discount, Actual/365
(Fixed) continuous zero rate, semiannual forward rate over the six months after the date and instantaneous continuous
forward rate. A refused value is {"throws": true}. The query dates are the zero-spreaded generator's 30, those before
the first spread's date left out, and that date itself when it follows the reference date.

The later schedule's expected values (the S-sr plan's SSR6 (vii)) are QuantLib's own curve with the first spread
prepended at the reference date: QuantLib integrates the spread from the first spread's time, leaving out the flat
spread it applies before it, where nanoql integrates from the reference date, which that curve's integral does, bit
for bit with one interpolated segment (tests/NanoQL.Tests/Divergences/ForwardSpreadFirstDateTests.cs). Its reads
before the first spread's date, where QuantLib's prepended curve averages a flat spread by its primitive, s·t/t, and
nanoql takes the spread itself, are left out.

Each case's inputs hold its settings, the "original" (the zero-spreaded generator's spec), the "spreads", the
"spreadDates" (the curve's, without the prepended one), the "interpolator" ("Linear" or "BackwardFlat") and the
"dates". Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/PiecewiseForwardSpreadedTermStructureVectorTests.cs."""
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


zs = _load("zerospreadedtermstructure", "termstructures/yield/zerospreadedtermstructure.py")
fs = _load("forwardspreadedtermstructure", "termstructures/yield/forwardspreadedtermstructure.py")

SCHEDULES = [
    [(ql.Period(0, ql.Days), 0.004), (ql.Period(6, ql.Months), 0.006), (ql.Period(2, ql.Years), 0.0),
     (ql.Period(30, ql.Years), 0.012)],
    [(ql.Period(0, ql.Days), -0.002), (ql.Period(5, ql.Years), 0.008)],
    [(ql.Period(1, ql.Years), 0.01), (ql.Period(10, ql.Years), 0.015)],
]
CLASSES = {"Linear": ql.PiecewiseLinearForwardSpreadedTermStructure,
           "BackwardFlat": ql.PiecewiseForwardSpreadedTermStructure}


def cases():
    out = []
    for name, spec in zs.ORIGINALS:
        for schedule in SCHEDULES:
            for interpolator, cls in CLASSES.items():
                with v.case_settings(zs.TODAY) as st:
                    base = zs.original(spec)
                    reference = base.referenceDate()
                    dates = [reference + p for p, _ in schedule]
                    spreads = [s for _, s in schedule]
                    oracle_dates, oracle_spreads = dates, spreads
                    if dates[0] != reference:
                        # QuantLib's curve with the first spread prepended at the reference date
                        assert len(dates) == 2
                        oracle_dates, oracle_spreads = [reference] + dates, [spreads[0]] + spreads
                    curve = cls(ql.YieldTermStructureHandle(base),
                                [ql.QuoteHandle(ql.SimpleQuote(s)) for s in oracle_spreads], oracle_dates)
                    # reads before the first spread's date left out, and at it, whose instantaneous forward reads the
                    # curve just before it
                    query = [d for d in zs.query_dates(base, spec)
                             if d > dates[0] or (d == dates[0] == reference)]
                    out.append({"name": f"{name} / {len(schedule)} spreads from {schedule[0][0]} / {interpolator}",
                                "inputs": {"settings": st, "original": spec, "spreads": spreads,
                                           "spreadDates": v.enc(dates), "interpolator": interpolator,
                                           "dates": v.enc(query)},
                                "expected": fs.curve_outputs(curve, query)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/piecewiseforwardspreadedtermstructure.hpp", cases)
