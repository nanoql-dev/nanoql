# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CPI swap (ql/instruments/cpiswap.hpp), through the wheel's CPISwap priced by the
discounting swap engine on a flat nominal curve.

- "swap" cases: payer and receiver swaps of a CPI leg, with an explicit base CPI or none (observed flat), against an
  Ibor leg with a spread, the inflation nominal subtracted or not, equal to the nominal or not, and a floating schedule
  of a single date: each leg's cash flows' dates and amounts; NPV, legNPV, legBPS, fixedLegNPV, floatLegNPV and
  fairSpread, derived from the floating leg's BPS as QuantLib derives it. The fair rate isn't sampled: QuantLib derives
  it from the CPI leg's BPS, which leaves out the index ratio, and nanoql's engine solves it
  (tests/NanoQL.Tests/Divergences/CPISwapFairRateTests.cs).

A refused value is {"throws": true}. A case's inputs hold its settings, the index spec and fixings
(tools/vectors/gen/cashflows/indexedcashflow.py's), the nominal curve, the floating index (tools/vectors/gen/indexes/
interestrateindex.py's spec, forecasting on its own flat curve) and the swap's arguments, schedules as
tools/vectors/gen/cashflows/cashflows.py's or {"dates", "calendar", "convention"}. Read back by
tests/NanoQL.Tests/Vectors/Instruments/CPISwapVectorTests.cs."""
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
cpi = _load("wheel_cpicoupon", "cashflows", "cpicoupon.py")
cf = icf.cf
irx = icf.irx

TODAY = icf.TODAY
UK = cf.UK_SETTLEMENT
M = int(ql.Months)
PAYER, RECEIVER = int(ql.Swap.Payer), int(ql.Swap.Receiver)
GBP_IBOR = irx.ibor_spec("GbpIbor", (6, ql.Months), 0, UK, ql.ModifiedFollowing, False, cf.A365,
                         irx.curve_spec(TODAY, 0.045))


def semiannual(end):
    return cf.schedule_spec(TODAY, end, ql.Period(6, ql.Months), UK, ql.ModifiedFollowing,
                            ql.DateGeneration.Backward, False)


def annual(end):
    return cf.schedule_spec(TODAY, end, ql.Period(1, ql.Years), UK, ql.Unadjusted, ql.DateGeneration.Backward, False)


def one_date(d):
    return {"dates": [v.enc(d)], "calendar": UK, "convention": int(ql.ModifiedFollowing)}


END = ql.Date(15, ql.June, 2029)

# (label, type, nominal, subtract, spread, float dc, float schedule, float roll, fixing days, fixed rate, base CPI,
#  fixed dc, fixed schedule, fixed roll, lag months, interpolation, inflation nominal)
SWAPS = [
    ("payer, base CPI, subtracted", PAYER, 1e6, True, 0.001, cf.A365, semiannual(END), ql.ModifiedFollowing, 0, 0.02,
     306.1, cf.A365, annual(END), ql.ModifiedFollowing, 3, ql.CPI.Flat, None),
    ("receiver, base CPI, linear, not subtracted", RECEIVER, 1e6, False, 0.0, cf.A365, semiannual(END),
     ql.ModifiedFollowing, 0, 0.0175, 306.1, cf.THIRTY360_BB, annual(END), ql.Following, 3, ql.CPI.Linear, None),
    ("payer, no base, flat", PAYER, 2e6, True, -0.0005, cf.A365, semiannual(END), ql.ModifiedFollowing, 0, 0.015,
     None, cf.A365, annual(END), ql.ModifiedFollowing, 3, ql.CPI.Flat, None),
    ("payer, inflation nominal differs", PAYER, 1e6, True, 0.0, cf.A365, semiannual(END), ql.ModifiedFollowing, 0,
     0.02, 306.1, cf.A365, annual(END), ql.ModifiedFollowing, 3, ql.CPI.Flat, 1.2e6),
    ("receiver, floating schedule of one date", RECEIVER, 1.4e6, True, 0.0, cf.A365, one_date(END),
     ql.ModifiedFollowing, 0, 0.0, 306.1, cf.A365, one_date(END), ql.ModifiedFollowing, 3, ql.CPI.Flat, 1e6),
    ("payer, Actual/360 floating leg, two-month lag", PAYER, 1e6, True, 0.0, cf.A360, semiannual(END), ql.Following,
     0, 0.02, 306.1, cf.A365, annual(END), ql.ModifiedFollowing, 2, ql.CPI.Flat, None),
]


def make_swap(inputs, index):
    float_index = irx.make_index(inputs["floatIndex"]) if inputs["floatIndex"] is not None else ql.IborIndex(
        "dummy", ql.Period(6, ql.Months), 0, ql.GBPCurrency(), ql.UnitedKingdom(), ql.ModifiedFollowing, False,
        ql.Actual365Fixed())
    return ql.CPISwap(inputs["type"], inputs["nominal"], inputs["subtractInflationNominal"], inputs["spread"],
                      cf.day_counter(inputs["floatDayCount"]), cpi.make_schedule(inputs["floatSchedule"]),
                      inputs["floatRoll"], inputs["fixingDays"], float_index, inputs["fixedRate"],
                      cpi.null_real(inputs["baseCPI"]), cf.day_counter(inputs["fixedDayCount"]),
                      cpi.make_schedule(inputs["fixedSchedule"]), inputs["fixedRoll"],
                      ql.Period(inputs["observationLag"][0], inputs["observationLag"][1]), index,
                      inputs["interpolation"], cpi.null_real(inputs["inflationNominal"]))


def swap_cases():
    out = []
    for k, (label, t, nominal, subtract, spread, float_dc, float_schedule, float_roll, days, rate, base, fixed_dc,
            fixed_schedule, fixed_roll, lag, interp, inflation_nominal) in enumerate(SWAPS):
        with v.case_settings(TODAY, use_at_par_coupons=True) as st:
            index = icf.index_with_fixings(icf.UKRPI, icf.UK_FIXINGS)
            single = "dates" in float_schedule
            inputs = {"settings": st, "index": icf.UKRPI, "fixings": icf.UK_FIXINGS, "nominalCurve": icf.NOMINAL,
                      "type": t, "nominal": nominal, "subtractInflationNominal": subtract, "spread": spread,
                      "floatDayCount": float_dc, "floatSchedule": float_schedule, "floatRoll": int(float_roll),
                      "fixingDays": days, "floatIndex": None if single else GBP_IBOR, "fixedRate": rate,
                      "baseCPI": base, "fixedDayCount": fixed_dc, "fixedSchedule": fixed_schedule,
                      "fixedRoll": int(fixed_roll), "observationLag": [lag, M], "interpolation": int(interp),
                      "inflationNominal": inflation_nominal}
            swap = make_swap(inputs, index)
            swap.setPricingEngine(ql.DiscountingSwapEngine(icf.nominal_curve(icf.NOMINAL)))
            out.append({"name": f"swap {label} / {k}", "inputs": inputs,
                        "expected": {
                            "cpiDates": v.enc([f.date() for f in swap.cpiLeg()]),
                            "cpiAmounts": [v.attempt(f.amount) for f in swap.cpiLeg()],
                            "floatDates": v.enc([f.date() for f in swap.floatLeg()]),
                            "floatAmounts": [v.attempt(f.amount) for f in swap.floatLeg()],
                            "NPV": v.attempt(swap.NPV),
                            "legNPV": [v.result(lambda j=j: swap.legNPV(j)) for j in (0, 1)],
                            "legBPS": [v.result(lambda j=j: swap.legBPS(j)) for j in (0, 1)],
                            "fixedLegNPV": v.result(swap.fixedLegNPV), "floatLegNPV": v.result(swap.floatLegNPV),
                            "fairSpread": v.result(swap.fairSpread)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/cpiswap.hpp", swap_cases)
