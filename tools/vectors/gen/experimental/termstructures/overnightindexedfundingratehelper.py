# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's overnight-indexed funding rate helper
(ql/experimental/termstructures/overnightindexedfundingratehelper.hpp, .cpp), each helper bootstrapped into a
PiecewiseLogLinearDiscount curve on Actual/365 (Fixed), as test-suite/overnightindexedfundingratehelper.cpp's cases.

- "bootstrap" cases: SOFR funding margins at 3M, 1Y, 2Y, 5Y and 10Y (QuantLib's first case's quotes; 30 September
  2025, a flat 4% projection curve, settlement 2, a quarterly Actual/360 schedule, a payment lag of 2 and telescopic
  value dates), and an AONIA overnight helper with no settlement or lag (QuantLib's third case, 15 July 2026): each
  helper's earliest, latest, latest relevant, pillar and maturity dates and its implied quote on the curve, the curve's
  discount at each pillar, and each helper's swap's two legs' payment dates and amounts.
- "dated" case: the helper on start and end dates (QuantLib's second case's; the wheel's forDates), alone in its
  curve: the same outputs.

Read back by tests/NanoQL.Tests/Vectors/Experimental/TermStructures/OvernightIndexedFundingRateHelperVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

USD = [([3, ql.Months], 0.0008750000), ([1, ql.Years], 0.0032250000), ([2, ql.Years], 0.0053769087),
       ([5, ql.Years], 0.0083935157), ([10, ql.Years], 0.0083935157)]


def flat(today, rate):
    return ql.YieldTermStructureHandle(ql.FlatForward(today, rate, ql.Actual365Fixed(), ql.Continuous))


def outputs(helpers, curve):
    out = {k: [] for k in ("earliestDates", "latestDates", "latestRelevantDates", "pillarDates", "maturityDates",
                           "impliedQuotes", "pillarDiscounts", "leg0Dates", "leg0Amounts", "leg1Dates", "leg1Amounts")}
    # the curve bootstraps on its first read, which binds the helpers to it
    curve.discount(helpers[-1].pillarDate())
    for h in helpers:
        out["earliestDates"].append(v.enc(h.earliestDate()))
        out["latestDates"].append(v.enc(h.latestDate()))
        out["latestRelevantDates"].append(v.enc(h.latestRelevantDate()))
        out["pillarDates"].append(v.enc(h.pillarDate()))
        out["maturityDates"].append(v.enc(h.maturityDate()))
        out["impliedQuotes"].append(h.impliedQuote())
        out["pillarDiscounts"].append(curve.discount(h.pillarDate()))
        swap = h.swap()
        for j in (0, 1):
            for cf in swap.leg(j):
                out[f"leg{j}Dates"].append(v.enc(cf.date()))
                out[f"leg{j}Amounts"].append(cf.amount())
    return out


def cases():
    out = []
    today = ql.Date(30, ql.September, 2025)
    st = v.settings(today)
    calendar = ql.UnitedStates(ql.UnitedStates.Settlement)
    sofr = ql.Sofr(flat(today, 0.04))
    helpers = [ql.OvernightIndexedFundingRateHelper(margin, ql.Period(*tenor), 2, calendar, ql.ModifiedFollowing, False,
                                                     sofr, ql.Period(3, ql.Months), ql.Actual360(), 2, True)
               for tenor, margin in USD]
    curve = ql.PiecewiseLogLinearDiscount(today, helpers, ql.Actual365Fixed())
    out.append({"name": "bootstrap: SOFR funding margins", "inputs": {"settings": st, "quotes": USD},
                "expected": outputs(helpers, curve)})

    start = ql.Date(2, ql.October, 2025)
    end = start + ql.Period(2, ql.Years)
    dated = ql.OvernightIndexedFundingRateHelper.forDates(0.003, start, end, calendar, ql.ModifiedFollowing, False,
                                                 ql.Sofr(flat(today, 0.041)), ql.Period(3, ql.Months), ql.Actual360(),
                                                 2, True)
    curve = ql.PiecewiseLogLinearDiscount(today, [dated], ql.Actual365Fixed())
    out.append({"name": "dated: start and end dates", "inputs": {"settings": st, "start": start.serialNumber(),
                                                                 "end": end.serialNumber()},
                "expected": outputs([dated], curve)})

    today = ql.Date(15, ql.July, 2026)
    st = v.settings(today)
    aonia = ql.Aonia(flat(today, 0.04))
    overnight = ql.OvernightIndexedFundingRateHelper(0.0004, ql.Period(1, ql.Days), 0, ql.Australia(), ql.Following,
                                                     False, aonia, ql.Period(12, ql.Months), ql.Actual365Fixed(), 0,
                                                     True)
    curve = ql.PiecewiseLogLinearDiscount(today, [overnight], ql.Actual365Fixed())
    out.append({"name": "bootstrap: an AONIA overnight pillar", "inputs": {"settings": st},
                "expected": outputs([overnight], curve)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/termstructures/overnightindexedfundingratehelper.hpp", cases)
