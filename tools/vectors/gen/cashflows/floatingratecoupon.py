# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FloatingRateCoupon (ql/cashflows/floatingratecoupon.hpp), read through IborCoupon, its
concrete class the wheel builds: 40 Ibor coupons, each under both settings of the at-par switch, on the evaluation
date 15 January 2025, priced by a BlackIborCouponPricer built without an explicit choice, so that it reads the switch.

The coupons fix in advance and in arrears, with gearings from -0.5 to 2 and spreads either side of zero, 0 to 3 fixing
days and null ones (the index's), on Euribor-style 1M, 3M, 6M and 1Y indexes and a 3M index with no fixing days on a
calendar with no holidays, over flat curves rising, falling and negative; their periods run from before the evaluation
date (fixings stored for the index's business days within a week of the fixing date, but none on 13 January 2025,
so that the coupon fixing then is refused), through it (a fixing today, stored for some), to years after it, regular and broken, with payment delays, explicit reference
periods, accrual day counters other than the index's, ex-coupon dates, fixing conventions, and a caplet volatility
where an in-arrears coupon's convexity adjustment needs one.

Each case's outputs are the coupon's fixing date, fixing days, accrual period, index name and day-counter name; its
rate, amount, index fixing, adjusted fixing and convexity adjustment; its price on a flat 2.5% discount curve; and its
accrued amount at five dates (the accrual start, ten days in, the middle, the day after the ex-coupon date or two days
before the payment date, and the payment date). A refused value is {"throws": true}; amounts are compared at the
coupon's nominal. Each case's inputs hold the coupon spec ("coupon", as iborcoupon.py's functions build it), the
discount curve ("discountCurve") and the accrued-amount dates ("accrualDates"). Read back by
tests/NanoQL.Tests/Vectors/Coupons/FloatingRateCouponVectorTests.cs."""
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

E1M = ibc.euribor((1, ql.Months), ibc.CURVE_STEEP)
E3M = ibc.euribor((3, ql.Months), ibc.CURVE)
E6M = ibc.euribor((6, ql.Months), ibc.CURVE)
E6M_NEG = ibc.euribor((6, ql.Months), ibc.CURVE_NEGATIVE, "EuriborNeg")
E1Y = ibc.euribor((1, ql.Years), ibc.CURVE_STEEP)
NUL3M = irx.ibor_spec("NulIbor", (3, ql.Months), 0, ibc.NULL_CALENDAR, ql.Following, False, ibc.A365, ibc.CURVE)


def coupons():
    """(coupon spec, store today's fixing)."""
    black = ibc.pricer_spec()
    vol = ibc.pricer_spec(ibc.VOL_LN)
    vol_n = ibc.pricer_spec(ibc.VOL_N)
    vol_sln = ibc.pricer_spec(ibc.VOL_SLN)
    c = ibc.ibor_coupon_spec
    T, F = True, False
    return [
        # past coupons, read from stored fixings
        (c(d(17, 7, 2024), 1e6, d(17, 1, 2024), d(17, 7, 2024), 2, E6M, pricer=black), F),
        (c(d(17, 1, 2025), 1e6, d(17, 7, 2024), d(17, 1, 2025), 2, E6M, pricer=black), F),
        (c(d(20, 3, 2025), 2e6, d(20, 12, 2024), d(20, 3, 2025), 2, E3M, 1.5, 0.001, pricer=black), F),
        (c(d(3, 2, 2025), 5e5, d(3, 1, 2025), d(3, 2, 2025), 1, E1M, 2.0, -0.002, pricer=black), F),
        (c(d(15, 4, 2025), 1e6, d(15, 1, 2025), d(15, 4, 2025), 0, NUL3M, pricer=black), F),
        (c(d(10, 2, 2025), 1e6, d(10, 11, 2024), d(10, 2, 2025), 3, E3M, -0.5, 0.04, pricer=black), F),
        (c(d(13, 1, 2026), 1e6, d(13, 1, 2025), d(13, 1, 2026), None, E1Y, pricer=black), F),
        (c(d(17, 3, 2025), 1e6, d(17, 9, 2024), d(17, 3, 2025), 2, E6M, ex_coupon=d(10, 3, 2025), pricer=black), F),
        (c(d(21, 7, 2025), 1e6, d(17, 1, 2025), d(17, 7, 2025), 2, E6M, pricer=black), F),
        (c(d(17, 7, 2025), 1e6, d(17, 1, 2025), d(17, 7, 2025), 2, E6M, pricer=black), T),
        (c(d(17, 4, 2025), 3e6, d(17, 1, 2025), d(17, 4, 2025), 2, E3M, 1.0, 0.0025, pricer=black), T),
        (c(d(17, 2, 2025), 1e6, d(17, 1, 2025), d(17, 2, 2025), 2, E1M, pricer=black), T),
        (c(d(19, 1, 2026), 1e6, d(17, 1, 2025), d(19, 1, 2026), 2, E1Y, 1.0, 0.0, dc=ibc.A365, pricer=black), T),
        # a missing past fixing
        (c(d(17, 7, 2025), 1e6, d(17, 1, 2025), d(17, 7, 2025), 4, E6M, pricer=black), F),
        # future coupons, forecast
        (c(d(18, 8, 2025), 1e6, d(18, 2, 2025), d(18, 8, 2025), 2, E6M, pricer=black), F),
        (c(d(2, 3, 2026), 1e6, d(2, 9, 2025), d(2, 3, 2026), 2, E6M, 1.25, 0.0015, pricer=black), F),
        (c(d(17, 12, 2027), 1e6, d(17, 6, 2027), d(17, 12, 2027), 2, E6M, dc=ibc.THIRTY360_BB, pricer=black), F),
        (c(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), 0, E3M, pricer=black), F),
        (c(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), 3, E3M, pricer=black), F),
        (c(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), None, E3M, pricer=black), F),
        (c(d(5, 9, 2025), 1e6, d(5, 5, 2025), d(5, 9, 2025), 2, E6M, pricer=black), F),
        (c(d(30, 11, 2026), 1e6, d(17, 4, 2026), d(30, 11, 2026), 2, E6M, ref_start=d(30, 5, 2026),
           ref_end=d(30, 11, 2026), pricer=black), F),
        (c(d(15, 10, 2025), 1e6, d(15, 4, 2025), d(15, 10, 2025), 2, E1M, 1.0, 0.0, pricer=black), F),
        (c(d(17, 1, 2030), 1e7, d(17, 1, 2029), d(17, 1, 2030), 2, E1Y, 0.8, 0.003, pricer=black), F),
        (c(d(14, 8, 2025), 1e6, d(14, 2, 2025), d(14, 8, 2025), 2, E6M_NEG, pricer=black), F),
        (c(d(14, 8, 2025), 1e6, d(14, 2, 2025), d(14, 8, 2025), 2, E6M_NEG, -1.0, 0.0, pricer=black), F),
        (c(d(17, 10, 2025), 1e6, d(17, 4, 2025), d(17, 10, 2025), 2, E6M, ex_coupon=d(10, 10, 2025),
           fixing_convention=ql.Following, pricer=black), F),
        (c(d(20, 1, 2027), 1e6, d(19, 1, 2026), d(19, 1, 2027), 1, E1Y, fixing_convention=ql.ModifiedPreceding,
           pricer=black), F),
        (c(d(3, 7, 2025), 1e6, d(3, 4, 2025), d(3, 7, 2025), 0, NUL3M, 1.0, -0.001, pricer=black), F),
        # in arrears, with the Black76 convexity adjustment
        (c(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), 2, E3M, in_arrears=True, pricer=vol), F),
        (c(d(17, 1, 2027), 1e6, d(17, 7, 2026), d(17, 1, 2027), 2, E6M, 1.5, 0.002, in_arrears=True, pricer=vol), F),
        (c(d(17, 1, 2028), 1e6, d(17, 1, 2027), d(17, 1, 2028), 2, E1Y, in_arrears=True, pricer=vol_sln), F),
        (c(d(20, 9, 2025), 1e6, d(20, 6, 2025), d(20, 9, 2025), 2, E3M, in_arrears=True, pricer=vol_n), F),
        (c(d(20, 9, 2025), 1e6, d(20, 6, 2025), d(20, 9, 2025), 2, E3M, in_arrears=True, pricer=black), F),
        (c(d(17, 2, 2025), 1e6, d(17, 12, 2024), d(17, 1, 2025), 2, E1M, in_arrears=True, pricer=vol), F),
        (c(d(15, 1, 2025), 1e6, d(15, 10, 2024), d(15, 1, 2025), 0, NUL3M, in_arrears=True, pricer=vol), T),
        # broken periods, which the at-par switch changes
        (c(d(17, 5, 2025), 1e6, d(17, 1, 2025), d(17, 5, 2025), 2, E6M, pricer=black), T),
        (c(d(17, 11, 2025), 1e6, d(19, 5, 2025), d(17, 11, 2025), 1, E3M, pricer=black), F),
        (c(d(19, 2, 2026), 1e6, d(17, 7, 2025), d(19, 2, 2026), 2, E6M, pricer=black), F),
        (c(d(21, 4, 2025), 1e6, d(21, 1, 2025), d(21, 4, 2025), 2, E1Y, pricer=black), F),
    ]


