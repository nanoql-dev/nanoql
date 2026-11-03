# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CDS bootstrap helpers (ql/termstructures/credit/defaultprobabilityhelpers.hpp), through the
wheel's SpreadCdsHelper and UpfrontCdsHelper, bound to the PiecewiseFlatHazardRate they bootstrap.

- "helpers" cases: each helper set under its evaluation date: spread helpers under the 2015 rule on WeekendsOnly
  (mid-point and ISDA models), under the 2009 rule from a given start date, and on a pre-2009 IMM schedule on TARGET;
  upfront helpers under the 2009 rule (mid-point and ISDA models). For each helper, its earliest, latest, pillar,
  maturity and latest relevant dates, and its implied quote and quote error on the curve.

The implied quotes and quote errors are solved, so the stability filter runs on them: the curve is rebuilt with the
helpers' quotes moved one ulp up, then down, together and each alone, and an output is written only where every move
leaves it within 1e-12 x max(|a|, |b|, 1); a dropped one is written null and its index listed in the case's inputs
"dropped".

A case's inputs hold its settings, the helper specs {"kind", "quote", "runningSpread", "tenor", "settlementDays",
"calendar", "frequency", "convention", "rule", "dayCounter", "recoveryRate", "discount", "upfrontSettlementDays",
"settlesAccrual", "paysAtDefaultTime", "startDate", "lastPeriodDayCounter", "rebatesAccrual", "model", "tradeDate"},
and the curve {"referenceDate" or "settlementDays" with "calendar", "dayCounter"}, as
tools/vectors/gen/termstructures/credit/piecewisedefaultcurve.py shares them. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Credit/DefaultProbabilityHelpersVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_defaulttermstructure",
                                               os.path.join(v.GEN_ROOT, "termstructures", "defaulttermstructure.py"))
dts = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dts)

M, Y = int(ql.Months), int(ql.Years)
MIDPOINT, ISDA = int(ql.CreditDefaultSwap.Midpoint), int(ql.CreditDefaultSwap.ISDA)
STABLE = 1e-12
MONDAY, SATURDAY = ql.Date(15, ql.January, 2024), ql.Date(16, ql.March, 2024)


def helper_spec(kind, quote, tenor, ev, rule, cal=None, running=None, settlement_days=1, dc=dts.A360,
                last_dc=dts.A360_INC, convention=ql.Following, model=MIDPOINT, start=None, upfront_days=3):
    return {"kind": kind, "quote": quote, "runningSpread": running, "tenor": [tenor[0], tenor[1]],
            "settlementDays": settlement_days, "calendar": cal or dts.WEEKENDS_ONLY, "frequency": int(ql.Quarterly),
            "convention": int(convention), "rule": int(rule), "dayCounter": dc, "recoveryRate": 0.4,
            "discount": dts.discount_spec(ev, 0.03), "upfrontSettlementDays": upfront_days, "settlesAccrual": True,
            "paysAtDefaultTime": True, "startDate": v.enc(start), "lastPeriodDayCounter": last_dc,
            "rebatesAccrual": True, "model": model, "tradeDate": None}


def make_helper(spec, quote):
    common = [ql.Period(spec["tenor"][0], spec["tenor"][1]), spec["settlementDays"], dts.calendar(spec["calendar"]),
              spec["frequency"], spec["convention"], spec["rule"], dts.day_counter(spec["dayCounter"]),
              spec["recoveryRate"], dts.discount_handle(spec["discount"])]
    tail = [spec["settlesAccrual"], spec["paysAtDefaultTime"],
            ql.Date(spec["startDate"]) if spec["startDate"] is not None else ql.Date(),
            dts.day_counter(spec["lastPeriodDayCounter"]), spec["rebatesAccrual"], spec["model"], ql.Date()]
    if spec["kind"] == "spread":
        return ql.SpreadCdsHelper(quote, *common, *tail)
    return ql.UpfrontCdsHelper(quote, spec["runningSpread"], *common, spec["upfrontSettlementDays"], *tail)


def make_curve(curve, helpers):
    dc = dts.day_counter(curve["dayCounter"])
    if curve.get("referenceDate") is not None:
        return ql.PiecewiseFlatHazardRate(ql.Date(curve["referenceDate"]), helpers, dc)
    return ql.PiecewiseFlatHazardRate(curve["settlementDays"], dts.calendar(curve["calendar"]), helpers, dc)


