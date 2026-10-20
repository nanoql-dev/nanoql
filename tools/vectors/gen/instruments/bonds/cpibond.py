# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CPI bond (ql/instruments/bonds/cpibond.hpp), through the wheel's CPIBond (its constructor
taking growthOnly, deprecated in QuantLib 1.40, passed false as QuantLib's non-deprecated constructor passes it),
priced by the discounting bond engine on a flat nominal curve.

- "bond" cases: bonds with an explicit base CPI, observed flat or linearly, or with none, observed flat, on semiannual
  and annual schedules, from a past or a future start, with an issue date, a payment calendar and an ex-coupon period:
  the cash flows' dates and amounts, notionals, maturityDate, redemption's date and amount, settlementDate, NPV,
  cleanPrice, dirtyPrice, accruedAmount and settlementValue.

A refused value is {"throws": true}. A case's inputs hold its settings, the index spec and fixings
(tools/vectors/gen/cashflows/indexedcashflow.py's), the nominal curve and the bond's arguments, a schedule as
tools/vectors/gen/cashflows/cashflows.py's. Read back by tests/NanoQL.Tests/Vectors/Instruments/CPIBondVectorTests.cs.
The functions here are shared by tools/vectors/gen/termstructures/yield/cpibondhelper.py, which imports this file."""
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

TODAY = icf.TODAY
UK = cf.UK_SETTLEMENT
M = int(ql.Months)
D = int(ql.Days)


def schedule(start, end, months, convention=ql.Unadjusted):
    return cf.schedule_spec(start, end, ql.Period(months, ql.Months), UK, convention, ql.DateGeneration.Backward, False)


def bond_spec(days, face, base, lag, interp, sched, coupons, dc, convention=ql.ModifiedFollowing, issue=None,
              calendar=None, ex_coupon=None):
    return {"settlementDays": days, "faceAmount": face, "baseCPI": base, "observationLag": [lag, M],
            "interpolation": int(interp), "schedule": sched, "coupons": list(coupons), "accrualDayCounter": dc,
            "paymentConvention": int(convention), "issueDate": v.enc(issue), "paymentCalendar": calendar,
            "exCouponPeriod": ex_coupon}


EX_COUPON = {"period": [7, D], "calendar": UK, "convention": int(ql.Unadjusted), "endOfMonth": False}

BONDS = [
    ("base CPI, flat, semiannual", bond_spec(3, 1e6, 306.1, 3, ql.CPI.Flat,
                                             schedule(ql.Date(2, ql.October, 2023), ql.Date(2, ql.October, 2035), 6),
                                             [0.0125], cf.A365)),
    ("base CPI, linear", bond_spec(2, 100.0, 305.0, 3, ql.CPI.Linear,
                                   schedule(ql.Date(15, ql.March, 2024), ql.Date(15, ql.March, 2034), 6),
                                   [0.01, 0.0125], cf.ACT_ISDA, ql.Following, ql.Date(15, ql.March, 2024))),
    ("no base, flat, annual", bond_spec(0, 5e6, None, 2, ql.CPI.Flat,
                                        schedule(TODAY, ql.Date(15, ql.June, 2031), 12), [0.02], cf.THIRTY360_BB)),
    ("payment calendar, ex-coupon", bond_spec(3, 1e6, 306.1, 3, ql.CPI.Flat,
                                              schedule(ql.Date(2, ql.October, 2023), ql.Date(2, ql.October, 2030), 6),
                                              [0.015], cf.A365, ql.ModifiedFollowing, None, cf.TARGET, EX_COUPON)),
    ("forward start", bond_spec(1, 1e6, 307.0, 3, ql.CPI.Linear,
                                schedule(ql.Date(1, ql.December, 2024), ql.Date(1, ql.December, 2029), 6), [0.005],
                                cf.A365)),
]


def make_bond(spec, index):
    ex = spec["exCouponPeriod"]
    ex_args = (ql.Period(ex["period"][0], ex["period"][1]), cf.calendar(ex["calendar"]), ex["convention"],
               ex["endOfMonth"]) if ex else (ql.Period(), ql.NullCalendar(), ql.Unadjusted, False)
    sched = cf.schedule(spec["schedule"])
    calendar = cf.calendar(spec["paymentCalendar"]) if spec["paymentCalendar"] is not None else sched.calendar()
    return ql.CPIBond(spec["settlementDays"], spec["faceAmount"], False, cpi.null_real(spec["baseCPI"]),
                      ql.Period(spec["observationLag"][0], spec["observationLag"][1]), index, spec["interpolation"],
                      sched, spec["coupons"], cf.day_counter(spec["accrualDayCounter"]), spec["paymentConvention"],
                      ql.Date(spec["issueDate"]) if spec["issueDate"] is not None else ql.Date(), calendar, *ex_args)


def bond_cases():
    out = []
    for k, (label, spec) in enumerate(BONDS):
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(icf.UKRPI, icf.UK_FIXINGS)
            bond = make_bond(spec, index)
            bond.setPricingEngine(ql.DiscountingBondEngine(icf.nominal_curve(icf.NOMINAL)))
            out.append({"name": f"bond {label} / {k}",
                        "inputs": {"settings": st, "index": icf.UKRPI, "fixings": icf.UK_FIXINGS,
                                   "nominalCurve": icf.NOMINAL, "bond": spec},
                        "expected": {
                            "dates": v.enc([f.date() for f in bond.cashflows()]),
                            "amounts": [v.attempt(f.amount) for f in bond.cashflows()],
                            "notionals": list(bond.notionals()), "maturityDate": v.enc(bond.maturityDate()),
                            "redemptionDate": v.enc(bond.redemption().date()),
                            "redemptionAmount": v.attempt(bond.redemption().amount),
                            "settlementDate": v.enc(bond.settlementDate()), "NPV": v.attempt(bond.NPV),
                            "cleanPrice": v.attempt(bond.cleanPrice), "dirtyPrice": v.attempt(bond.dirtyPrice),
                            "accruedAmount": v.attempt(bond.accruedAmount),
                            "settlementValue": v.attempt(bond.settlementValue)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bonds/cpibond.hpp", bond_cases)
