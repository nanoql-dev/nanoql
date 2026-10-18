# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FixedRateCoupon and FixedRateLeg (ql/cashflows/fixedratecoupon.hpp): 5 schedules x 7 leg
variants (flat, amortizing with stepped rates, compounded, a first-period day counter, a payment lag with a payment
calendar and adjustment, an ex-coupon period, a negative rate). For every coupon of each leg: its payment date,
amount, nominal and rate; its accrual and reference-period dates; its accrual period and days; its ex-coupon date;
and its accrued amount at two dates, the middle of the accrual period and a late date. The late date is the day after
the ex-coupon date where the coupon has one, inside the ex-coupon window, so accruedAmount's ex-coupon branch is
checked; otherwise two days before the payment date. Amounts are compared at the leg's largest notional.

Twelve more legs, appended after those, use QuantLib's empty calendar, which QuantLib uses unchecked: a rule-based
schedule built on it with Unadjusted conventions, whose calendar is then the leg's payment calendar and gives its
reference dates, with Unadjusted payments, Following payments or a payment lag, and an ex-coupon period on it; the
schedule refused with an adjusting convention or the end-of-month flag; and ex-coupon periods counted on it from a
TARGET schedule, in weeks, months with the end-of-month flag and zero days and weeks, which build, and in days or
with a Following convention, which QuantLib refuses. A refused leg is the one output "date", QuantLib's throw.

Each case's inputs describe the leg in full, once: "fixedRateLeg" holds its schedule (effective and termination
dates, tenor, calendar, conventions, rule, end of month, first and next-to-last dates) and every FixedRateLeg
setting (notionals; coupon rates with their day counter, compounding and frequency; first-period day counter;
payment calendar, lag and adjustment; ex-coupon period, calendar, convention and end of month). A null setting is
one the leg leaves at its default, but for the schedule's and the ex-coupon period's calendars, where null is QuantLib's
empty calendar, which the wheel gives as a default-constructed schedule's calendar. Calendars are {"type": <class>},
with a "market" integer for UnitedStates and UnitedKingdom; day counters are {"type": <class>}, with a "convention"
integer for Thirty360 and ActualActual;
enums and time units are QuantLib's integers. "midDates" and "lateDates" give the accrued-amount dates per coupon.

The schedules are built with ql.Schedule, the wheel's proxy of C++ QuantLib::Schedule's rule-based constructor. The
legs are built with ql.FixedRateLeg, which forwards to the wheel's C++ helper _FixedRateLeg (QuantLib-SWIG 1.44,
SWIG/cashflows.i). It builds a C++ QuantLib::FixedRateLeg and calls withNotionals, withPaymentAdjustment,
withPaymentCalendar (the schedule's calendar when none is given), withPaymentLag, withFirstPeriodDayCounter (an empty
day counter when none is given, which QuantLib ignores), withExCouponPeriod (an empty period when none is given,
which means no ex-coupon dates) and withCouponRates(rates, dayCount, compounding, frequency), and converts the
builder to a Leg. Read back by tests/NanoQL.Tests/Vectors/Coupons/FixedRateCouponVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TARGET = {"type": "TARGET"}
NULL_CALENDAR = {"type": "NullCalendar"}
US_SETTLEMENT = {"type": "UnitedStates", "market": int(ql.UnitedStates.Settlement)}
UK_SETTLEMENT = {"type": "UnitedKingdom", "market": int(ql.UnitedKingdom.Settlement)}
A360 = {"type": "Actual360"}
A365 = {"type": "Actual365Fixed"}
THIRTY360_BB = {"type": "Thirty360", "convention": int(ql.Thirty360.BondBasis)}
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}


def schedule_spec(eff, mat, tenor, cal, convention, rule, eom, first=None):
    return {"effectiveDate": v.enc(eff), "terminationDate": v.enc(mat), "tenor": [tenor.length(), int(tenor.units())],
            "calendar": cal, "convention": int(convention), "terminationDateConvention": int(convention),
            "rule": int(rule), "endOfMonth": eom, "firstDate": v.enc(first), "nextToLastDate": None}


