# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BondHelper and FixedRateBondHelper (ql/termstructures/yield/bondhelpers.hpp): yield curves
bootstrapped on bond helpers, the helpers' dates and their implied quotes on the curves.

Five helper sets, each under one evaluation date with flags drawn with the seed: QuantLib's test fixture's five bonds
(test-suite/piecewiseyieldcurve.cpp's bondData: FixedRateBondHelpers on TARGET, 3 settlement days, Actual/Actual
(ISDA), Following, at par) on clean prices and on dirty ones; BondHelpers on four bonds of
tools/vectors/gen/instruments/bond.py's specs (two zero-coupon bonds, an amortizing bond and a fixed-rate bond); fixed
-rate helpers with a payment calendar, an ex-coupon period and redemptions other than par; and BondHelpers on a
floating-rate bond, which forecasts on its own index's curve while the helper discounts on the curve, beside fixed-rate
helpers. Each set is bootstrapped by 4 iterative curves of
tools/vectors/gen/termstructures/yield/piecewiseyieldcurve.py (log-linear discounts, linear zero rates, backward-flat
and linear forward rates), each with a fixed reference date two TARGET days after the evaluation date. QuantLib
refuses one curve, the linear forward rates on the bonds' set, whose amortizing bond it can't bracket ("root
not bracketed"): its throw is its "built" output, its other outputs empty.

A set's outputs are each helper's five dates (earliestDate, maturityDate, latestRelevantDate, pillarDate, latestDate,
one element per helper) and its price type; and for each curve (one element per curve) piecewiseyieldcurve.py's:
whether it builds, its nodes' dates and data, its maximum date, its discounts at 40 dates, each helper's quote error
on it, and here each helper's implied quote on it. The helpers' quotes are prices per 100 of notional, compared as
reals. The harness's cxx/termstructures/yield/bondhelpers covers the price type on its own and the implied quote on a
given curve. Read back by tests/NanoQL.Tests/Vectors/TermStructures/Yield/BondHelpersVectorTests.cs."""
import importlib.util
import os
import random
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


bd = _load("wheel_bond", "instruments/bond.py")
frb = _load("wheel_floatingratebond", "instruments/bonds/floatingratebond.py")
pyc = _load("wheel_piecewiseyieldcurve", "termstructures/yield/piecewiseyieldcurve.py")

D = ql.Date
TARGET, UK = bd.TARGET, bd.UK_SETTLEMENT
CLEAN, DIRTY = int(ql.BondPrice.Clean), int(ql.BondPrice.Dirty)
CURVES = [pyc.PAIRS[0], pyc.PAIRS[5], pyc.PAIRS[8], pyc.PAIRS[9]]

# test-suite/piecewiseyieldcurve.cpp's bondData: (n, units, length in years, frequency, coupon %, price)
BOND_DATA = [(6, ql.Months, 5, ql.Semiannual, 4.75, 101.320), (1, ql.Years, 3, ql.Semiannual, 2.75, 100.590),
             (2, ql.Years, 5, ql.Semiannual, 5.00, 105.650), (5, ql.Years, 11, ql.Semiannual, 5.50, 113.610),
             (10, ql.Years, 11, ql.Semiannual, 3.75, 104.070)]


def fixed_helper_spec(price, days, face, schedule, coupons, dc, convention=ql.Following, redemption=100.0, issue=None,
                      payment_calendar=None, ex_coupon=None, price_type=CLEAN):
    return {"type": "FixedRateBondHelper", "price": price, "settlementDays": days, "faceAmount": face,
            "schedule": schedule, "coupons": coupons, "dayCounter": dc, "paymentConv": int(convention),
            "redemption": redemption, "issueDate": v.enc(issue), "paymentCalendar": payment_calendar,
            "exCouponPeriod": ex_coupon, "priceType": price_type}


def bond_helper_spec(price, bond, price_type=CLEAN):
    return {"type": "BondHelper", "price": price, "bond": bond, "priceType": price_type}


def make_helper(spec):
    quote = ql.QuoteHandle(ql.SimpleQuote(spec["price"]))
    if spec["type"] == "BondHelper":
        return ql.BondHelper(quote, bd.make_bond(spec["bond"]), spec["priceType"])
    kw = {"paymentConvention": spec["paymentConv"], "redemption": spec["redemption"],
          "issueDate": bd.date_or_null(spec["issueDate"]), "priceType": spec["priceType"]}
    if spec["paymentCalendar"] is not None:
        kw["paymentCalendar"] = bd.calendar(spec["paymentCalendar"])
    kw.update(bd.ex_coupon_kw(spec["exCouponPeriod"]))
    return ql.FixedRateBondHelper(quote, spec["settlementDays"], spec["faceAmount"], bd.make_schedule(spec["schedule"]),
                                  spec["coupons"], bd.day_counter(spec["dayCounter"]), **kw)


def fixture(today, price_type):
    """The test fixture's bond helpers, at its prices; dirty prices are the clean ones plus the accrued amount."""
    out = []
    cal = ql.TARGET()
    for n, units, length, freq, coupon, price in BOND_DATA:
        maturity = cal.advance(today, n, units)
        issue = cal.advance(maturity, -length, ql.Years)
        sch = bd.schedule_spec(issue, maturity, (ql.Period(freq).length(), ql.Period(freq).units()), TARGET,
                               ql.Following)
        spec = fixed_helper_spec(price, 3, 100.0, sch, [coupon / 100.0], bd.ACT_ISDA, ql.Following, 100.0, issue)
        if price_type == DIRTY:
            # the clean price plus the accrued amount of the bond the helper builds, at its settlement date
            accrued = make_helper(spec).bond().accruedAmount()
            spec = dict(spec, price=price + accrued, priceType=DIRTY)
        out.append(spec)
    return out


def bond_set(today):
    """BondHelpers on bonds of bond.py's specs."""
    s6m = bd.schedule_spec(D(15, 9, 2024), D(15, 9, 2031), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    sinking = bd.schedule_spec(D(15, 1, 2025), D(15, 1, 2033), (1, ql.Years), TARGET)
    return [
        bond_helper_spec(97.1, bd.zero_bond_spec(2, TARGET, 100.0, today + 365)),
        bond_helper_spec(90.2, bd.zero_bond_spec(2, TARGET, 100.0, today + 3 * 365 + 12)),
        bond_helper_spec(99.6, bd.amortizing_bond_spec(2, [100.0, 85.0, 70.0, 55.0, 40.0, 25.0, 10.0, 5.0], sinking,
                                                       [0.04], bd.A365), DIRTY),
        bond_helper_spec(103.4, bd.fixed_bond_spec(2, 100.0, s6m, [0.045], bd.A360, ql.Following, 100.0,
                                                   D(15, 9, 2024))),
    ]


def featured_set(today):
    """Fixed-rate helpers with a payment calendar, an ex-coupon period and redemptions other than par."""
    out = []
    for years, coupon, price, redemption in [(2, 0.03, 100.4, 100.0), (4, 0.035, 101.2, 101.0),
                                             (7, 0.04, 102.5, 99.5), (12, 0.042, 101.9, 100.0)]:
        sch = bd.schedule_spec(today - 30, today - 30 + ql.Period(years, ql.Years), (1, ql.Years), TARGET,
                               ql.Unadjusted)
        out.append(fixed_helper_spec(price, 2, 1e6, sch, [coupon], bd.THIRTY360_BB, ql.ModifiedFollowing, redemption,
                                     None, UK, bd.ex_coupon_spec(10, ql.Days, TARGET)))
    return out


def floating_set(today):
    """A BondHelper on a floating-rate bond, beside fixed-rate helpers."""
    s6m = bd.schedule_spec(today + 2, today + 2 + ql.Period(5, ql.Years), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    frn = bd.floating_bond_spec(2, 100.0, s6m, frb.euribor((6, ql.Months), today, 0.03), bd.A360)
    out = [bond_helper_spec(100.5, frn)]
    for years, coupon, price in [(1, 0.025, 99.8), (3, 0.03, 100.1), (8, 0.0375, 101.0)]:
        sch = bd.schedule_spec(today, today + ql.Period(years, ql.Years), (1, ql.Years), TARGET, ql.Following)
        out.append(fixed_helper_spec(price, 2, 100.0, sch, [coupon], bd.A365))
    return out


SETS = [("fixture, clean", lambda today: fixture(today, CLEAN), D(15, 1, 2025)),
        ("fixture, dirty", lambda today: fixture(today, DIRTY), D(15, 1, 2025)),
        ("bonds", bond_set, D(16, 6, 2025)),
        ("featured fixed-rate helpers", featured_set, D(14, 3, 2025)),
        ("floating beside fixed", floating_set, D(16, 6, 2025))]


def helper_outputs(specs):
    out = {"earliestDate": [], "maturityDate": [], "latestRelevantDate": [], "pillarDate": [], "latestDate": [],
           "priceType": []}
    for spec in specs:
        h = make_helper(spec)
        for k in ("earliestDate", "maturityDate", "latestRelevantDate", "pillarDate", "latestDate"):
            out[k].append(v.enc(getattr(h, k)()))
        out["priceType"].append(spec["priceType"])
    return out


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for name, set_on, today in SETS:
        ref_events, todays = rnd.choice([False, True]), rnd.choice([None, False, True])
        with v.case_settings(today, include_reference_date_events=ref_events, include_todays_cash_flows=todays) as st:
            specs = set_on(today)
            expected = helper_outputs(specs)
            keys = ["built", "dates", "data", "maxDate", "discounts", "quoteErrors", "impliedQuotes"]
            expected.update({k: [] for k in keys})
            curves, discount_inputs, flag_inputs = [], [], []
            for traits, interpolator, dc in CURVES:
                curve_spec = pyc.curve_spec(traits, interpolator, dc, today)
                curves.append(curve_spec)
                helpers = [make_helper(s) for s in specs]
                try:
                    curve = pyc.make_curve(curve_spec, helpers)
                    curve.nodes()
                except RuntimeError:
                    one = {"built": dict(v.THROWS), "dates": [], "data": [], "maxDate": None, "discounts": [],
                           "quoteErrors": [], "impliedQuotes": []}
                    dates, flags = [], []
                else:
                    one, dates, flags = pyc.curve_outputs(curve, helpers)
                    one["impliedQuotes"] = [v.attempt(h.impliedQuote) for h in helpers]
                for k in keys:
                    expected[k].append(one[k])
                discount_inputs.append(dates)
                flag_inputs.append(flags)
            out.append({"name": f"{name} / {today.ISO()} {ref_events} {todays}",
                        "inputs": {"settings": st, "helpers": specs, "curves": curves,
                                   "discountDates": discount_inputs, "extrapolate": flag_inputs},
                        "expected": expected})
    # the bonds' set is inconsistent with linear forward rates: QuantLib refuses that curve, "root not bracketed"
    assert sum(b is not True for c in out for b in c["expected"]["built"]) == 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/bondhelpers.hpp", cases)
