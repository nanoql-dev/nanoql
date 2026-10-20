# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year inflation swap (ql/instruments/yearonyearinflationswap.hpp), through the
wheel's YearOnYearInflationSwap priced by the discounting swap engine on a flat nominal curve.

- "swap" cases: payer and receiver swaps on a ratio or a quoted index, observed flat or linearly, on annual or
  semiannual schedules, from today or a past start, with spreads: each leg's cash flows' dates and amounts; NPV,
  legNPV, legBPS, startDiscounts, endDiscounts, npvDateDiscount, fixedLegNPV, yoyLegNPV, and fairRate and fairSpread,
  derived from the legs' BPS as QuantLib derives them. A linear swap on a ratio index observes its coupons where
  QuantLib's test of its ratio branch and nanoql's agree (tests/NanoQL.Tests/Divergences/LaggedYoYRateBranchTests.cs).

A refused value is {"throws": true}. A case's inputs hold its settings, the index spec and fixings
(tools/vectors/gen/cashflows/indexedcashflow.py's), the nominal curve and the swap's arguments, schedules as
tools/vectors/gen/cashflows/cashflows.py's. Read back by
tests/NanoQL.Tests/Vectors/Instruments/YearOnYearInflationSwapVectorTests.cs."""
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
ii = icf.ii

TODAY = icf.TODAY
UK = cf.UK_SETTLEMENT
M = int(ql.Months)
PAYER, RECEIVER = int(ql.Swap.Payer), int(ql.Swap.Receiver)
INDEXES = {"ratio": (icf.YYUKRPI_RATIO, icf.UK_FIXINGS), "quoted": (ii.preset("YYUKRPI", icf.YOY_UK), ii.YY_FIXINGS)}


def annual(start, end):
    return cf.schedule_spec(start, end, ql.Period(1, ql.Years), UK, ql.Unadjusted, ql.DateGeneration.Backward, False)


SEMIANNUAL = cf.schedule_spec(TODAY, ql.Date(15, ql.June, 2029), ql.Period(6, ql.Months), UK, ql.ModifiedFollowing,
                              ql.DateGeneration.Backward, False)

# (label, index, type, nominal, fixed schedule, rate, fixed dc, yoy schedule, lag months, interpolation, spread,
#  yoy dc, payment convention)
SWAPS = [
    ("payer, ratio, flat", "ratio", PAYER, 1e6, annual(TODAY, ql.Date(15, ql.June, 2029)), 0.03, cf.THIRTY360_BB,
     annual(TODAY, ql.Date(15, ql.June, 2029)), 2, ql.CPI.Flat, 0.0, cf.THIRTY360_BB, ql.ModifiedFollowing),
    ("receiver, ratio, linear, spread", "ratio", RECEIVER, 5e6, annual(TODAY, ql.Date(15, ql.June, 2034)), 0.0285,
     cf.A365, annual(TODAY, ql.Date(15, ql.June, 2034)), 3, ql.CPI.Linear, 0.0015, cf.A365, ql.Following),
    ("payer, quoted, semiannual", "quoted", PAYER, 1e6, annual(TODAY, ql.Date(15, ql.June, 2029)), 0.029, cf.A365,
     SEMIANNUAL, 2, ql.CPI.Flat, 0.0, cf.A365, ql.ModifiedFollowing),
    ("payer, ratio, past start", "ratio", PAYER, 1e6, annual(ql.Date(15, ql.March, 2023), ql.Date(15, ql.March, 2028)),
     0.031, cf.THIRTY360_BB, annual(ql.Date(15, ql.March, 2023), ql.Date(15, ql.March, 2028)), 2, ql.CPI.Flat,
     -0.001, cf.THIRTY360_BB, ql.ModifiedFollowing),
    ("receiver, zero nominal", "ratio", RECEIVER, 0.0, annual(TODAY, ql.Date(15, ql.June, 2027)), 0.03, cf.A365,
     annual(TODAY, ql.Date(15, ql.June, 2027)), 2, ql.CPI.Flat, 0.0, cf.A365, ql.ModifiedFollowing),
]


def make_swap(inputs, index):
    return ql.YearOnYearInflationSwap(
        inputs["type"], inputs["nominal"], cf.schedule(inputs["fixedSchedule"]), inputs["fixedRate"],
        cf.day_counter(inputs["fixedDayCount"]), cf.schedule(inputs["yoySchedule"]), index,
        ql.Period(inputs["observationLag"][0], inputs["observationLag"][1]), inputs["interpolation"],
        inputs["spread"], cf.day_counter(inputs["yoyDayCount"]), cf.calendar(inputs["paymentCalendar"]),
        inputs["paymentConvention"])


def swap_cases():
    out = []
    for k, (label, kind, t, nominal, fixed_schedule, rate, fixed_dc, yoy_schedule, lag, interp, spread, yoy_dc,
            convention) in enumerate(SWAPS):
        spec, fixings = INDEXES[kind]
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(spec, fixings)
            inputs = {"settings": st, "index": spec, "fixings": fixings, "nominalCurve": icf.NOMINAL, "type": t,
                      "nominal": nominal, "fixedSchedule": fixed_schedule, "fixedRate": rate,
                      "fixedDayCount": fixed_dc, "yoySchedule": yoy_schedule, "observationLag": [lag, M],
                      "interpolation": int(interp), "spread": spread, "yoyDayCount": yoy_dc,
                      "paymentCalendar": UK, "paymentConvention": int(convention)}
            swap = make_swap(inputs, index)
            swap.setPricingEngine(ql.DiscountingSwapEngine(icf.nominal_curve(icf.NOMINAL)))
            out.append({"name": f"swap {label} / {k}", "inputs": inputs,
                        "expected": {
                            "fixedDates": v.enc([f.date() for f in swap.fixedLeg()]),
                            "fixedAmounts": [v.attempt(f.amount) for f in swap.fixedLeg()],
                            "yoyDates": v.enc([f.date() for f in swap.yoyLeg()]),
                            "yoyAmounts": [v.attempt(f.amount) for f in swap.yoyLeg()],
                            "NPV": v.attempt(swap.NPV),
                            "legNPV": [v.attempt(lambda j=j: swap.legNPV(j)) for j in (0, 1)],
                            "legBPS": [v.attempt(lambda j=j: swap.legBPS(j)) for j in (0, 1)],
                            "startDiscounts": [v.result(lambda j=j: swap.startDiscounts(j)) for j in (0, 1)],
                            "endDiscounts": [v.result(lambda j=j: swap.endDiscounts(j)) for j in (0, 1)],
                            "npvDateDiscount": v.result(swap.npvDateDiscount),
                            "fixedLegNPV": v.result(swap.fixedLegNPV), "yoyLegNPV": v.result(swap.yoyLegNPV),
                            "fairRate": v.result(swap.fairRate), "fairSpread": v.result(swap.fairSpread)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/yearonyearinflationswap.hpp", swap_cases)
