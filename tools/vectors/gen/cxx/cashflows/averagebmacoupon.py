# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AverageBMACoupon and AverageBMALeg (ql/cashflows/averagebmacoupon.hpp), each output computed
by the C++ harness: the wheel binds neither class, and reaches the coupons only inside a BMASwap, whose leg sets no
gearings or spreads. 26 cases on the evaluation date Wednesday 15 January 2025, on a BMA index forecasting on a flat
curve (2.75%, continuous, Actual/365 (Fixed)), with the BMA fixings of the Wednesdays (or the business days after a
Wednesday holiday) from 4 December 2024 to 8 January 2025 stored.

- 13 coupons: weekly, quarterly and yearly periods, future and seasoned (whose averages read the stored fixings),
  around Wednesday holidays and 4 July, geared, spread and with an explicit day counter and reference period; a
  coupon whose end is a day before its start, in the same week, whose averaging days (0) differ from its interest
  days (-1), which QuantLib refuses ("averaging days 0 differ from interest days -1"); a zero gearing, which QuantLib
  refuses at construction ("Null gearing not allowed"); and an index with no curve, whose forecasts QuantLib refuses.
  The outputs are fixingDates(), indexFixings() and rate().
- 13 legs: weekly, monthly and quarterly schedules, rule-based (with short first and last periods, whose reference
  dates QuantLib moves by the tenor) or from dates; one notional or amortizing ones; gearings and spreads, one each or
  shorter lists than the coupons, whose last element repeats (each given as one value or as a list of one); payment
  day counters and adjustments; and no notional,
  on an empty schedule and on a full one, which QuantLib refuses ("no notional given") before it reads the schedule's
  size. The outputs are, coupon by coupon, the payment date, the nominal, the accrual and reference-period dates, the
  day counter's name, the gearing, the spread and the fixing dates.

A refused value is {"throws": true}; a refused leg gives one in each output. Each case's inputs hold its settings and
its "coupon" ({"paymentDate", "nominal", "startDate", "endDate", "indexCurve", "gearing", "spread", "refPeriodStart",
"refPeriodEnd", "dayCounter"}) or "leg" ({"schedule", "indexCurve", "notionals", "paymentDayCounter",
"paymentAdjustment", "gearings", "spreads"}), as tools/cxx/src/cashflows/averagebmacoupon.cpp reads them: a schedule
is a spec of cashflows/fixedratecoupon.py or {"dates"}, a curve a flat forward spec of indexes/interestrateindex.py,
a null day counter or reference date QuantLib's empty one. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Coupons/AverageBMACouponCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_fixedvsfloatingswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "fixedvsfloatingswap.py"))
fvf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fvf)
irx = fvf.irx

TODAY = fvf.TODAY
D = fvf.d
CURVE = fvf.curve(0.0275)
US_GOVT = {"type": "UnitedStates", "market": int(ql.UnitedStates.GovernmentBond)}
BMA_NAME = "BMA1W Actual/Actual (ISDA)"
# The stored BMA fixings: 4, 11 and 18 December 2024, Thursday 26 December after Christmas Day, Thursday 2 January after
# New Year's Day, and 8 January 2025.
STORED = [(D(4, 12, 2024), 0.0291), (D(11, 12, 2024), 0.0288), (D(18, 12, 2024), 0.0293), (D(26, 12, 2024), 0.0290),
          (D(2, 1, 2025), 0.0286), (D(8, 1, 2025), 0.0284)]


def coupon(payment, start, end, nominal=1e6, gearing=1.0, spread=0.0, ref_start=None, ref_end=None, dc=None,
           curve=CURVE):
    return {"paymentDate": v.enc(payment), "nominal": nominal, "startDate": v.enc(start), "endDate": v.enc(end),
            "indexCurve": curve, "gearing": gearing, "spread": spread, "refPeriodStart": v.enc(ref_start),
            "refPeriodEnd": v.enc(ref_end), "dayCounter": dc}


def leg(schedule, notionals=(1e6,), dc=None, adjustment=ql.Following, gearings=(), spreads=()):
    """A leg spec; a gearing or spread given as a number calls the setter taking one value, a tuple the one taking a
    vector."""
    return {"schedule": schedule, "indexCurve": CURVE, "notionals": list(notionals), "paymentDayCounter": dc,
            "paymentAdjustment": int(adjustment),
            "gearings": gearings if isinstance(gearings, float) else list(gearings),
            "spreads": spreads if isinstance(spreads, float) else list(spreads)}


def sched(eff, mat, tenor, convention=ql.Following, rule=ql.DateGeneration.Backward, first=None):
    return fvf.sched(eff, mat, tenor, US_GOVT, convention, rule, False, first)


