# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's multiple-resets coupon and leg (ql/cashflows/multipleresetscoupon.hpp), through the wheel's
MultipleResetsCoupon, CompoundingMultipleResetsPricer, AveragingMultipleResetsPricer and MultipleResetsLeg, on the
evaluation date 15 January 2025 and Euribor-style indexes on a flat 3.1% curve, with the past fixings of
cashflows/iborcoupon.py's history (every TARGET business day of the year to the evaluation date).

- "coupon" cases: a coupon on a reset schedule of 1-month or 3-month sub-periods, future, in progress or past, with 0,
  2 or 5 fixing days, a gearing, a coupon spread and a rate spread, a reference period, a day counter or none (the
  index's), an ex-coupon date, priced by the compounding or the averaging pricer: fixingDate (the last sub-period's),
  fixingDates, valueDates, dt, rate, amount, accrualPeriod, gearing, spread, rateSpread, exCouponDate and the accrued
  amount half-way through the accrual; and the pricer's swapletPrice, and capletPrice, capletRate, floorletPrice and
  floorletRate at 3%, each of which QuantLib's multiple-resets pricers refuse ("not implemented").
- "leg" cases: MultipleResetsLeg with every setting given, compounding or averaging, a payment lag and calendar, an
  ex-coupon period, settings per coupon: per coupon its payment, accrual and ex-coupon dates, its sub-periods' count
  and fixing dates, rate and amount. A reset schedule whose periods the resets per coupon don't divide, and too many
  values of a setting, are refused ("construction": {"throws": true}); zero resets per coupon is the S-cms plan's
  CMR5 (xiv) (tests/NanoQL.Tests/Divergences/MultipleResetsZeroResetsTests.cs) and isn't sampled.