SCHEDULES = [
    ("6M MF EOM", schedule_spec(ql.Date(31, 1, 2026), ql.Date(31, 1, 2031), ql.Period(6, ql.Months), TARGET,
                                ql.ModifiedFollowing, ql.DateGeneration.Backward, True)),
    ("3M F short front", schedule_spec(ql.Date(15, 11, 2025), ql.Date(15, 6, 2029), ql.Period(3, ql.Months), TARGET,
                                       ql.Following, ql.DateGeneration.Backward, False)),
    ("1Y U short back", schedule_spec(ql.Date(10, 3, 2024), ql.Date(10, 9, 2034), ql.Period(1, ql.Years),
                                      NULL_CALENDAR, ql.Unadjusted, ql.DateGeneration.Forward, False)),
    ("6M MF first date", schedule_spec(ql.Date(20, 1, 2026), ql.Date(20, 1, 2033), ql.Period(6, ql.Months), TARGET,
                                       ql.ModifiedFollowing, ql.DateGeneration.Backward, False, ql.Date(20, 4, 2026))),
    ("1M F 2Y", schedule_spec(ql.Date(29, 2, 2024), ql.Date(28, 2, 2026), ql.Period(1, ql.Months), US_SETTLEMENT,
                              ql.Following, ql.DateGeneration.Backward, True)),
]


def leg_spec(dc, notionals, rates, compounding=ql.Simple, frequency=ql.Annual, first_period_dc=None,
             payment_calendar=None, payment_lag=0, payment_adjustment=ql.Following, ex_coupon=None):
    return {"notionals": notionals, "couponRates": rates, "dayCounter": dc, "compounding": int(compounding),
            "frequency": int(frequency), "firstPeriodDayCounter": first_period_dc, "paymentCalendar": payment_calendar,
            "paymentLag": payment_lag, "paymentAdjustment": int(payment_adjustment), "exCouponPeriod": ex_coupon}


VARIANTS = [
    ("flat 3% A360", leg_spec(A360, [1e6], [0.03])),
    ("amortizing 30/360", leg_spec(THIRTY360_BB, [1e6, 9e5, 8e5, 7e5], [0.025, 0.03])),
    ("A365 compounded", leg_spec(A365, [5e6], [0.04], ql.Compounded, ql.Semiannual)),
    ("ActAct first period dc", leg_spec(ACT_ISDA, [1e6], [0.05], first_period_dc=A365)),
    ("payment lag and calendar", leg_spec(A360, [1e6], [0.03], payment_calendar=UK_SETTLEMENT, payment_lag=2,
                                          payment_adjustment=ql.ModifiedFollowing)),
    ("ex-coupon", leg_spec(A360, [1e6], [0.03], ex_coupon={"period": [5, int(ql.Days)], "calendar": TARGET,
                                                           "convention": int(ql.Following), "endOfMonth": False})),
    ("negative rate", leg_spec(A360, [2e6], [-0.004])),
]


# QuantLib's empty calendar, Calendar(): the wheel gives it only as a default-constructed schedule's calendar, whose
# proxy refers to the schedule's member, so the schedule is kept alive with it.
_EMPTY_CALENDAR_OWNER = ql.Schedule()
EMPTY_CALENDAR = _EMPTY_CALENDAR_OWNER.calendar()

EMPTY_SCHEDULE = schedule_spec(ql.Date(15, 11, 2025), ql.Date(15, 6, 2029), ql.Period(3, ql.Months), None,
                               ql.Unadjusted, ql.DateGeneration.Backward, False)


def ex_coupon_spec(length, units, cal, convention, eom=False):
    return {"period": [length, int(units)], "calendar": cal, "convention": int(convention), "endOfMonth": eom}