def coupons():
    A360, A365 = irx.A360, irx.A365
    out = [
        ("a week", coupon(D(29, 1, 2025), D(22, 1, 2025), D(29, 1, 2025))),
        ("a quarter", coupon(D(17, 4, 2025), D(17, 1, 2025), D(17, 4, 2025))),
        ("a year", coupon(D(20, 1, 2026), D(17, 1, 2025), D(20, 1, 2026))),
        ("seasoned a quarter", coupon(D(2, 4, 2025), D(2, 1, 2025), D(2, 4, 2025))),
        ("seasoned from 5 December", coupon(D(5, 3, 2025), D(5, 12, 2024), D(5, 3, 2025))),
        ("geared and spread", coupon(D(17, 4, 2025), D(17, 1, 2025), D(17, 4, 2025), gearing=0.8, spread=0.001)),
        ("negative gearing", coupon(D(17, 4, 2025), D(17, 1, 2025), D(17, 4, 2025), gearing=-0.5, spread=0.02)),
        ("Actual/360, a reference period", coupon(D(18, 4, 2025), D(17, 1, 2025), D(17, 4, 2025), dc=A360,
                                                  ref_start=D(17, 10, 2024), ref_end=D(17, 4, 2025))),
        ("around 4 July", coupon(D(10, 7, 2025), D(1, 7, 2025), D(9, 7, 2025), dc=A365)),
        ("around Christmas Eve 2025", coupon(D(29, 12, 2025), D(22, 12, 2025), D(29, 12, 2025))),
        ("ending a day before it starts", coupon(D(24, 1, 2025), D(24, 1, 2025), D(23, 1, 2025))),
        ("zero gearing", coupon(D(17, 4, 2025), D(17, 1, 2025), D(17, 4, 2025), gearing=0.0)),
        ("no curve", coupon(D(17, 4, 2025), D(17, 1, 2025), D(17, 4, 2025), curve=None)),
    ]
    return out


def legs():
    M, W = ql.Months, ql.Weeks
    start, end = D(17, 1, 2025), D(17, 7, 2025)
    out = [
        ("weekly", leg(sched(start, D(18, 4, 2025), ql.Period(1, W)))),
        ("monthly, gearings and spreads", leg(sched(start, end, ql.Period(1, M)), gearings=(0.8, 0.9),
                                              spreads=(0.001,))),
        ("quarterly, one gearing and spread", leg(sched(start, D(17, 1, 2026), ql.Period(3, M)), gearings=1.1,
                                                  spreads=-0.0005)),
        ("quarterly, lists of one gearing and spread", leg(sched(start, D(17, 1, 2026), ql.Period(3, M)),
                                                           gearings=(1.2,), spreads=(0.0004,))),
        ("amortizing", leg(sched(start, end, ql.Period(1, M)), notionals=(3e6, 2e6, 1e6))),
        ("Actual/360, ModifiedFollowing", leg(sched(start, end, ql.Period(1, M), ql.ModifiedFollowing),
                                              dc=irx.A360, adjustment=ql.ModifiedFollowing)),
        ("unadjusted payments", leg(sched(start, end, ql.Period(1, M)), adjustment=ql.Unadjusted)),
        ("short first period", leg(sched(D(3, 2, 2025), D(17, 7, 2025), ql.Period(1, M),
                                         first=D(17, 2, 2025)))),
        ("short last period, forward", leg(sched(start, D(5, 6, 2025), ql.Period(1, M),
                                                 rule=ql.DateGeneration.Forward))),
        ("from dates", leg({"dates": v.enc([D(17, 1, 2025), D(14, 2, 2025), D(21, 3, 2025), D(17, 4, 2025)])},
                           gearings=(0.95,))),
        ("seasoned", leg(sched(D(4, 12, 2024), D(4, 6, 2025), ql.Period(1, M)))),
        ("no notional, empty schedule", leg({"dates": []}, notionals=())),
        ("no notional", leg(sched(start, end, ql.Period(1, M)), notionals=())),
    ]
    return out


LEG_OUTPUTS = ["date", "nominal", "accrualStartDate", "accrualEndDate", "referencePeriodStart", "referencePeriodEnd",
               "dayCounter", "gearing", "spread", "fixingDates"]


def cases():
    with cxx.connect() as H:
        out = []
        fixings = {BMA_NAME: [[v.enc(d), x] for d, x in STORED]}
        for name, spec in coupons():
            with H.case_settings(v.enc(TODAY), fixings=fixings) as st:
                out.append({"name": f"coupon {name}", "inputs": {"settings": st, "coupon": spec},
                            "expected": {
                                "fixingDates": v.attempt(lambda: H.call("AverageBMACoupon::fixingDates", spec)),
                                "indexFixings": v.attempt(lambda: H.call("AverageBMACoupon::indexFixings", spec)),
                                "rate": v.attempt(lambda: H.call("AverageBMACoupon::rate", spec))}})
        for name, spec in legs():
            with H.case_settings(v.enc(TODAY), fixings=fixings) as st:
                try:
                    built = H.call("AverageBMALeg::operator Leg", spec)
                except RuntimeError:
                    expected = {k: dict(v.THROWS) for k in LEG_OUTPUTS}
                else:
                    expected = {k: [c[k] for c in built] for k in LEG_OUTPUTS}
                out.append({"name": f"leg {name}", "inputs": {"settings": st, "leg": spec}, "expected": expected})
        check_rows(out)
        return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    assert by_name["coupon ending a day before it starts"]["rate"] == v.THROWS
    assert by_name["coupon ending a day before it starts"]["fixingDates"] != v.THROWS
    assert by_name["coupon zero gearing"]["fixingDates"] == v.THROWS
    assert by_name["coupon no curve"]["indexFixings"] == v.THROWS
    assert by_name["leg no notional, empty schedule"]["date"] == v.THROWS
    assert by_name["leg monthly, gearings and spreads"]["gearing"][-1] == 0.9


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/averagebmacoupon.hpp", cases)
