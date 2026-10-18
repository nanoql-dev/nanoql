# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FloatingRateCoupon (ql/cashflows/floatingratecoupon.hpp) built directly, which the wheel
can't build, each output computed by the C++ harness, on the evaluation date 15 January 2025: 16 coupons on Ibor and
overnight indexes (the specs of tools/vectors/gen/indexes/interestrateindex.py), in advance and in arrears, with
gearings, spreads, 0 to 3 and null fixing days, an accrual day counter or none (the index's), reference periods,
ex-coupon dates and fixing conventions, fixing before (fixings stored) and after the evaluation date, priced by the
harness's IndexFixingPricer (the geared index fixing shifted by an adjustment, plus the spread, so that the
convexity adjustment is the adjustment); and four that fail: one with a BlackIborCouponPricer, which QuantLib refuses
for a coupon that isn't an IborCoupon ("IborCouponPricer: expected IborCoupon"), one with no pricer ("pricer not
set"), one whose past fixing is missing, and one with a zero gearing, which the constructor refuses.

Each case's outputs are the constructor's inspectors (payment date, nominal, accrual and reference-period dates,
ex-coupon date, accrual period, fixing date, fixing days, gearing, spread, in-arrears flag, fixing convention, day
counter and index names, and whether a pricer is set), then rate, amount, indexFixing, adjustedFixing,
convexityAdjustment, price on a flat 2.5% discount curve, and accruedAmount at three dates (a refused constructor
records only "coupon": {"throws": true}). A refused value is {"throws": true}; amounts are compared at the coupon's
nominal. Each case's inputs hold the coupon spec ("coupon", as tools/vectors/gen/cashflows/iborcoupon.py's functions
build it, with the type "FloatingRateCoupon"), the discount curve ("discountCurve") and the accrued-amount dates
("accrualDates"). tests/NanoQL.Tests/Vectors/Cxx/Coupons/FloatingRateCouponCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_iborcoupon", os.path.join(v.GEN_ROOT, "cashflows", "iborcoupon.py"))
ibc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ibc)

irx = ibc.irx
TODAY = ibc.TODAY
d = ibc.d

E3M = ibc.euribor((3, ql.Months), ibc.CURVE)
E6M = ibc.euribor((6, ql.Months), ibc.CURVE)
E1Y = ibc.euribor((1, ql.Years), ibc.CURVE_STEEP)
SOFR = irx.overnight_spec("Sofr", 0, irx.US_SOFR, ibc.A360, ibc.CURVE_STEEP)
ESTR = irx.overnight_spec("Estr", 1, ibc.TARGET, ibc.A360, ibc.CURVE)
NUL = irx.ibor_spec("NulIbor", (3, ql.Months), 0, ibc.NULL_CALENDAR, ql.Following, False, ibc.A365, ibc.CURVE)
INSPECTORS = ["date", "nominal", "accrualStartDate", "accrualEndDate", "referencePeriodStart", "referencePeriodEnd",
              "exCouponDate", "accrualPeriod", "fixingDate", "fixingDays", "gearing", "spread", "isInArrears",
              "fixingConvention", "dayCounter", "index", "hasPricer"]


def frc(*args, adjustment=0.0, pricer="fixing", **kw):
    if pricer == "fixing":
        p = {"type": "IndexFixingPricer", "adjustment": adjustment}
    elif pricer == "black":
        p = ibc.pricer_spec(ibc.VOL_LN)
    else:
        p = None
    return dict(ibc.ibor_coupon_spec(*args, pricer=p, **kw), type="FloatingRateCoupon")