def spread_set(ev, rule, model=MIDPOINT, cal=None, start=None, dc=dts.A360, settlement_days=1):
    quotes = [((6, M), 0.0045), ((1, Y), 0.006), ((2, Y), 0.0075), ((3, Y), 0.009), ((5, Y), 0.0115),
              ((7, Y), 0.013), ((10, Y), 0.0145)]
    return [helper_spec("spread", q, t, ev, rule, cal, model=model, start=start, dc=dc,
                        settlement_days=settlement_days) for t, q in quotes]


def upfront_set(ev, model=MIDPOINT):
    quotes = [((1, Y), -0.0035), ((2, Y), 0.0021), ((3, Y), 0.0098), ((5, Y), 0.026), ((7, Y), 0.04)]
    return [helper_spec("upfront", q, t, ev, ql.DateGeneration.CDS, running=0.05, model=model) for t, q in quotes]


def curve_spec(ev, dc=dts.A365):
    return {"referenceDate": v.enc(ev), "settlementDays": None, "calendar": None, "dayCounter": dc}


SETS = [
    ("spread, 2015 rule, mid-point", MONDAY, lambda ev: spread_set(ev, ql.DateGeneration.CDS2015)),
    ("spread, 2015 rule, ISDA", MONDAY, lambda ev: spread_set(ev, ql.DateGeneration.CDS2015, ISDA)),
    ("spread, 2015 rule, ISDA, a Saturday", SATURDAY, lambda ev: spread_set(ev, ql.DateGeneration.CDS2015, ISDA)),
    ("spread, 2009 rule from a start date", MONDAY,
     lambda ev: spread_set(ev, ql.DateGeneration.CDS, start=ql.Date(20, ql.September, 2023))),
    ("spread, IMM on TARGET, 30/360", MONDAY,
     lambda ev: spread_set(ev, ql.DateGeneration.TwentiethIMM, cal=dts.TARGET, dc=dts.THIRTY360_BB)),
    ("upfront, mid-point", MONDAY, lambda ev: upfront_set(ev)),
    ("upfront, ISDA", MONDAY, lambda ev: upfront_set(ev, ISDA)),
]


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


def filtered(ev, specs, curve, solved, tally):
    """solved(curve, helpers) for the built curve and for every move of the quotes, each output kept only where
    stable; returns the outputs and the dropped indexes, by name."""
    def run(quotes):
        with v.case_settings(ev):
            helpers = [make_helper(s, q) for s, q in zip(specs, quotes)]
            built = make_curve(curve, helpers)
            built.dates()  # the bootstrap, which binds the helpers, runs when the curve is first read
            return solved(built, helpers)
    quotes = [s["quote"] for s in specs]
    base = run(quotes)
    moved = [run(m) for m in moves(quotes)]
    dropped = {}
    for name, values in base.items():
        keep = [all(stable(values[i], m[name][i]) for m in moved) for i in range(len(values))]
        dropped[name] = [i for i, kept in enumerate(keep) if not kept]
        base[name] = [x if kept else None for x, kept in zip(values, keep)]
        tally[0] += len(dropped[name])
        tally[1] += len(values)
    return base, dropped


def make_cases():
    out = []
    tally = [0, 0]
    for k, (label, ev, build) in enumerate(SETS):
        specs = build(ev)
        curve = curve_spec(ev)
        base, dropped = filtered(
            ev, specs, curve,
            lambda built, helpers: {"impliedQuotes": [h.impliedQuote() for h in helpers],
                                    "quoteErrors": [h.quoteError() for h in helpers]}, tally)
        print(f"helpers {label}: dropped {sum(len(d) for d in dropped.values())} outputs")
        with v.case_settings(ev) as st:
            helpers = [make_helper(s, s["quote"]) for s in specs]
            make_curve(curve, helpers)
            expected = {"earliestDates": v.enc([h.earliestDate() for h in helpers]),
                        "latestDates": v.enc([h.latestDate() for h in helpers]),
                        "pillarDates": v.enc([h.pillarDate() for h in helpers]),
                        "maturityDates": v.enc([h.maturityDate() for h in helpers]),
                        "latestRelevantDates": v.enc([h.latestRelevantDate() for h in helpers])}
            expected.update(base)
            out.append({"name": f"helpers {label} / {k}",
                        "inputs": {"settings": st, "helpers": specs, "curve": curve, "dropped": dropped},
                        "expected": expected})
    print(f"termstructures/credit/defaultprobabilityhelpers: dropped {tally[0]} of {tally[1]} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/credit/defaultprobabilityhelpers.hpp", make_cases)
