# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year inflation coupon and leg (ql/cashflows/yoyinflationcoupon.hpp), through the
wheel's YoYInflationCoupon and yoyInflationLeg.

- "coupon" cases: a YoYInflationCoupon on a ratio or a quoted year-on-year index, observed flat or linearly, fixed or
  forecast, with fixing days, a gearing and a spread, and a reference period, priced by the wheel's Black year-on-year
  pricer, whose swaplet is its base pricer's: fixingDate, indexFixing, rate, amount, adjustedFixing, gearing, spread,
  interpolation and accrualPeriod. A linear coupon on a ratio index is sampled where QuantLib's test of its ratio
  branch and nanoql's agree (tests/NanoQL.Tests/Divergences/LaggedYoYRateBranchTests.cs).
- "leg" cases: yoyInflationLeg with every setting given, one count of fixing days (the wheel's function takes no
  vector of them), no caps or floors, so with QuantLib's default pricer: each
  flow's kind ("YoYInflationCoupon" or "FixedRateCoupon", the coupon of a zero gearing), date, amount and rate, and
  a coupon's fixing date. More gearings or spreads than periods are refused.

A refused value is {"throws": true}. A case's inputs hold its settings, the index spec and fixings
(tools/vectors/gen/cashflows/indexedcashflow.py's) and the coupon's or leg's arguments, a schedule as
tools/vectors/gen/cashflows/cashflows.py's. Read back by
tests/NanoQL.Tests/Vectors/Coupons/YoYInflationCouponVectorTests.cs."""
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
A365 = cf.A365
THIRTY360_BB = cf.THIRTY360_BB
LAG2 = ql.Period(2, ql.Months)
LAG3 = ql.Period(3, ql.Months)
RATIO = icf.YYUKRPI_RATIO
QUOTED = ii.preset("YYUKRPI", icf.YOY_UK)
INDEXES = {"ratio": (RATIO, icf.UK_FIXINGS), "quoted": (QUOTED, ii.YY_FIXINGS)}


def null_date(d):
    return d if d is not None else ql.Date()


# (label, index, payment, nominal, start, end, fixing days, lag, interpolation, dc, gearing, spread, ref start, ref end)
COUPONS = [
    ("ratio, flat, forecast", "ratio", ql.Date(17, ql.June, 2025), 1e6, ql.Date(15, ql.June, 2024),
     ql.Date(15, ql.June, 2025), 0, LAG2, ql.CPI.Flat, A365, 1.0, 0.0, None, None),
    ("ratio, linear, forecast, geared", "ratio", ql.Date(16, ql.March, 2026), 2.5e6, ql.Date(15, ql.March, 2025),
     ql.Date(15, ql.March, 2026), 0, LAG3, ql.CPI.Linear, THIRTY360_BB, 1.5, 0.002, None, None),
    ("ratio, flat, fixed", "ratio", ql.Date(3, ql.May, 2024), 1e6, ql.Date(1, ql.May, 2023), ql.Date(1, ql.May, 2024),
     0, LAG2, ql.CPI.Flat, A365, 1.0, 0.0, None, None),
    ("ratio, linear, fixed, fixing days", "ratio", ql.Date(2, ql.April, 2024), 1e6, ql.Date(31, ql.March, 2023),
     ql.Date(31, ql.March, 2024), 5, LAG2, ql.CPI.Linear, A365, 0.8, -0.001, None, None),
    ("quoted, flat, forecast, reference period", "quoted", ql.Date(15, ql.December, 2025), 1e6,
     ql.Date(15, ql.June, 2025), ql.Date(15, ql.December, 2025), 2, LAG2, ql.CPI.Flat, A365, 1.0, 0.0,
     ql.Date(15, ql.December, 2024), ql.Date(15, ql.December, 2025)),
    ("quoted, linear, fixed", "quoted", ql.Date(15, ql.May, 2024), 1e6, ql.Date(15, ql.May, 2023),
     ql.Date(15, ql.May, 2024), 0, LAG2, ql.CPI.Linear, A365, 1.0, 0.0, None, None),
    ("ratio, negative gearing", "ratio", ql.Date(17, ql.June, 2027), 1e6, ql.Date(15, ql.June, 2026),
     ql.Date(15, ql.June, 2027), 0, LAG3, ql.CPI.Flat, A365, -0.5, 0.03, None, None),
]


def pricer():
    vol = ql.YoYOptionletVolatilitySurfaceHandle(ql.ConstantYoYOptionletVolatility(
        0.01, 0, ql.UnitedKingdom(), ql.ModifiedFollowing, ql.Actual365Fixed(), LAG2, ql.Monthly, False))
    return ql.BlackYoYInflationCouponPricer(vol, icf.nominal_curve(icf.NOMINAL))


def coupon_cases():
    out = []
    for k, (label, kind, payment, nominal, start, end, days, lag, interp, dc, gearing, spread, ref_start,
            ref_end) in enumerate(COUPONS):
        spec, fixings = INDEXES[kind]
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(spec, fixings)
            c = ql.YoYInflationCoupon(payment, nominal, start, end, days, index, lag, interp, cf.day_counter(dc),
                                      gearing, spread, null_date(ref_start), null_date(ref_end))
            ql.setCouponPricer([c], pricer())
            out.append({"name": f"coupon {label} / {k}",
                        "inputs": {"settings": st, "kind": "coupon", "index": spec, "fixings": fixings,
                                   "paymentDate": v.enc(payment), "nominal": nominal, "startDate": v.enc(start),
                                   "endDate": v.enc(end), "fixingDays": days, "observationLag": icf.period(lag),
                                   "interpolation": int(interp), "dayCounter": dc, "gearing": gearing,
                                   "spread": spread, "refPeriodStart": v.enc(ref_start),
                                   "refPeriodEnd": v.enc(ref_end), "nominalCurve": icf.NOMINAL},
                        "expected": {
                            "fixingDate": v.enc(c.fixingDate()), "indexFixing": v.attempt(c.indexFixing),
                            "rate": v.attempt(c.rate), "amount": v.attempt(c.amount),
                            "adjustedFixing": v.attempt(c.adjustedFixing), "gearing": c.gearing(),
                            "spread": c.spread(), "interpolation": int(c.interpolation()),
                            "accrualPeriod": c.accrualPeriod()}})
    return out


def leg_spec(schedule, calendar, lag, interp, notionals, dc, adjustment, fixing_days=(), gearings=(), spreads=()):
    return {"schedule": schedule, "paymentCalendar": calendar, "observationLag": icf.period(lag),
            "interpolation": int(interp), "notionals": list(notionals), "paymentDayCounter": dc,
            "paymentAdjustment": int(adjustment), "fixingDays": list(fixing_days), "gearings": list(gearings),
            "spreads": list(spreads)}


ANNUAL = cf.schedule_spec(ql.Date(15, ql.June, 2024), ql.Date(15, ql.June, 2031), ql.Period(1, ql.Years), UK,
                          ql.Unadjusted, ql.DateGeneration.Backward, False)
SEMIANNUAL_STUB = cf.schedule_spec(ql.Date(31, ql.January, 2024), ql.Date(30, ql.September, 2027),
                                   ql.Period(6, ql.Months), UK, ql.ModifiedFollowing, ql.DateGeneration.Backward, True)
PAST = cf.schedule_spec(ql.Date(1, ql.May, 2022), ql.Date(1, ql.May, 2027), ql.Period(1, ql.Years), UK,
                        ql.Following, ql.DateGeneration.Forward, False)

# (label, index, spec)
LEGS = [
    ("ratio, annual", "ratio", leg_spec(ANNUAL, UK, LAG2, ql.CPI.Flat, [1e6], A365, ql.ModifiedFollowing)),
    ("ratio, linear, gearings and spreads", "ratio",
     leg_spec(ANNUAL, UK, LAG3, ql.CPI.Linear, [1e6, 9e5], THIRTY360_BB, ql.Following, (2,),
              (1.0, 1.2, 0.9), (0.0, 0.001))),
    ("ratio, zero gearings are fixed", "ratio",
     leg_spec(ANNUAL, UK, LAG2, ql.CPI.Flat, [1e6], A365, ql.ModifiedFollowing, (), (1.0, 0.0, 1.0, 0.0),
              (0.005, 0.0075))),
    ("quoted, semiannual stub", "quoted",
     leg_spec(SEMIANNUAL_STUB, UK, LAG2, ql.CPI.Flat, [2e6], A365, ql.Preceding, (1,))),
    ("ratio, past start", "ratio", leg_spec(PAST, UK, LAG2, ql.CPI.Linear, [1e6], A365, ql.ModifiedFollowing)),
    ("too many gearings", "ratio",
     leg_spec(ANNUAL, UK, LAG2, ql.CPI.Flat, [1e6], A365, ql.ModifiedFollowing, (), [1.0] * 9)),
    ("too many spreads", "ratio",
     leg_spec(ANNUAL, UK, LAG2, ql.CPI.Flat, [1e6], A365, ql.ModifiedFollowing, (), (), [0.0] * 8)),
]


def make_leg(spec, index):
    return ql.yoyInflationLeg(cf.schedule(spec["schedule"]), cf.calendar(spec["paymentCalendar"]), index,
                              ql.Period(spec["observationLag"][0], spec["observationLag"][1]), spec["interpolation"],
                              spec["notionals"], cf.day_counter(spec["paymentDayCounter"]), spec["paymentAdjustment"],
                              spec["fixingDays"][0] if spec["fixingDays"] else 0, spec["gearings"],
                              spec["spreads"])


def leg_cases():
    out = []
    for k, (label, kind, spec) in enumerate(LEGS):
        index_spec, fixings = INDEXES[kind]
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(index_spec, fixings)
            inputs = {"settings": st, "kind": "leg", "index": index_spec, "fixings": fixings, "leg": spec}
            try:
                leg = make_leg(spec, index)
            except RuntimeError:
                out.append({"name": f"leg {label} / {k}", "inputs": inputs,
                            "expected": {"construction": dict(v.THROWS)}})
                continue
            yoy = [ql.as_yoy_inflation_coupon(f) for f in leg]
            coupons = [ql.as_coupon(f) for f in leg]
            out.append({"name": f"leg {label} / {k}", "inputs": inputs,
                        "expected": {
                            "kinds": ["YoYInflationCoupon" if y is not None else "FixedRateCoupon" for y in yoy],
                            "dates": v.enc([f.date() for f in leg]),
                            "fixingDates": [v.enc(y.fixingDate()) if y is not None else None for y in yoy],
                            "amounts": [v.attempt(f.amount) for f in leg],
                            "rates": [v.attempt(c.rate) for c in coupons]}})
    return out


def make_cases():
    return coupon_cases() + leg_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/yoyinflationcoupon.hpp", make_cases)
