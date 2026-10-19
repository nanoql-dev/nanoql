# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BondHelper and FixedRateBondHelper (ql/termstructures/yield/bondhelpers.hpp) where the
wheel can't reach, each output computed by the C++ harness: a helper's priceType(), and its implied quote and quote
error against a curve of one's choice, the helper bound to it by setTermStructure, which the wheel doesn't bind; on
the wheel generator's helpers and on helpers of bonds the wheel can't build.

- 22 cases, the helpers of tools/vectors/gen/termstructures/yield/bondhelpers.py's five sets (the test fixture's
  bonds on clean and on dirty prices, BondHelpers on bond.py's bonds, fixed-rate helpers with a payment calendar, an
  ex-coupon period and redemptions other than par, and a BondHelper on a floating-rate bond beside fixed-rate
  helpers), each under its set's evaluation date with the flags drawn with the seed.
- 5 cases on bonds the wheel can't build (cxx/instruments/bond.py's specs, under its first evaluation date): BondHelpers
  on a fixed-rate bond with a first-period day counter, on a floating-rate bond with no accrual day counter, and on a
  bond on QuantLib's empty calendar, whose construction QuantLib refuses (the helper reads the bond's next cash-flow
  date at its settlement date: "no calendar implementation provided"); and fixed-rate helpers with an ex-coupon month
  on the empty calendar and on a schedule from dates with a payment calendar.

A case's outputs are whether the helper builds (QuantLib's refusal recorded as its throw, the case's one output),
its price type, its five dates (earliest, maturity, latest relevant, pillar and latest), and its implied quote and
quote error bound to a flat forward curve (3.5%, continuous, Actual/365 (Fixed)) and to an interpolated discount curve
(log-linear on 10 nodes from the evaluation date to 50 years, Actual/365 (Fixed)), both with the evaluation date as
reference date. The quotes are prices per 100 of notional, compared as reals.

Each case's inputs hold its settings, "helper" (a helper spec of bondhelpers.py) and "curves" (the flat forward spec
and {"type": "DiscountCurve", "dates", "discounts", "dayCounter"}). Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/Yield/BondHelpersCxxVectorTests.cs."""
import importlib.util
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bd = _load("wheel_bond", "instruments/bond.py")
wh = _load("wheel_bondhelpers", "termstructures/yield/bondhelpers.py")
cb = _load("cxx_bond", "cxx/instruments/bond.py")

D = ql.Date
DATES = ["earliestDate", "maturityDate", "latestRelevantDate", "pillarDate", "latestDate"]


def curves(today):
    """The flat forward curve and log-linear discounts of a zero curve rising from 3.2% to 4.1% on 10 nodes to 50
    years, both from the evaluation date."""
    offsets = [0, 30, 182, 365, 730, 1826, 3652, 7305, 10958, 18262]
    dates = [today + n for n in offsets]
    dc = ql.Actual365Fixed()
    dfs = [1.0] + [math.exp(-(0.032 + 0.009 * (1.0 - math.exp(-n / 3000.0))) * dc.yearFraction(today, d))
                   for n, d in zip(offsets[1:], dates[1:])]
    flat = bd.curve_spec(today, 0.035)
    return [flat, {"type": "DiscountCurve", "dates": v.enc(dates), "discounts": dfs, "dayCounter": bd.A365}]


def harness_helpers():
    """(name, helper spec) for the 5 helpers on bonds the wheel can't build."""
    bonds = {name: spec for name, spec, _, _ in cb.harness_bonds()}
    month = bd.ex_coupon_spec(1, ql.Months, None, ql.Unadjusted)
    s1y = bd.schedule_spec(D(16, 6, 2025), D(16, 6, 2030), (1, ql.Years), bd.TARGET, ql.Unadjusted)
    from_dates = bd.dates_schedule_spec([D(16, 6, 2025), D(1, 9, 2025), D(16, 6, 2026), D(16, 6, 2027)])
    return [
        ("on a first-period day counter",
         wh.bond_helper_spec(100.6, bonds["fixed rate, first-period Actual/365 on 30/360"])),
        ("on no accrual day counter", wh.bond_helper_spec(99.4, bonds["floating rate, no accrual day counter"],
                                                          wh.DIRTY)),
        ("on the empty calendar", wh.bond_helper_spec(101.0, bonds["coupons on the empty calendar"])),
        ("ex-coupon a month on the empty calendar",
         wh.fixed_helper_spec(101.3, 2, 100.0, s1y, [0.035], bd.A365, ex_coupon=month, price_type=wh.DIRTY)),
        ("from dates, a payment calendar",
         wh.fixed_helper_spec(99.9, 1, 100.0, from_dates, [0.03], bd.A360, ql.Unadjusted, 100.0, None, bd.TARGET)),
    ]


def outputs(H, spec, bound):
    try:
        price_type = H.call("BondHelper::priceType", spec)
    except RuntimeError:
        return {"built": dict(v.THROWS)}
    out = {"built": True, "priceType": price_type}
    for m in DATES:
        out[m] = v.attempt(lambda m=m: H.call(f"BondHelper::{m}", spec))
    for name, curve in zip(["Flat", "Curve"], bound):
        out["impliedQuote" + name] = v.attempt(lambda: H.call("BondHelper::impliedQuote", spec, curve))
        out["quoteError" + name] = v.attempt(lambda: H.call("BondHelper::quoteError", spec, curve))
    return out


def cases():
    rnd = random.Random(v.SEED)
    rows = [(f"{name} / {k}", spec, today) for name, set_on, today in wh.SETS
            for k, spec in enumerate(set_on(today))]
    rows += [(name, spec, bd.EVALUATION_DATES[0]) for name, spec in harness_helpers()]
    out = []
    with cxx.connect() as H:
        for name, spec, today in rows:
            ref_events, todays = rnd.choice([False, True]), rnd.choice([None, False, True])
            bound = curves(today)
            with H.case_settings(v.enc(today), include_reference_date_events=ref_events,
                                 include_todays_cash_flows=todays) as st:
                out.append({"name": f"{name} / {today.ISO()} {ref_events} {todays}",
                            "inputs": {"settings": st, "helper": spec, "curves": bound},
                            "expected": outputs(H, spec, bound)})
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold, whatever the draws."""
    assert sum(c["expected"] == {"built": v.THROWS} for c in out) == 1
    assert {c["expected"].get("priceType") for c in out} == {None, wh.CLEAN, wh.DIRTY}
    assert all(isinstance(c["expected"]["impliedQuoteFlat"], float) for c in out if c["expected"]["built"] is True)


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/bondhelpers.hpp", cases)