A case's inputs hold its settings with the stored fixings, the index (a spec of indexes/interestrateindex.py), and the
coupon's or leg's arguments, a schedule as cashflows/cashflows.py's. Read back by
tests/NanoQL.Tests/Vectors/Coupons/MultipleResetsCouponVectorTests.cs."""
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

TODAY = irx.TODAY
D = ql.Date
TARGET, A360, A365 = cf.TARGET, cf.A360, cf.A365
MF, F = ql.ModifiedFollowing, ql.Following
E1M, E3M = ibc.E1M, ibc.E3M
PRICERS = {"compounding": ql.CompoundingMultipleResetsPricer, "averaging": ql.AveragingMultipleResetsPricer}
AVERAGING = {"compounding": ql.RateAveraging.Compound, "averaging": ql.RateAveraging.Simple}


def resets(start, end, months, convention=MF, rule=ql.DateGeneration.Forward):
    return cf.schedule_spec(start, end, ql.Period(months, ql.Months), TARGET, convention, rule, False)


def optional_date(d):
    return d if d is not None else ql.Date()


# (label, index, payment, nominal, reset schedule, fixing days, gearing, coupon spread, rate spread, ref start,
#  ref end, day counter, ex-coupon date)
COUPONS = [
    ("1Y on 3M resets, future", E3M, D(17, ql.March, 2026), 1e6,
     resets(D(17, ql.March, 2025), D(17, ql.March, 2026), 3), 2, 1.0, 0.0, 0.0, None, None, A360, None),
    ("6M on 1M resets, in progress", E1M, D(15, ql.May, 2025), 1e6, resets(D(15, ql.November, 2024),
                                                                            D(15, ql.May, 2025), 1),
     2, 1.0, 0.0, 0.0, None, None, A360, None),
    ("1Y on 3M resets, geared, both spreads", E3M, D(19, ql.January, 2026), 2.5e6,
     resets(D(17, ql.January, 2025), D(19, ql.January, 2026), 3), 2, 1.5, 0.001, 0.0025, None, None, A365, None),
    ("6M on 1M resets, 0 fixing days, the index's day counter", E1M, D(20, ql.August, 2025), 1e6,
     resets(D(20, ql.February, 2025), D(20, ql.August, 2025), 1), 0, 1.0, 0.0, 0.0, None, None, None, None),
    ("9M on 3M resets, 5 fixing days, a reference period", E3M, D(16, ql.October, 2025), 1e6,
     resets(D(16, ql.January, 2025), D(16, ql.October, 2025), 3), 5, 1.0, -0.0005, 0.001,
     D(16, ql.October, 2024), D(16, ql.October, 2025), A360, None),
    ("9M on 3M resets, past", E3M, D(19, ql.November, 2024), 1e6, resets(D(19, ql.February, 2024),
                                                                           D(19, ql.November, 2024), 3),
     2, 1.0, 0.0, 0.0, None, None, A360, None),
    ("6M on 1M resets, ex-coupon", E1M, D(16, ql.June, 2025), 1e6, resets(D(16, ql.December, 2024),
                                                                           D(16, ql.June, 2025), 1),
     2, 0.8, 0.002, 0.0, None, None, A360, D(9, ql.June, 2025)),
]


def coupon_cases():
    out = []
    k = 0
    for label, index_spec, payment, nominal, schedule, days, gearing, spread, rate_spread, ref_start, ref_end, dc, \
            ex_coupon in COUPONS:
        for kind, pricer in PRICERS.items():
            with v.case_settings(TODAY, fixings=ibc.fixings_for(index_spec)) as st:
                index = irx.make_index(index_spec)
                # the wheel builds no empty day counter: a coupon with none leaves it, and what follows, out
                args = [payment, nominal, cf.schedule(schedule), days, index, gearing, spread, rate_spread,
                        optional_date(ref_start), optional_date(ref_end)]
                if dc is not None:
                    args += [cf.day_counter(dc), optional_date(ex_coupon)]
                else:
                    assert ex_coupon is None
                c = ql.MultipleResetsCoupon(*args)
                p = pricer()
                c.setPricer(p)
                start, end = c.accrualStartDate(), c.accrualEndDate()
                mid = start + (end - start) // 2
                out.append({"name": f"coupon {label}, {kind} / {k}",
                            "inputs": {"settings": st, "kind": "coupon", "index": index_spec, "pricer": kind,
                                       "paymentDate": v.enc(payment), "nominal": nominal, "resetSchedule": schedule,
                                       "fixingDays": days, "gearing": gearing, "couponSpread": spread,
                                       "rateSpread": rate_spread, "refPeriodStart": v.enc(ref_start),
                                       "refPeriodEnd": v.enc(ref_end), "dayCounter": dc,
                                       "exCouponDate": v.enc(ex_coupon), "accrualDate": v.enc(mid)},
                            "expected": {
                                "fixingDate": v.enc(c.fixingDate()), "fixingDates": v.enc(list(c.fixingDates())),
                                "valueDates": v.enc(list(c.valueDates())), "dt": list(c.dt()),
                                "rate": v.attempt(c.rate), "amount": v.attempt(c.amount),
                                "accrualPeriod": c.accrualPeriod(), "gearing": c.gearing(), "spread": c.spread(),
                                "rateSpread": c.rateSpread(), "exCouponDate": v.enc(c.exCouponDate()) or None,
                                "accruedAmount": v.attempt(lambda: c.accruedAmount(mid)),
                                "swapletPrice": v.attempt(p.swapletPrice),
                                "capletPrice": v.attempt(lambda: p.capletPrice(0.03)),
                                "capletRate": v.attempt(lambda: p.capletRate(0.03)),
                                "floorletPrice": v.attempt(lambda: p.floorletPrice(0.03)),
                                "floorletRate": v.attempt(lambda: p.floorletRate(0.03))}})
            k += 1
    return out


def leg_spec(schedule, resets_per_coupon, notionals, dc, convention, calendar, lag, fixing_days=(), gearings=(),
             coupon_spreads=(), rate_spreads=(), ex_coupon=None, averaging="compounding"):
    return {"schedule": schedule, "resetsPerCoupon": resets_per_coupon, "notionals": list(notionals),
            "paymentDayCounter": dc, "paymentConvention": int(convention), "paymentCalendar": calendar,
            "paymentLag": lag, "fixingDays": list(fixing_days), "gearings": list(gearings),
            "couponSpreads": list(coupon_spreads), "rateSpreads": list(rate_spreads), "exCoupon": ex_coupon,
            "averaging": averaging}


def ex_coupon_spec(period, calendar, convention, eom=False):
    return {"period": [period[0], int(period[1])], "calendar": calendar, "convention": int(convention),
            "endOfMonth": eom}


MONTHLY = resets(D(17, ql.March, 2025), D(17, ql.March, 2027), 1)
QUARTERLY = resets(D(16, ql.January, 2024), D(16, ql.January, 2027), 3)
MONTHLY_PAST = resets(D(15, ql.October, 2024), D(15, ql.October, 2025), 1)

# (label, index, spec)
LEGS = [
    ("monthly resets, quarterly coupons, compounding", E1M,
     leg_spec(MONTHLY, 3, [1e6], A360, MF, TARGET, 0)),
    ("monthly resets, quarterly coupons, averaging", E1M,
     leg_spec(MONTHLY, 3, [1e6], A360, MF, TARGET, 0, averaging="averaging")),
    ("semiannual coupons, settings per coupon", E1M,
     leg_spec(MONTHLY, 6, [1e6, 8e5], A365, F, TARGET, 0, (2, 0), (1.0, 1.2), (0.0, 0.001), (0.0005, 0.0, 0.001))),
    ("a payment lag on another calendar, an ex-coupon period", E3M,
     leg_spec(QUARTERLY, 2, [2e6], A360, MF, cf.UK_SETTLEMENT, 2,
              ex_coupon=ex_coupon_spec((5, ql.Days), TARGET, ql.Preceding))),
    ("annual coupons, past and in progress, averaging", E3M,
     leg_spec(QUARTERLY, 4, [1e6], A360, MF, TARGET, 0, (2,), averaging="averaging")),
    ("in progress, the schedule's calendar ex-coupon", E1M,
     leg_spec(MONTHLY_PAST, 4, [1e6], A360, MF, TARGET, 1, ex_coupon=ex_coupon_spec((1, ql.Weeks), None, MF))),
    ("resets not dividing the periods", E1M, leg_spec(MONTHLY, 5, [1e6], A360, MF, TARGET, 0)),
    ("too many gearings", E1M, leg_spec(MONTHLY, 3, [1e6], A360, MF, TARGET, 0, gearings=[1.0] * 9)),
    ("too many fixing days", E1M, leg_spec(MONTHLY, 12, [1e6], A360, MF, TARGET, 0, fixing_days=[2, 2, 2])),
]


def make_leg(spec, index):
    # the wheel builds no empty calendar: the ex-coupon settings go by keyword, an absent calendar left out
    ex = spec["exCoupon"]
    kw = {"averagingMethod": AVERAGING[spec["averaging"]]}
    if ex is not None:
        kw.update(exCouponPeriod=ql.Period(ex["period"][0], ex["period"][1]), exCouponConvention=ex["convention"],
                  exCouponEndOfMonth=ex["endOfMonth"])
        if ex["calendar"] is not None:
            kw["exCouponCalendar"] = cf.calendar(ex["calendar"])
    return ql.MultipleResetsLeg(cf.schedule(spec["schedule"]), index, spec["resetsPerCoupon"], spec["notionals"],
                                cf.day_counter(spec["paymentDayCounter"]), spec["paymentConvention"],
                                cf.calendar(spec["paymentCalendar"]), spec["paymentLag"], spec["fixingDays"],
                                spec["gearings"], spec["couponSpreads"], spec["rateSpreads"], **kw)


def leg_cases():
    out = []
    for k, (label, index_spec, spec) in enumerate(LEGS):
        with v.case_settings(TODAY, fixings=ibc.fixings_for(index_spec)) as st:
            index = irx.make_index(index_spec)
            inputs = {"settings": st, "kind": "leg", "index": index_spec, "leg": spec}
            try:
                leg = make_leg(spec, index)
            except RuntimeError:
                out.append({"name": f"leg {label} / {k}", "inputs": inputs,
                            "expected": {"construction": dict(v.THROWS)}})
                continue
            coupons = [ql.as_multiple_resets_coupon(f) for f in leg]
            out.append({"name": f"leg {label} / {k}", "inputs": inputs,
                        "expected": {
                            "dates": v.enc([c.date() for c in coupons]),
                            "accrualStartDates": v.enc([c.accrualStartDate() for c in coupons]),
                            "accrualEndDates": v.enc([c.accrualEndDate() for c in coupons]),
                            "exCouponDates": [v.enc(c.exCouponDate()) or None for c in coupons],
                            "resets": [len(c.fixingDates()) for c in coupons],
                            "fixingDates": [v.enc(list(c.fixingDates())) for c in coupons],
                            "rates": [v.attempt(c.rate) for c in coupons],
                            "amounts": [v.attempt(c.amount) for c in coupons]}})
    return out


def make_cases():
    return coupon_cases() + leg_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/multipleresetscoupon.hpp", make_cases)