def accrual_dates(spec):
    start, payment = ql.Date(spec["startDate"]), ql.Date(spec["paymentDate"])
    end = ql.Date(spec["endDate"])
    late = ql.Date(spec["exCouponDate"]) + 1 if spec["exCouponDate"] is not None else payment - 2
    return [start, start + 10, start + (end - start) // 2, late, payment]


def stored_fixings(spec, k, today_fixing):
    """Fixings for the coupon's index on the business days within a week of its fixing date and before the evaluation
    date, drawn with the seed, but none on 13 January 2025, which the coupon with 4 fixing days needs; and today's when
    asked for."""
    index = irx.make_index(spec["index"])
    cal = index.fixingCalendar()
    fixing = ibc.coupon(dict(spec, pricer=None)).fixingDate()
    pairs = [(x, y) for x, y in ibc.history(spec["index"], fixing - 7, min(fixing + 7, TODAY - 1), v.SEED + k,
                                            skip=(d(13, 1, 2025),))]
    if today_fixing and cal.isBusinessDay(TODAY):
        pairs.append((TODAY, 0.0301))
    return [(index, pairs)] if pairs else None


def cases():
    out = []
    discount = irx.handle(ibc.DISCOUNT)
    for k, (spec, today_fixing) in enumerate(coupons()):
        dates = accrual_dates(spec)
        for at_par in (True, False):
            stored = stored_fixings(spec, k, today_fixing)
            with v.case_settings(TODAY, use_at_par_coupons=at_par, fixings=stored) as st:
                c = ibc.coupon(spec)
                scale = spec["nominal"]
                out.append({
                    "name": f"{k}: {spec['index']['familyName']} {ql.Date(spec['startDate'])} to "
                            f"{ql.Date(spec['endDate'])}, {'at par' if at_par else 'indexed'}",
                    "inputs": {"settings": st, "coupon": v.enc(spec), "discountCurve": ibc.DISCOUNT,
                               "accrualDates": v.enc(dates)},
                    "expected": {
                        "fixingDate": v.enc(c.fixingDate()),
                        "fixingDays": c.fixingDays(),
                        "accrualPeriod": c.accrualPeriod(),
                        "index": c.index().name(),
                        "dayCounter": c.dayCounter().name(),
                        "rate": v.attempt(c.rate),
                        "amount": ibc.scaled(c.amount, scale),
                        "indexFixing": v.attempt(c.indexFixing),
                        "adjustedFixing": v.attempt(c.adjustedFixing),
                        "convexityAdjustment": v.attempt(c.convexityAdjustment),
                        "price": ibc.scaled(lambda: c.price(discount), scale),
                        "accruedAmount": [ibc.scaled(lambda x=x: c.accruedAmount(x), scale) for x in dates],
                    }})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/floatingratecoupon.hpp", cases)
