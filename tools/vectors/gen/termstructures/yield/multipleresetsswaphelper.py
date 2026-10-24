# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's multiple-resets swap rate helper (ql/termstructures/yield/multipleresetsswaphelper.hpp),
through the wheel's MultipleResetsSwapRateHelper, on the evaluation date 15 January 2025 and Euribor 1M and 3M
indexes with no curve of their own (the helper forwards on the curve it bootstraps).

- "helper" cases: a helper on a tenor, an index and the resets per coupon, compounding or averaging, with a spread,
  settlement days, a fixed frequency, day count and convention, and an exogenous discounting curve or none: its
  earliest, latest, maturity, pillar and latest relevant dates, its swap's start and maturity dates and fixed rate,
  and a log-linear discount curve (Actual/365 (Fixed)) bootstrapped on it alone: the discount at its latest date, its
  implied quote and quote error on that curve.
- "curve" cases: a curve bootstrapped on five helpers of 2 to 10 years: the discount at each helper's latest date, and
  each helper's implied quote.

A case's inputs hold its settings and the helpers ({"settlementDays", "tenor", "fixedRate", "index",
"resetsPerCoupon", "discountingCurve", "averaging", "spread", "fixedFrequency", "fixedDayCount", "fixedConvention"},
a null curve none, a null day count the index's), an index a spec of indexes/interestrateindex.py. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/MultipleResetsSwapHelperVectorTests.cs."""
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


irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")
cf = _load("wheel_cashflows", "cashflows/cashflows.py")
ibc = _load("wheel_iborcoupon", "cashflows/iborcoupon.py")

TODAY = irx.TODAY
E1M = ibc.euribor((1, ql.Months), None)
E3M = ibc.euribor((3, ql.Months), None)
DISCOUNT = irx.curve_spec(TODAY, 0.025)
AVERAGING = {"compounding": ql.RateAveraging.Compound, "averaging": ql.RateAveraging.Simple}


def helper_spec(tenor, rate, index, per_coupon, days=2, discount=None, averaging="compounding", spread=0.0,
                frequency=ql.NoFrequency, dc=None, convention=ql.ModifiedFollowing):
    return {"settlementDays": days, "tenor": [tenor[0], int(tenor[1])], "fixedRate": rate, "index": index,
            "resetsPerCoupon": per_coupon, "discountingCurve": discount, "averaging": averaging, "spread": spread,
            "fixedFrequency": int(frequency), "fixedDayCount": dc, "fixedConvention": int(convention)}


Y = ql.Years
HELPERS = [
    ("5Y on 3M, 2 resets", helper_spec((5, Y), 0.031, E3M, 2)),
    ("5Y on 1M, 3 resets, averaging", helper_spec((5, Y), 0.0305, E1M, 3, averaging="averaging")),
    ("10Y on 3M, 4 resets, a spread, an annual 30/360 fixed leg",
     helper_spec((10, Y), 0.033, E3M, 4, spread=0.001, frequency=ql.Annual, dc=cf.THIRTY360_BB)),
    ("3Y on 3M, 2 resets, a discounting curve", helper_spec((3, Y), 0.029, E3M, 2, discount=DISCOUNT)),
    ("2Y on 1M, 6 resets, 0 settlement days", helper_spec((2, Y), 0.028, E1M, 6, days=0)),
    ("7Y on 3M, 2 resets, a semiannual fixed leg, Following",
     helper_spec((7, Y), 0.0315, E3M, 2, frequency=ql.Semiannual, dc=cf.A365, convention=ql.Following)),
]
CURVES = [
    ("five helpers on 3M, 2 resets",
     [helper_spec((n, Y), r, E3M, 2) for n, r in ((2, 0.0285), (3, 0.0292), (5, 0.0305), (7, 0.0313), (10, 0.0322))]),
    ("five helpers on 1M, 3 resets, averaging, a discounting curve",
     [helper_spec((n, Y), r, E1M, 3, discount=DISCOUNT, averaging="averaging")
      for n, r in ((2, 0.028), (3, 0.0288), (5, 0.03), (7, 0.0309), (10, 0.0318))]),
]


def make_helper(spec):
    kw = {"averagingMethod": AVERAGING[spec["averaging"]], "spread": spec["spread"],
          "fixedFrequency": spec["fixedFrequency"], "fixedConvention": spec["fixedConvention"]}
    if spec["discountingCurve"] is not None:
        kw["discountingCurve"] = irx.handle(spec["discountingCurve"])
    if spec["fixedDayCount"] is not None:
        kw["fixedDayCount"] = cf.day_counter(spec["fixedDayCount"])
    return ql.MultipleResetsSwapRateHelper(spec["settlementDays"], ql.Period(spec["tenor"][0], spec["tenor"][1]),
                                           spec["fixedRate"], irx.make_index(spec["index"]), spec["resetsPerCoupon"],
                                           **kw)


def helper_cases():
    out = []
    for k, (label, spec) in enumerate(HELPERS):
        with v.case_settings(TODAY) as st:
            h = make_helper(spec)
            sw = h.swap()
            expected = {"earliestDate": v.enc(h.earliestDate()), "latestDate": v.enc(h.latestDate()),
                        "maturityDate": v.enc(h.maturityDate()), "pillarDate": v.enc(h.pillarDate()),
                        "latestRelevantDate": v.enc(h.latestRelevantDate()), "swapStartDate": v.enc(sw.startDate()),
                        "swapMaturityDate": v.enc(sw.maturityDate()), "swapFixedRate": sw.fixedRate()}
            # A curve bootstrapped on the helper alone; it is kept alive while the helper, which holds a raw pointer
            # to it, is read.
            curve = ql.PiecewiseLogLinearDiscount(TODAY, [h], ql.Actual365Fixed())
            expected.update({"bootstrapDiscount": v.attempt(lambda: curve.discount(h.latestDate())),
                             "impliedQuote": v.attempt(h.impliedQuote), "quoteError": v.attempt(h.quoteError)})
            out.append({"name": f"helper {label} / {k}",
                        "inputs": {"settings": st, "kind": "helper", "helpers": [spec]}, "expected": expected})
    return out


def curve_cases():
    out = []
    for k, (label, specs) in enumerate(CURVES):
        with v.case_settings(TODAY) as st:
            helpers = [make_helper(s) for s in specs]
            curve = ql.PiecewiseLogLinearDiscount(TODAY, helpers, ql.Actual365Fixed())
            out.append({"name": f"curve {label} / {k}", "inputs": {"settings": st, "kind": "curve", "helpers": specs},
                        "expected": {
                            "discounts": [v.attempt(lambda h=h: curve.discount(h.latestDate())) for h in helpers],
                            "impliedQuotes": [v.attempt(h.impliedQuote) for h in helpers]}})
    return out


def make_cases():
    return helper_cases() + curve_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/multipleresetsswaphelper.hpp", make_cases)