def coupons():
    """(coupon spec, fixings to store as (index spec, [(date, value), ...]))."""
    past6 = [(d(13, 9, 2024), 0.0331), (d(15, 7, 2024), 0.0372)]
    return [
        (frc(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M), None),
        (frc(d(21, 1, 2026), 2e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, 1.5, 0.001, adjustment=0.0005), None),
        (frc(d(17, 3, 2025), 1e6, d(17, 9, 2024), d(17, 3, 2025), 2, E6M, adjustment=-0.001), past6),
        (frc(d(17, 1, 2025), 1e6, d(17, 7, 2024), d(17, 1, 2025), None, E6M, ex_coupon=d(10, 1, 2025)), past6),
        (frc(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), 0, E3M, in_arrears=True, adjustment=0.0002), None),
        (frc(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), 3, E3M, -0.5, 0.04, dc=ibc.A365), None),
        (frc(d(17, 1, 2030), 1e7, d(17, 1, 2029), d(17, 1, 2030), 2, E1Y, 0.8, 0.003, dc=ibc.THIRTY360_BB), None),
        (frc(d(30, 11, 2026), 1e6, d(17, 4, 2026), d(30, 11, 2026), 2, E6M, ref_start=d(30, 5, 2026),
             ref_end=d(30, 11, 2026)), None),
        (frc(d(17, 10, 2025), 1e6, d(17, 4, 2025), d(17, 10, 2025), 1, E6M, fixing_convention=ql.Following,
             ex_coupon=d(10, 10, 2025)), None),
        (frc(d(20, 1, 2027), 1e6, d(19, 1, 2026), d(19, 1, 2027), 2, E1Y, fixing_convention=ql.ModifiedPreceding),
         None),
        (frc(d(21, 4, 2025), 1e6, d(21, 1, 2025), d(21, 4, 2025), 0, SOFR, spread=0.0025), None),
        (frc(d(17, 2, 2025), 1e6, d(15, 1, 2025), d(17, 2, 2025), 0, SOFR, in_arrears=True), None),
        (frc(d(20, 2, 2025), 1e6, d(14, 1, 2025), d(20, 2, 2025), None, ESTR), [(ESTR, [(d(13, 1, 2025), 0.0291)])]),
        (frc(d(3, 7, 2025), 1e6, d(3, 4, 2025), d(3, 7, 2025), 0, NUL, 2.0, -0.01, adjustment=0.001), None),
        (frc(d(15, 1, 2025), 5e5, d(15, 10, 2024), d(15, 1, 2025), 0, NUL, in_arrears=True),
         [(NUL, [(TODAY, 0.0305)])]),
        (frc(d(18, 8, 2025), 1e6, d(18, 2, 2025), d(18, 8, 2025), 2, E6M, -1.0, 0.0, adjustment=0.0003), None),
        # failing
        (frc(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, pricer="black"), None),
        (frc(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, pricer=None), None),
        (frc(d(17, 3, 2025), 1e6, d(17, 9, 2024), d(17, 3, 2025), 2, E6M), None),
        (frc(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, 0.0), None),
    ]


def scaled(H, method, scale, *args):
    try:
        x = H.call(method, *args)
    except RuntimeError:
        return dict(v.THROWS)
    return v.amount(x, scale)


def cases():
    with cxx.connect() as H:
        out = []
        for k, (spec, stored) in enumerate(coupons()):
            fixings = None
            if stored is not None:
                fixings = {}
                for index, pairs in ([(spec["index"], stored)] if isinstance(stored[0][0], ql.Date) else stored):
                    fixings[H.call("Index::name", index)] = [[v.enc(x), y] for x, y in pairs]
            start, end = ql.Date(spec["startDate"]), ql.Date(spec["endDate"])
            dates = [v.enc(x) for x in (start + 1, start + (end - start) // 2, ql.Date(spec["paymentDate"]) - 3)]
            with H.case_settings(v.enc(TODAY), fixings=fixings) as st:
                inputs = {"settings": st, "coupon": spec, "discountCurve": ibc.DISCOUNT, "accrualDates": dates}
                try:
                    inspectors = H.call("FloatingRateCoupon", spec)
                except RuntimeError:
                    out.append({"name": f"{k}: refused", "inputs": inputs, "expected": {"coupon": dict(v.THROWS)}})
                    continue
                expected = {name: inspectors[name] for name in INSPECTORS}
                scale = spec["nominal"]
                for member in ("rate", "indexFixing", "adjustedFixing", "convexityAdjustment"):
                    expected[member] = v.attempt(lambda m=member: H.call(f"FloatingRateCoupon::{m}", spec))
                expected["amount"] = scaled(H, "FloatingRateCoupon::amount", scale, spec)
                expected["price"] = scaled(H, "FloatingRateCoupon::price(Handle<YieldTermStructure>)", scale, spec,
                                           ibc.DISCOUNT)
                expected["accruedAmount"] = [scaled(H, "FloatingRateCoupon::accruedAmount(Date)", scale, spec, x)
                                             for x in dates]
                out.append({"name": f"{k}: {inspectors['index']} {start} to {end}", "inputs": inputs,
                            "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/floatingratecoupon.hpp", cases)
