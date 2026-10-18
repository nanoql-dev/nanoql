# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CappedFlooredCoupon and CappedFlooredIborCoupon (ql/cashflows/capflooredcoupon.hpp): 30
capped, floored and collared Ibor coupons on the evaluation date 15 January 2025, built with CappedFlooredIborCoupon's
constructor or as a CappedFlooredCoupon over an IborCoupon, each with a BlackIborCouponPricer of its own, set with
setPricer, which sets the coupon's and its underlying's, on a ConstantOptionletVolatility: lognormal, shifted
lognormal and normal.

The coupons fix before the evaluation date (fixings stored, so the caplet and floorlet are intrinsic), on it, and after
it (Black's or Bachelier's formula), at gearings either side of zero (a negative gearing turns the cap on the rate into
a floor on the fixing, and the floor into a cap), with spreads, in advance and in arrears, with caps and floors in and
out of the money, under both settings of the at-par switch in turn. Five of the 30 are refused or fail: a cap below
the floor, at a positive and at a negative gearing, which the constructors refuse ("coupon": {"throws": true}); a
capped coupon with no pricer, whose rate QuantLib refuses ("pricer not set"); a caplet with no volatility; and a
floorlet on a coupon whose past fixing is missing.

Each case's outputs are the coupon's nominal, fixing date, rate, amount, cap, floor, effective cap and floor (null
where QuantLib returns Null<Rate>()), isCapped, isFloored, index fixing (the plain index fixing on the fixing date),
adjusted fixing, convexity adjustment (the underlying's), price on a flat 2.5% discount curve, and accrued amount at
the middle of the accrual period. A refused value is {"throws": true}; amounts are compared at the coupon's nominal.
Each case's inputs hold the coupon spec ("coupon", as iborcoupon.py's functions build it, a capped coupon's pricer on
the coupon itself), the discount curve ("discountCurve") and the accrued-amount date ("accrualDate"). Read back by
tests/NanoQL.Tests/Vectors/Coupons/CapFlooredCouponVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_iborcoupon", os.path.join(v.GEN_ROOT, "cashflows", "iborcoupon.py"))
ibc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ibc)

irx = ibc.irx
TODAY = ibc.TODAY
d = ibc.d

E3M = ibc.euribor((3, ql.Months), ibc.CURVE)
E6M = ibc.euribor((6, ql.Months), ibc.CURVE)
E6M_NEG = ibc.euribor((6, ql.Months), ibc.CURVE_NEGATIVE, "EuriborNeg")
E1Y = ibc.euribor((1, ql.Years), ibc.CURVE_STEEP)
VOL_HIGH = ibc.vol_spec(0.45, dc=ibc.A360)


def capped(payment, start, end, index, cap=None, floor=None, gearing=1.0, spread=0.0, vol=ibc.VOL_LN,
           in_arrears=False, nominal=1e6, wrapped=False, timing=ql.BlackIborCouponPricer.Black76, pricer=True):
    p = ibc.pricer_spec(vol, timing) if pricer else None
    if wrapped:
        underlying = ibc.ibor_coupon_spec(payment, nominal, start, end, 2, index, gearing, spread,
                                          in_arrears=in_arrears)
        return ibc.capped_coupon_spec(underlying, cap, floor, p)
    return ibc.ibor_coupon_spec(payment, nominal, start, end, 2, index, gearing, spread, in_arrears=in_arrears,
                                pricer=p, cap=cap, floor=floor, capped=True)