EMPTY_CALENDAR_LEGS = [
    ("3M U short front, empty calendar / Unadjusted payments",
     dict(leg_spec(A360, [1e6], [0.03], payment_adjustment=ql.Unadjusted), schedule=EMPTY_SCHEDULE)),
    ("3M U short front, empty calendar / Following payments refused",
     dict(leg_spec(A360, [1e6], [0.03]), schedule=EMPTY_SCHEDULE)),
    ("3M U short front, empty calendar / payment lag refused",
     dict(leg_spec(A360, [1e6], [0.03], payment_lag=2, payment_adjustment=ql.Unadjusted), schedule=EMPTY_SCHEDULE)),
    ("3M U short front, empty calendar / ex-coupon 1M on it",
     dict(leg_spec(THIRTY360_BB, [1e6], [0.025], payment_adjustment=ql.Unadjusted,
                   ex_coupon=ex_coupon_spec(1, ql.Months, None, ql.Unadjusted)), schedule=EMPTY_SCHEDULE)),
    ("3M MF short front, empty calendar / schedule refused",
     dict(leg_spec(A360, [1e6], [0.03], payment_adjustment=ql.Unadjusted),
          schedule=dict(EMPTY_SCHEDULE, convention=int(ql.ModifiedFollowing),
                        terminationDateConvention=int(ql.ModifiedFollowing)))),
    ("1M U end of month, empty calendar / schedule refused",
     dict(leg_spec(A360, [1e6], [0.03], payment_adjustment=ql.Unadjusted),
          schedule=schedule_spec(ql.Date(31, 1, 2026), ql.Date(31, 1, 2027), ql.Period(1, ql.Months), None,
                                 ql.Unadjusted, ql.DateGeneration.Backward, True))),
    ("6M MF EOM / ex-coupon 2W on the empty calendar",
     dict(leg_spec(A360, [1e6], [0.03], ex_coupon=ex_coupon_spec(2, ql.Weeks, None, ql.Unadjusted)),
          schedule=SCHEDULES[0][1])),
    ("6M MF EOM / ex-coupon 1M end of month on the empty calendar",
     dict(leg_spec(A360, [1e6], [0.03], ex_coupon=ex_coupon_spec(1, ql.Months, None, ql.Unadjusted, True)),
          schedule=SCHEDULES[0][1])),
    ("6M MF EOM / ex-coupon 0D on the empty calendar",
     dict(leg_spec(A360, [1e6], [0.03], ex_coupon=ex_coupon_spec(0, ql.Days, None, ql.Following)),
          schedule=SCHEDULES[0][1])),
    ("6M MF EOM / ex-coupon 0W on the empty calendar",
     dict(leg_spec(A360, [1e6], [0.03], ex_coupon=ex_coupon_spec(0, ql.Weeks, None, ql.Following)),
          schedule=SCHEDULES[0][1])),
    ("6M MF EOM / ex-coupon 5D on the empty calendar refused",
     dict(leg_spec(A360, [1e6], [0.03], ex_coupon=ex_coupon_spec(5, ql.Days, None, ql.Unadjusted)),
          schedule=SCHEDULES[0][1])),
    ("6M MF EOM / ex-coupon 1M Following on the empty calendar refused",
     dict(leg_spec(A360, [1e6], [0.03], ex_coupon=ex_coupon_spec(1, ql.Months, None, ql.Following)),
          schedule=SCHEDULES[0][1])),
]


def calendar(spec):
    if spec is None:
        return EMPTY_CALENDAR
    t = spec["type"]
    if t == "TARGET":
        return ql.TARGET()
    if t == "NullCalendar":
        return ql.NullCalendar()
    if t == "UnitedStates":
        return ql.UnitedStates(spec["market"])
    if t == "UnitedKingdom":
        return ql.UnitedKingdom(spec["market"])
    raise ValueError(f"unknown calendar {t}")


def day_counter(spec):
    t = spec["type"]
    if t == "Actual365Fixed":
        return ql.Actual365Fixed()
    if t == "Actual360":
        return ql.Actual360()
    if t == "Thirty360":
        return ql.Thirty360(spec["convention"])
    if t == "ActualActual":
        return ql.ActualActual(spec["convention"])
    raise ValueError(f"unknown day counter {t}")


def date_or_null(serial):
    return ql.Date(serial) if serial is not None else ql.Date()


def schedule(spec):
    return ql.Schedule(ql.Date(spec["effectiveDate"]), ql.Date(spec["terminationDate"]),
                       ql.Period(spec["tenor"][0], spec["tenor"][1]), calendar(spec["calendar"]), spec["convention"],
                       spec["terminationDateConvention"], spec["rule"], spec["endOfMonth"],
                       date_or_null(spec["firstDate"]), date_or_null(spec["nextToLastDate"]))


