# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CmsLeg and its coupons (ql/cashflows/cmscoupon.hpp, capflooredcoupon.hpp), the legs' structure
only: their rates wait for batch 3's CMS coupon pricers (S-cms), so each rate and amount is QuantLib's refusal "pricer
not set". Legs on 10Y and 2Y EUR swap indexes over Euribor 6M and 3M, on the evaluation date 15 January 2025: plain,
with gearings and spreads per coupon, a single gearing, a payment day counter, convention and fixing days, caps and
floors (capped and floored CMS coupons), in arrears, amortizing nominals, a zero gearing, which QuantLib's leg builds as
a fixed coupon at the spread, whose rate and amount are given, floors and fixing days per coupon, ex-coupon dates with a
fixing convention, and zero payments (the wheel's CmsZeroLeg, QuantLib's CmsLeg with withZeroPayments).

Each case's outputs are, per coupon, whether it is a floating coupon, its payment, accrual, reference-period and
ex-coupon dates, its nominal, its rate and amount (refused, or the fixed coupon's), and for a floating coupon its fixing
date, fixing days, gearing, spread and whether it fixes in arrears, null for a fixed coupon; amounts compared at the
largest nominal. The wheel binds no cast to a capped coupon, so the caps and floors aren't read: their cases check the
rest of the legs, and tests/NanoQL.Tests/Conventions/CmsPricerRefusalTests.cs that a capped leg is built of capped CMS
coupons.

Each case's inputs hold its settings and "leg" ({"schedule", "index", "nominals", "paymentDayCounter",
"paymentConvention", "fixingDays", "gearings", "spreads", "caps", "floors", "inArrears", "zeroPayments", "exCoupon",
"fixingConvention"}), "exCoupon" null or {"period", "calendar", "convention", "endOfMonth"}: the schedule a spec of
instruments/fixedvsfloatingswap.py, the index one of indexes/swapindex.py, empty lists QuantLib's defaults; the day
counter is always given, the wheel building no empty one. The wheel's CmsLeg is QuantLib-SWIG's function building
QuantLib's CmsLeg with those setters. Read back by tests/NanoQL.Tests/Vectors/Coupons/CmsCouponVectorTests.cs."""
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


fvf = _load("wheel_fixedvsfloatingswap", "instruments/fixedvsfloatingswap.py")
swi = _load("wheel_swapindex", "indexes/swapindex.py")
irx = fvf.irx

TODAY = fvf.TODAY
D = fvf.d
M, Y = ql.Months, ql.Years
MF, F, P = ql.ModifiedFollowing, ql.Following, ql.Preceding
CURVE = fvf.curve(0.031)
CMS10Y = swi.swap_index("EurSwap", (10, Y), 2, "EUR", irx.TARGET, (1, Y), MF, irx.THIRTY360_BB,
                        fvf.preset("Euribor6M", CURVE))
CMS2Y = swi.swap_index("EurSwap", (2, Y), 2, "EUR", irx.TARGET, (1, Y), MF, irx.THIRTY360_BB,
                       fvf.preset("Euribor3M", CURVE))


def leg(schedule, index, nominals, dc=irx.A365, convention=F, fixing_days=(), gearings=(), spreads=(), caps=(),
        floors=(), in_arrears=False, zero_payments=False, ex_coupon=None, fixing_convention=P):
    return {"schedule": schedule, "index": index, "nominals": list(nominals), "paymentDayCounter": dc,
            "paymentConvention": int(convention), "fixingDays": list(fixing_days), "gearings": list(gearings),
            "spreads": list(spreads), "caps": list(caps), "floors": list(floors), "inArrears": in_arrears,
            "zeroPayments": zero_payments, "exCoupon": ex_coupon, "fixingConvention": int(fixing_convention)}


def ex_coupon(days, calendar, convention, eom):
    return {"period": [days, int(ql.Days)], "calendar": calendar, "convention": int(convention), "endOfMonth": eom}


def make_leg(spec):
    ex = spec["exCoupon"]
    ex_args = ((ql.Period(), ql.TARGET(), ql.Unadjusted, False) if ex is None else
               (ql.Period(*ex["period"]), irx.calendar(ex["calendar"]), ex["convention"], ex["endOfMonth"]))
    args = (spec["nominals"], fvf.schedule(spec["schedule"]), swi.make_swap_index(spec["index"]),
            irx.day_counter(spec["paymentDayCounter"]), spec["paymentConvention"], spec["fixingDays"],
            spec["gearings"], spec["spreads"], spec["caps"], spec["floors"])
    if spec["zeroPayments"]:
        return ql.CmsZeroLeg(*args, *ex_args)
    return ql.CmsLeg(*args, spec["inArrears"], *ex_args, spec["fixingConvention"])