def coupons():
    future = (d(17, 1, 2026), d(17, 7, 2025), d(19, 1, 2026))
    near = (d(17, 4, 2025), d(17, 1, 2025), d(17, 4, 2025))
    past = (d(17, 3, 2025), d(17, 9, 2024), d(17, 3, 2025))
    far = (d(17, 1, 2030), d(17, 1, 2029), d(17, 1, 2030))
    BL = ql.BlackIborCouponPricer.BivariateLognormal
    return [
        # caps, floors and collars on a future fixing, lognormal
        capped(*future, E6M, cap=0.032),
        capped(*future, E6M, cap=0.028, wrapped=True),
        capped(*future, E6M, floor=0.03),
        capped(*future, E6M, floor=0.035, wrapped=True),
        capped(*future, E6M, cap=0.035, floor=0.025),
        capped(*future, E6M, cap=0.031, floor=0.031, wrapped=True),
        capped(*far, E1Y, cap=0.05, floor=0.03, vol=VOL_HIGH),
        # gearing and spread
        capped(*future, E6M, cap=0.05, floor=0.04, gearing=1.5, spread=0.001),
        capped(*future, E6M, cap=0.02, gearing=0.5, spread=0.005, wrapped=True),
        capped(*future, E6M, cap=0.03, gearing=-1.0, spread=0.06),
        capped(*future, E6M, floor=0.02, gearing=-1.0, spread=0.06, wrapped=True),
        capped(*future, E6M, cap=0.035, floor=0.025, gearing=-0.5, spread=0.045),
        # shifted lognormal and normal volatilities, a negative forward
        capped(*future, E6M_NEG, cap=0.0, floor=-0.01, vol=ibc.VOL_SLN),
        capped(*future, E6M_NEG, floor=-0.002, vol=ibc.VOL_N, wrapped=True),
        capped(*future, E6M, cap=0.033, floor=0.029, vol=ibc.VOL_N),
        capped(*far, E1Y, cap=0.04, vol=ibc.VOL_SLN, gearing=2.0, spread=-0.01),
        # in arrears, both timing adjustments
        capped(d(20, 9, 2025), d(20, 6, 2025), d(20, 9, 2025), E3M, cap=0.032, in_arrears=True),
        capped(d(17, 1, 2027), d(17, 7, 2026), d(17, 1, 2027), E6M, floor=0.03, in_arrears=True, vol=ibc.VOL_N),
        capped(d(17, 1, 2027), d(17, 7, 2026), d(17, 1, 2027), E6M, cap=0.033, floor=0.028, timing=BL),
        capped(d(22, 1, 2027), d(17, 7, 2026), d(17, 1, 2027), E6M, cap=0.031, in_arrears=True, timing=BL,
               wrapped=True),
        # fixings on and before the evaluation date: intrinsic values
        capped(*near, E3M, cap=0.029),
        capped(*near, E3M, floor=0.031, wrapped=True),
        capped(*past, E6M, cap=0.03, floor=0.02),
        capped(*past, E6M, cap=0.025, gearing=-1.0, spread=0.05, wrapped=True),
        capped(d(20, 2, 2025), d(20, 11, 2024), d(20, 2, 2025), E3M, cap=0.02, floor=0.01, nominal=5e6),
        # refused or failing
        capped(*future, E6M, cap=0.02, floor=0.03),
        capped(*future, E6M, cap=0.02, floor=0.03, gearing=-1.0, spread=0.06, wrapped=True),
        capped(*future, E6M, cap=0.03, pricer=False),
        capped(*future, E6M, cap=0.03, vol=None),
        capped(*past, E6M, floor=0.03, wrapped=True),
    ]


def stored(spec, k):
    underlying = spec["underlying"] if spec["type"] == "CappedFlooredCoupon" else spec
    index = irx.make_index(underlying["index"])
    fixing = ibc.coupon(dict(underlying, type="IborCoupon", pricer=None)).fixingDate()
    pairs = ibc.history(underlying["index"], fixing - 7, min(fixing + 7, TODAY), v.SEED + k)
    if k == 29:
        pairs = [p for p in pairs if p[0] != fixing]
    return [(index, pairs)] if pairs else None


def cases():
    out = []
    discount = irx.handle(ibc.DISCOUNT)
    for k, spec in enumerate(coupons()):
        start = spec["underlying"]["startDate"] if spec["type"] == "CappedFlooredCoupon" else spec["startDate"]
        end = spec["underlying"]["endDate"] if spec["type"] == "CappedFlooredCoupon" else spec["endDate"]
        nominal = spec["underlying"]["nominal"] if spec["type"] == "CappedFlooredCoupon" else spec["nominal"]
        mid = ql.Date(start + (end - start) // 2)
        with v.case_settings(TODAY, use_at_par_coupons=k % 2 == 0, fixings=stored(spec, k)) as st:
            inputs = {"settings": st, "coupon": v.enc(spec), "discountCurve": ibc.DISCOUNT, "accrualDate": v.enc(mid)}
            name = f"{k}: {spec['type']}"
            try:
                c = ibc.coupon(spec)
            except RuntimeError:
                out.append({"name": name, "inputs": inputs, "expected": {"coupon": dict(v.THROWS)}})
                continue
            out.append({"name": name, "inputs": inputs, "expected": {
                "nominal": c.nominal(),
                "fixingDate": v.enc(c.fixingDate()),
                "rate": v.attempt(c.rate),
                "amount": ibc.scaled(c.amount, nominal),
                "cap": v.enc(c.cap()),
                "floor": v.enc(c.floor()),
                "effectiveCap": v.enc(c.effectiveCap()),
                "effectiveFloor": v.enc(c.effectiveFloor()),
                "isCapped": c.isCapped(),
                "isFloored": c.isFloored(),
                "indexFixing": v.attempt(c.indexFixing),
                "adjustedFixing": v.attempt(c.adjustedFixing),
                "convexityAdjustment": v.attempt(c.convexityAdjustment),
                "price": ibc.scaled(lambda: c.price(discount), nominal),
                "accruedAmount": ibc.scaled(lambda: c.accruedAmount(mid), nominal)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/capflooredcoupon.hpp", cases)
