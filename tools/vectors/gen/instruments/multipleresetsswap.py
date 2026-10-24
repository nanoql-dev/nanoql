# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's multiple-resets swap (ql/instruments/multipleresetsswap.hpp), through the wheel's
MultipleResetsSwap and DiscountingSwapEngine, on the evaluation date 15 January 2025, Euribor 1M and 3M indexes on a
flat 3.1% curve with the stored past fixings of cashflows/iborcoupon.py, and instruments/fixedvsfloatingswap.py's flat
discounting curve.

Each "swap" case is a payer or a receiver, its floating coupons compounding or averaging 2, 3, 4 or 6 resets, forward
starting or seasoned, with a spread, a payment convention, lag and calendar: the base's inspectors (as
instruments/fixedvsfloatingswap.py writes them) with the full reset schedule's dates, the resets per coupon and the
averaging method; each leg's payment dates, nominals, rates and amounts; and the engine's results (NPV, the per-leg NPV
and BPS, the fair rate and the four leg accessors). The fair spread is recorded on the averaging legs only: on a
compounding leg QuantLib's is linear in the leg's BPS, where the spread compounds, and misses par, the S-cms plan's CMR5
(xv) (tests/NanoQL.Tests/Divergences/MultipleResetsFairSpreadTests.cs). NPVs are compared at the nominal and BPS at it
times 1e-4.

A case's inputs hold its settings with the stored fixings, the discounting curve and "swap" ({"type", "nominal",
"fixedSchedule", "fixedRate", "fixedDayCount", "fullResetSchedule", "index", "resetsPerCoupon", "spread", "averaging",
"paymentConvention", "paymentLag", "paymentCalendar"}, a null payment convention QuantLib's std::nullopt and a null
calendar none), a schedule as cashflows/cashflows.py's. Read back by
tests/NanoQL.Tests/Vectors/Instruments/MultipleResetsSwapVectorTests.cs."""
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
fvf = _load("wheel_fixedvsfloatingswap", "instruments/fixedvsfloatingswap.py")

TODAY = irx.TODAY
D = ql.Date
TARGET, UK = cf.TARGET, cf.UK_SETTLEMENT
A360, A365, THIRTY360_BB = cf.A360, cf.A365, cf.THIRTY360_BB
E1M, E3M = ibc.E1M, ibc.E3M
DISCOUNT = fvf.DISCOUNT
PAYER, RECEIVER = int(ql.Swap.Payer), int(ql.Swap.Receiver)
AVERAGING = {"compounding": ql.RateAveraging.Compound, "averaging": ql.RateAveraging.Simple}


def sched(start, end, months, convention=ql.ModifiedFollowing):
    return cf.schedule_spec(start, end, ql.Period(months, ql.Months), TARGET, convention, ql.DateGeneration.Forward,
                            False)


def swap_spec(swap_type, nominal, fixed_schedule, fixed_rate, fixed_dc, resets, index, per_coupon, spread=0.0,
              averaging="compounding", convention=None, lag=0, calendar=None):
    return {"type": swap_type, "nominal": nominal, "fixedSchedule": fixed_schedule, "fixedRate": fixed_rate,
            "fixedDayCount": fixed_dc, "fullResetSchedule": resets, "index": index, "resetsPerCoupon": per_coupon,
            "spread": spread, "averaging": averaging,
            "paymentConvention": int(convention) if convention is not None else None, "paymentLag": lag,
            "paymentCalendar": calendar}


START, END = D(17, ql.March, 2025), D(17, ql.March, 2030)
SEASONED_START, SEASONED_END = D(16, ql.April, 2024), D(16, ql.April, 2027)

# (label, spec)
SWAPS = [
    ("5Y, monthly resets compounded quarterly, payer",
     swap_spec(PAYER, 1e7, sched(START, END, 12), 0.031, THIRTY360_BB, sched(START, END, 1), E1M, 3)),
    ("5Y, monthly resets averaged quarterly, receiver, a spread",
     swap_spec(RECEIVER, 1e7, sched(START, END, 12), 0.03, THIRTY360_BB, sched(START, END, 1), E1M, 3, 0.001,
               "averaging")),
    ("5Y, monthly resets compounded semiannually, a spread",
     swap_spec(RECEIVER, 2.5e7, sched(START, END, 6), 0.0315, A360, sched(START, END, 1), E1M, 6, 0.0005)),
    ("seasoned, quarterly resets compounded annually",
     swap_spec(PAYER, 1e7, sched(SEASONED_START, SEASONED_END, 12), 0.033, A365, sched(SEASONED_START, SEASONED_END, 3),
               E3M, 4)),
    ("seasoned, quarterly resets averaged semiannually",
     swap_spec(RECEIVER, 1e7, sched(SEASONED_START, SEASONED_END, 12), 0.033, A365,
               sched(SEASONED_START, SEASONED_END, 3), E3M, 2, 0.0, "averaging")),
    ("a payment convention, lag and calendar",
     swap_spec(PAYER, 1e7, sched(START, END, 12), 0.031, THIRTY360_BB, sched(START, END, 3), E3M, 2, 0.0,
               "compounding", ql.Preceding, 2, UK)),
    ("a payment lag, averaging",
     swap_spec(RECEIVER, 1e7, sched(START, END, 12), 0.031, THIRTY360_BB, sched(START, END, 3), E3M, 4, -0.0005,
               "averaging", None, 1, None)),
]


def make_swap(spec):
    index = irx.make_index(spec["index"])
    args = [spec["type"], spec["nominal"], cf.schedule(spec["fixedSchedule"]), spec["fixedRate"],
            cf.day_counter(spec["fixedDayCount"]), cf.schedule(spec["fullResetSchedule"]), index,
            spec["resetsPerCoupon"], spec["spread"], AVERAGING[spec["averaging"]]]
    # the wheel builds no empty calendar: the payment settings go by keyword, an absent calendar left out
    kw = {"paymentLag": spec["paymentLag"]}
    if spec["paymentConvention"] is not None:
        kw["paymentConvention"] = spec["paymentConvention"]
    if spec["paymentCalendar"] is not None:
        kw["paymentCalendar"] = cf.calendar(spec["paymentCalendar"])
    return ql.MultipleResetsSwap(*args, **kw)


def cases():
    out = []
    for k, (label, spec) in enumerate(SWAPS):
        with v.case_settings(TODAY, fixings=ibc.fixings_for(spec["index"])) as st:
            sw = make_swap(spec)
            sw.setPricingEngine(fvf.engine(DISCOUNT))
            scale = spec["nominal"]
            expected = fvf.inspectors(sw)
            expected.update({"fullResetSchedule": fvf.schedule_dates(sw.fullResetSchedule()),
                             "resetsPerCoupon": sw.resetsPerCoupon(), "averagingMethod": int(sw.averagingMethod())})
            expected.update(fvf.leg_outputs("fixedLeg", sw.fixedLeg(), scale, full=True))
            expected.update(fvf.leg_outputs("floatingLeg", sw.floatingLeg(), scale, full=True))
            results = fvf.results(sw, scale)
            if spec["averaging"] == "compounding":
                del results["fairSpread"]
            expected.update(results)
            out.append({"name": f"swap {label} / {k}",
                        "inputs": {"settings": st, "discountCurve": DISCOUNT, "swap": spec},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/multipleresetsswap.hpp", cases)
