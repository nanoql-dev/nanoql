# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's zero-coupon inflation swap (ql/instruments/zerocouponinflationswap.hpp), through the
wheel's ZeroCouponInflationSwap priced by the discounting swap engine on a flat nominal curve.

- "swap" cases: payer and receiver swaps from today or a past start, observed flat or linearly, with the inflation
  leg's calendar and convention given or defaulted: startDate, maturityDate; the fixed leg's and the inflation leg's
  cash flow (date and amount, and the inflation flow's base and fixing dates); NPV, legNPV, fixedLegNPV,
  inflationLegNPV, endDiscounts and fixedLegBPS; and fairRate where both legs pay on the same day. Where they pay on
  different days, QuantLib's fair rate doesn't zero the swap, which nanoql fixes
  (tests/NanoQL.Tests/Divergences/ZeroCouponInflationSwapFairRateTests.cs). An explicit inflation convention is never
  Following unless the fixed one is: QuantLib takes Following for its default and replaces it
  (tests/NanoQL.Tests/Divergences/ZeroCouponInflationSwapConventionTests.cs).
- "construction" cases: an observation lag inconsistent with the index's availability lag, flat or linear: refused.

A refused value is {"throws": true}. A case's inputs hold its settings, the index spec and fixings
(tools/vectors/gen/cashflows/indexedcashflow.py's), the nominal curve and the swap's arguments, a calendar as
tools/vectors/gen/cashflows/cashflows.py's or null. Read back by
tests/NanoQL.Tests/Vectors/Instruments/ZeroCouponInflationSwapVectorTests.cs."""
import importlib.util
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

TODAY = icf.TODAY
UK = cf.UK_SETTLEMENT
M = int(ql.Months)
PAYER, RECEIVER = int(ql.Swap.Payer), int(ql.Swap.Receiver)

# (label, type, nominal, start, maturity, fixed calendar, fixed convention, dc, rate, lag months, interpolation,
#  inflation calendar, inflation convention)
SWAPS = [
    ("payer, flat", PAYER, 1e6, TODAY, ql.Date(15, ql.June, 2029), UK, ql.ModifiedFollowing, cf.THIRTY360_BB, 0.032,
     3, ql.CPI.Flat, None, None),
    ("receiver, linear", RECEIVER, 5e6, TODAY, ql.Date(17, ql.June, 2034), UK, ql.ModifiedFollowing, cf.A365, 0.0315,
     3, ql.CPI.Linear, None, None),
    ("payer, past start", PAYER, 1e6, ql.Date(15, ql.March, 2023), ql.Date(15, ql.March, 2028), UK, ql.Following,
     cf.ACT_ISDA, 0.035, 2, ql.CPI.Flat, None, None),
    ("payer, Saturday maturity", PAYER, 1e6, TODAY, ql.Date(31, ql.May, 2025), UK, ql.ModifiedFollowing, cf.A365, 0.03,
     3, ql.CPI.Flat, None, None),
    ("inflation leg on TARGET, Preceding", PAYER, 1e6, TODAY, ql.Date(31, ql.May, 2025), UK, ql.Following, cf.A365,
     0.03, 3, ql.CPI.Flat, cf.TARGET, ql.Preceding),
    ("inflation leg Following, as the fixed", RECEIVER, 2e6, TODAY, ql.Date(30, ql.November, 2030), UK, ql.Following,
     cf.A365, 0.029, 3, ql.CPI.Linear, UK, ql.Following),
    ("inflation calendar alone", PAYER, 1e6, TODAY, ql.Date(1, ql.January, 2028), UK, ql.ModifiedFollowing, cf.A365,
     0.031, 3, ql.CPI.Flat, cf.TARGET, None),
    ("negative rate", RECEIVER, 1e6, TODAY, ql.Date(15, ql.June, 2027), UK, ql.ModifiedFollowing, cf.A365, -0.01, 3,
     ql.CPI.Flat, None, None),
]

# (label, lag, interpolation): monthly UK RPI with a one-month availability lag
REFUSED = [("flat, lag shorter than the availability", ql.Period(0, ql.Months), ql.CPI.Flat),
           ("linear, lag less the period shorter than the availability", ql.Period(1, ql.Months), ql.CPI.Linear)]


def make_swap(inputs, index):
    args = [inputs["type"], inputs["nominal"], ql.Date(inputs["startDate"]), ql.Date(inputs["maturity"]),
            cf.calendar(inputs["fixCalendar"]), inputs["fixConvention"], cf.day_counter(inputs["dayCounter"]),
            inputs["fixedRate"], index, ql.Period(inputs["observationLag"][0], inputs["observationLag"][1]),
            inputs["interpolation"], False]
    if inputs["infCalendar"] is not None or inputs["infConvention"] is not None:
        args.append(cf.calendar(inputs["infCalendar"]) if inputs["infCalendar"] is not None
                    else cf.calendar(inputs["fixCalendar"]))
    if inputs["infConvention"] is not None:
        args.append(inputs["infConvention"])
    return ql.ZeroCouponInflationSwap(*args)


def swap_cases():
    out = []
    for k, (label, t, nominal, start, maturity, fix_cal, fix_conv, dc, rate, lag, interp, inf_cal,
            inf_conv) in enumerate(SWAPS):
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(icf.UKRPI, icf.UK_FIXINGS)
            inputs = {"settings": st, "kind": "swap", "index": icf.UKRPI, "fixings": icf.UK_FIXINGS,
                      "nominalCurve": icf.NOMINAL, "type": t, "nominal": nominal, "startDate": v.enc(start),
                      "maturity": v.enc(maturity), "fixCalendar": fix_cal, "fixConvention": int(fix_conv),
                      "dayCounter": dc, "fixedRate": rate, "observationLag": [lag, M], "interpolation": int(interp),
                      "infCalendar": inf_cal, "infConvention": None if inf_conv is None else int(inf_conv)}
            swap = make_swap(inputs, index)
            swap.setPricingEngine(ql.DiscountingSwapEngine(icf.nominal_curve(icf.NOMINAL)))
            fixed = swap.fixedLeg()[0]
            inflation = ql.as_indexed_cashflow(swap.inflationLeg()[0]) if hasattr(ql, "as_indexed_cashflow") else None
            infl = swap.inflationLeg()[0]
            expected = {
                "startDate": v.enc(swap.startDate()), "maturityDate": v.enc(swap.maturityDate()),
                "fixedDate": v.enc(fixed.date()), "fixedAmount": v.attempt(fixed.amount),
                "inflationDate": v.enc(infl.date()), "inflationAmount": v.attempt(infl.amount),
                "NPV": v.attempt(swap.NPV), "legNPV": [v.attempt(lambda j=j: swap.legNPV(j)) for j in (0, 1)],
                "fixedLegNPV": v.attempt(swap.fixedLegNPV), "inflationLegNPV": v.attempt(swap.inflationLegNPV),
                "endDiscounts": [v.result(lambda j=j: swap.endDiscounts(j)) for j in (0, 1)],
                "fixedLegBPS": v.attempt(swap.fixedLegBPS)}
            if inflation is not None:
                expected["inflationBaseDate"] = v.enc(inflation.baseDate())
                expected["inflationFixingDate"] = v.enc(inflation.fixingDate())
            if fixed.date() == infl.date():
                expected["fairRate"] = v.attempt(swap.fairRate)
            out.append({"name": f"swap {label} / {k}", "inputs": inputs, "expected": expected})
    return out


def construction_cases():
    out = []
    for k, (label, lag, interp) in enumerate(REFUSED):
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(icf.UKRPI, icf.UK_FIXINGS)
            inputs = {"settings": st, "kind": "construction", "index": icf.UKRPI, "fixings": icf.UK_FIXINGS,
                      "nominalCurve": icf.NOMINAL, "type": PAYER, "nominal": 1e6, "startDate": v.enc(TODAY),
                      "maturity": v.enc(ql.Date(15, ql.June, 2029)), "fixCalendar": UK,
                      "fixConvention": int(ql.ModifiedFollowing), "dayCounter": cf.A365, "fixedRate": 0.03,
                      "observationLag": icf.period(lag), "interpolation": int(interp), "infCalendar": None,
                      "infConvention": None}
            out.append({"name": f"construction {label} / {k}", "inputs": inputs,
                        "expected": {"construction": v.attempt(lambda: make_swap(inputs, index) and 0)}})
    return out


def make_cases():
    return swap_cases() + construction_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/zerocouponinflationswap.hpp", make_cases)