def legs():
    two6 = fvf.sched(D(17, 1, 2025), D(17, 1, 2027), ql.Period(6, M), irx.TARGET)
    five1 = fvf.sched(D(17, 1, 2025), D(17, 1, 2030), ql.Period(1, Y), irx.TARGET)
    stub = fvf.sched(D(17, 1, 2025), D(15, 9, 2026), ql.Period(6, M), irx.TARGET)
    return [
        ("plain, 10Y", leg(two6, CMS10Y, [100.0])),
        ("gearings and spreads", leg(two6, CMS10Y, [1e6], gearings=[1.0, 0.9, 0.8, 0.7],
                                     spreads=[0.001, 0.002, 0.003, 0.004])),
        ("day counter, Preceding, 3 fixing days", leg(five1, CMS2Y, [100.0], irx.A360, P, [3])),
        ("caps and floors", leg(two6, CMS10Y, [100.0], caps=[0.05], floors=[0.01])),
        ("caps only, per coupon", leg(five1, CMS10Y, [100.0], caps=[0.04, 0.045, 0.05, 0.055, 0.06])),
        ("in arrears", leg(two6, CMS2Y, [100.0], in_arrears=True)),
        ("amortizing, a short front stub", leg(stub, CMS10Y, [100.0, 75.0, 50.0, 25.0])),
        ("a zero gearing", leg(two6, CMS10Y, [100.0], gearings=[0.0, 1.0], spreads=[0.01])),
        ("a single gearing", leg(two6, CMS10Y, [100.0], gearings=[0.8])),
        ("a single spread, floors and fixing days per coupon",
         leg(two6, CMS10Y, [100.0], fixing_days=[2, 3], spreads=[0.001], floors=[0.01, 0.015])),
        ("ex-coupon 7 days, Modified Following fixings on an unadjusted schedule from a Saturday",
         leg(fvf.sched(D(18, 1, 2025), D(18, 1, 2027), ql.Period(6, M), irx.TARGET, ql.Unadjusted), CMS2Y, [100.0],
             fixing_days=[0], ex_coupon=ex_coupon(7, irx.TARGET, F, False), fixing_convention=MF)),
        ("zero payments", leg(five1, CMS10Y, [100.0, 80.0], ex_coupon=ex_coupon(2, irx.TARGET, P, False),
                              zero_payments=True)),
    ]


def coupon_outputs(cashflows, scale):
    out = {k: [] for k in ["floating", "dates", "accrualStartDates", "accrualEndDates", "referenceStarts",
                           "referenceEnds", "exCouponDates", "nominals", "rates", "amounts", "fixingDates",
                           "fixingDays", "gearings", "spreads", "inArrears"]}
    for cf in cashflows:
        c = ql.as_coupon(cf)
        f = ql.as_floating_rate_coupon(cf)
        out["floating"].append(f is not None)
        out["dates"].append(v.enc(c.date()))
        out["accrualStartDates"].append(v.enc(c.accrualStartDate()))
        out["accrualEndDates"].append(v.enc(c.accrualEndDate()))
        out["referenceStarts"].append(v.enc(c.referencePeriodStart()))
        out["referenceEnds"].append(v.enc(c.referencePeriodEnd()))
        out["exCouponDates"].append(v.enc(c.exCouponDate()))
        out["nominals"].append(c.nominal())
        out["rates"].append(v.attempt(c.rate))
        out["amounts"].append(fvf.ibc.scaled(c.amount, scale))
        out["fixingDates"].append(v.enc(f.fixingDate()) if f is not None else None)
        out["fixingDays"].append(f.fixingDays() if f is not None else None)
        out["gearings"].append(f.gearing() if f is not None else None)
        out["spreads"].append(f.spread() if f is not None else None)
        out["inArrears"].append(f.isInArrears() if f is not None else None)
    return out


def cases():
    out = []
    for name, spec in legs():
        with v.case_settings(TODAY) as st:
            cashflows = make_leg(spec)
            out.append({"name": name, "inputs": {"settings": st, "leg": spec},
                        "expected": coupon_outputs(cashflows, max(abs(x) for x in spec["nominals"]))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/cmscoupon.hpp", cases)