def fixed_rate_leg(spec):
    """The leg a "fixedRateLeg" spec describes, every setting passed by name; unset ones keep the helper's defaults."""
    kw = {"dayCount": day_counter(spec["dayCounter"]), "nominals": spec["notionals"],
          "couponRates": spec["couponRates"], "compounding": spec["compounding"],
          "compoundingFrequency": spec["frequency"], "paymentLag": spec["paymentLag"],
          "paymentAdjustment": spec["paymentAdjustment"]}
    if spec["firstPeriodDayCounter"] is not None:
        kw["firstPeriodDayCount"] = day_counter(spec["firstPeriodDayCounter"])
    if spec["paymentCalendar"] is not None:
        kw["paymentCalendar"] = calendar(spec["paymentCalendar"])
    ex = spec["exCouponPeriod"]
    if ex is not None:
        kw.update(exCouponPeriod=ql.Period(ex["period"][0], ex["period"][1]), exCouponCalendar=calendar(ex["calendar"]),
                  exCouponConvention=ex["convention"], exCouponEndOfMonth=ex["endOfMonth"])
    return ql.FixedRateLeg(schedule(spec["schedule"]), **kw)


def mid_date(c):
    return c.accrualStartDate() + (c.accrualEndDate() - c.accrualStartDate()) // 2


def late_date(c):
    """Inside the ex-coupon window when the coupon has one, else two days before payment."""
    return c.exCouponDate() + 1 if c.exCouponDate() != ql.Date() else c.date() - 2


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for sname, sspec in SCHEDULES:
        for vname, lspec in VARIANTS:
            out.append(leg_case(f"{sname} / {vname}", dict(lspec, schedule=sspec), st))
    for name, spec in EMPTY_CALENDAR_LEGS:
        out.append(leg_case(name, spec, st))
    refused = [c["name"] for c in out if c["expected"].get("date") == v.THROWS]
    assert refused == [name for name, _ in EMPTY_CALENDAR_LEGS if name.endswith("refused")], refused
    return out


def leg_case(name, spec, st):
    """A leg's case: its coupons' outputs, or QuantLib's refusal as the one output "date"."""
    try:
        coupons = [ql.as_fixed_rate_coupon(cf) for cf in fixed_rate_leg(spec)]
    except RuntimeError:
        return {"name": name, "inputs": {"settings": st, "fixedRateLeg": spec, "midDates": [], "lateDates": []},
                "expected": {"date": dict(v.THROWS)}}
    scale = max(spec["notionals"])
    mids, lates = [mid_date(c) for c in coupons], [late_date(c) for c in coupons]
    return {
        "name": name,
        "inputs": {"settings": st, "fixedRateLeg": spec, "midDates": v.enc(mids), "lateDates": v.enc(lates)},
        "expected": {
            "date": [v.enc(c.date()) for c in coupons],
            "amount": [v.amount(c.amount(), scale) for c in coupons],
            "nominal": [v.amount(c.nominal(), scale) for c in coupons],
            "rate": [c.rate() for c in coupons],
            "accrualStartDate": [v.enc(c.accrualStartDate()) for c in coupons],
            "accrualEndDate": [v.enc(c.accrualEndDate()) for c in coupons],
            "referencePeriodStart": [v.enc(c.referencePeriodStart()) for c in coupons],
            "referencePeriodEnd": [v.enc(c.referencePeriodEnd()) for c in coupons],
            "accrualPeriod": [c.accrualPeriod() for c in coupons],
            "accrualDays": [c.accrualDays() for c in coupons],
            "exCouponDate": [v.enc(c.exCouponDate()) for c in coupons],
            "accruedAmountMid": [v.amount(c.accruedAmount(d), scale) for c, d in zip(coupons, mids)],
            "accruedAmountLate": [v.amount(c.accruedAmount(d), scale) for c, d in zip(coupons, lates)]}}


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/fixedratecoupon.hpp", cases)
