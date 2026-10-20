# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CPI coupon, CPI cash flow and CPI leg (ql/cashflows/cpicoupon.hpp), with QuantLib's
default CPI-coupon pricer (ql/cashflows/cpicouponpricer.hpp), through the wheel's CPICoupon, CPICashFlow and CPILeg.

- "coupon" cases: a CPICoupon in each of its three constructors' forms (a base CPI, a base date, both), observed flat
  or linearly, its accrual end fixed or forecast, with the pricer set: fixedRate, baseCPI, baseDate,
  observationInterpolation, fixingDate, indexFixing, rate, amount, adjustedIndexGrowth, indexRatio at 6 dates and
  accruedAmount at 6 dates. A coupon with neither base or a base CPI of zero is refused.
- "cashflow" cases: a CPICashFlow with a base date, a base fixing or both, paying the ratio or its growth: notional,
  baseDate (refused with none), baseFixing, indexFixing, fixingDate, frequency, interpolation, growthOnly and amount.
  One with neither base is refused.
- "leg" cases: CPILeg with every setting given, the wheel's function's defaults differing from QuantLib's builder's:
  each flow's kind ("CPICoupon", "FixedRateCoupon" or "CPICashFlow"), date and amount; a coupon's accrual dates and
  rate. A leg with an explicit base CPI, observed flat or linearly, or with neither base, observed flat: QuantLib's two
  bases of a leg with neither, its coupons' and its notional flow's, agree under a flat observation, and differ under
  a linear one, which nanoql fixes (tests/NanoQL.Tests/Divergences/CpiLegBaseFixingTests.cs). A leg with caps on a
  non-zero rate, or a single-date schedule with neither base, is refused. The wheel's function takes no base date,
  so a leg with one is QuantLib's test-suite case (test-suite/inflationcpibond.cpp) alone.

A refused value is {"throws": true}. A case's inputs hold its settings, the index spec and fixings
(tools/vectors/gen/cashflows/indexedcashflow.py's) and the coupon's, cash flow's or leg's arguments, a schedule as
tools/vectors/gen/cashflows/cashflows.py's or {"dates", "calendar", "convention"}. Read back by
tests/NanoQL.Tests/Vectors/Coupons/CPICouponVectorTests.cs."""
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

TODAY = icf.TODAY
UK = cf.UK_SETTLEMENT
THIRTY360_BB = cf.THIRTY360_BB
A365 = cf.A365
LAG3 = ql.Period(3, ql.Months)
LAG2 = ql.Period(2, ql.Months)


def null_date(d):
    return d if d is not None else ql.Date()


def null_real(x):
    return x if x is not None else ql.nullDouble()


def ratio_dates(start, end):
    return [start, start + 17, start + ql.Period(2, ql.Months), end - 30, end, end + 45]


# (label, form, base CPI, base date, payment, nominal, start, end, lag, interpolation, day counter, fixed rate,
#  ref start, ref end, ex-coupon date)
COUPONS = [
    ("base CPI, flat, forecast", "baseCPI", 305.2, None, ql.Date(17, ql.June, 2025), 1e6, ql.Date(15, ql.June, 2024),
     ql.Date(15, ql.June, 2025), LAG3, ql.CPI.Flat, A365, 0.02, None, None, None),
    ("base date, flat, forecast", "baseDate", None, ql.Date(1, ql.March, 2024), ql.Date(17, ql.June, 2025), 1e6,
     ql.Date(15, ql.June, 2024), ql.Date(15, ql.June, 2025), LAG3, ql.CPI.Flat, A365, 0.02, None, None, None),
    ("base date, linear, month end", "baseDate", None, ql.Date(31, ql.January, 2024), ql.Date(1, ql.February, 2027),
     2.5e6, ql.Date(31, ql.July, 2024), ql.Date(31, ql.January, 2027), LAG3, ql.CPI.Linear, THIRTY360_BB, 0.0125,
     None, None, None),
    ("both, linear, fixed end", "both", 301.0, ql.Date(1, ql.July, 2023), ql.Date(4, ql.April, 2024), 1e6,
     ql.Date(1, ql.October, 2023), ql.Date(1, ql.April, 2024), LAG2, ql.CPI.Linear, A365, 0.03, None, None, None),
    ("base date, flat, fixed end, two-month lag", "baseDate", None, ql.Date(1, ql.September, 2023),
     ql.Date(3, ql.May, 2024), 1e6, ql.Date(1, ql.November, 2023), ql.Date(1, ql.May, 2024), LAG2, ql.CPI.Flat, A365,
     0.015, None, None, None),
    ("base CPI, linear, reference period and ex-coupon", "baseCPI", 306.75, None, ql.Date(17, ql.December, 2024), 1e6,
     ql.Date(15, ql.June, 2024), ql.Date(15, ql.December, 2024), LAG3, ql.CPI.Linear, A365, 0.025,
     ql.Date(15, ql.March, 2024), ql.Date(15, ql.December, 2024), ql.Date(10, ql.December, 2024)),
    ("both, base CPI used", "both", 290.0, ql.Date(1, ql.January, 2022), ql.Date(17, ql.June, 2026), 1e6,
     ql.Date(15, ql.June, 2024), ql.Date(15, ql.June, 2026), LAG3, ql.CPI.Flat, A365, 0.01, None, None, None),
    ("negative fixed rate", "baseDate", None, ql.Date(1, ql.March, 2024), ql.Date(17, ql.June, 2025), 1e6,
     ql.Date(15, ql.June, 2024), ql.Date(15, ql.June, 2025), LAG3, ql.CPI.Linear, A365, -0.005, None, None, None),
    ("neither base", "both", None, None, ql.Date(17, ql.June, 2025), 1e6, ql.Date(15, ql.June, 2024),
     ql.Date(15, ql.June, 2025), LAG3, ql.CPI.Flat, A365, 0.02, None, None, None),
    ("zero base CPI", "baseCPI", 0.0, None, ql.Date(17, ql.June, 2025), 1e6, ql.Date(15, ql.June, 2024),
     ql.Date(15, ql.June, 2025), LAG3, ql.CPI.Flat, A365, 0.02, None, None, None),
]


def make_coupon(form, base_cpi, base_date, payment, nominal, start, end, lag, interp, dc, rate, ref_start, ref_end,
                ex_coupon, index):
    tail = (payment, nominal, start, end, index, lag, interp, cf.day_counter(dc), rate, null_date(ref_start),
            null_date(ref_end), null_date(ex_coupon))
    if form == "baseCPI":
        return ql.CPICoupon(base_cpi, *tail)
    if form == "baseDate":
        return ql.CPICoupon(base_date, *tail)
    return ql.CPICoupon(null_real(base_cpi), null_date(base_date), *tail)


def coupon_cases():
    out = []
    for k, (label, form, base_cpi, base_date, payment, nominal, start, end, lag, interp, dc, rate, ref_start, ref_end,
            ex_coupon) in enumerate(COUPONS):
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(icf.UKRPI, icf.UK_FIXINGS)
            inputs = {"settings": st, "kind": "coupon", "index": icf.UKRPI, "fixings": icf.UK_FIXINGS, "form": form,
                      "baseCPI": base_cpi, "baseDate": v.enc(base_date), "paymentDate": v.enc(payment),
                      "nominal": nominal, "startDate": v.enc(start), "endDate": v.enc(end),
                      "observationLag": icf.period(lag), "interpolation": int(interp), "dayCounter": dc,
                      "fixedRate": rate, "refPeriodStart": v.enc(ref_start), "refPeriodEnd": v.enc(ref_end),
                      "exCouponDate": v.enc(ex_coupon)}
            try:
                c = make_coupon(form, base_cpi, base_date, payment, nominal, start, end, lag, interp, dc, rate,
                                ref_start, ref_end, ex_coupon, index)
            except RuntimeError:
                out.append({"name": f"coupon {label} / {k}", "inputs": inputs,
                            "expected": {"construction": dict(v.THROWS)}})
                continue
            c.setPricer(ql.CPICouponPricer())
            dates = ratio_dates(start, end)
            inputs["dates"] = v.enc(dates)
            out.append({"name": f"coupon {label} / {k}", "inputs": inputs,
                        "expected": {
                            "fixedRate": c.fixedRate(), "baseCPI": v.enc(c.baseCPI()),
                            "baseDate": v.enc(c.baseDate()),
                            "observationInterpolation": int(c.observationInterpolation()),
                            "fixingDate": v.enc(c.fixingDate()), "indexFixing": v.attempt(c.indexFixing),
                            "rate": v.attempt(c.rate), "amount": v.attempt(c.amount),
                            "adjustedIndexGrowth": v.attempt(c.adjustedIndexGrowth),
                            "indexRatio": [v.attempt(lambda d=d: c.indexRatio(d)) for d in dates],
                            "accruedAmount": [v.attempt(lambda d=d: c.accruedAmount(d)) for d in dates]}})
    return out


# (label, notional, base date, base fixing, observation date, lag, interpolation, payment, growth only)
CASHFLOWS = [
    ("base date, flat", 1e6, ql.Date(1, ql.January, 2024), None, ql.Date(15, ql.June, 2027), LAG3, ql.CPI.Flat,
     ql.Date(17, ql.June, 2027), False),
    ("base date, linear, growth", 1e6, ql.Date(15, ql.February, 2024), None, ql.Date(31, ql.December, 2026), LAG3,
     ql.CPI.Linear, ql.Date(4, ql.January, 2027), True),
    ("base fixing", 5e6, None, 301.5, ql.Date(15, ql.June, 2027), LAG3, ql.CPI.Flat, ql.Date(17, ql.June, 2027),
     False),
    ("both, linear", 1e6, ql.Date(1, ql.December, 2023), 300.0, ql.Date(20, ql.March, 2026), LAG2, ql.CPI.Linear,
     ql.Date(20, ql.March, 2026), True),
    ("fixed observation", 1e6, ql.Date(1, ql.June, 2022), None, ql.Date(15, ql.May, 2024), LAG2, ql.CPI.Linear,
     ql.Date(15, ql.May, 2024), False),
    ("neither base", 1e6, None, None, ql.Date(15, ql.June, 2027), LAG3, ql.CPI.Flat, ql.Date(17, ql.June, 2027),
     False),
]


def cashflow_cases():
    out = []
    for k, (label, notional, base_date, base_fixing, obs, lag, interp, payment, growth) in enumerate(CASHFLOWS):
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(icf.UKRPI, icf.UK_FIXINGS)
            inputs = {"settings": st, "kind": "cashflow", "index": icf.UKRPI, "fixings": icf.UK_FIXINGS,
                      "notional": notional, "baseDate": v.enc(base_date), "baseFixing": base_fixing,
                      "observationDate": v.enc(obs), "observationLag": icf.period(lag), "interpolation": int(interp),
                      "paymentDate": v.enc(payment), "growthOnly": growth}
            try:
                c = ql.CPICashFlow(notional, index, null_date(base_date), null_real(base_fixing), obs, lag, interp,
                                   payment, growth)
            except RuntimeError:
                out.append({"name": f"cashflow {label} / {k}", "inputs": inputs,
                            "expected": {"construction": dict(v.THROWS)}})
                continue
            out.append({"name": f"cashflow {label} / {k}", "inputs": inputs,
                        "expected": {
                            "notional": c.notional(), "baseDate": v.attempt(c.baseDate),
                            "baseFixing": v.attempt(c.baseFixing), "indexFixing": v.attempt(c.indexFixing),
                            "fixingDate": v.enc(c.fixingDate()), "frequency": int(c.frequency()),
                            "interpolation": int(c.interpolation()), "growthOnly": c.growthOnly(),
                            "amount": v.attempt(c.amount)}})
    return out


def leg_spec(schedule, base_cpi, lag, notionals, fixed_rates, dc, adjustment, calendar, interp, growth, caps=(),
             floors=(), ex_coupon=None):
    return {"schedule": schedule, "baseCPI": base_cpi, "observationLag": icf.period(lag), "notionals": list(notionals),
            "fixedRates": list(fixed_rates), "paymentDayCounter": dc, "paymentAdjustment": int(adjustment),
            "paymentCalendar": calendar, "interpolation": int(interp), "subtractInflationNominal": growth,
            "caps": list(caps), "floors": list(floors), "exCouponPeriod": ex_coupon}


def make_schedule(spec):
    if "dates" in spec:
        return ql.Schedule([ql.Date(d) for d in spec["dates"]], cf.calendar(spec["calendar"]), spec["convention"])
    return cf.schedule(spec)


def make_leg(spec, index):
    ex = spec["exCouponPeriod"]
    ex_args = (ql.Period(ex["period"][0], ex["period"][1]), cf.calendar(ex["calendar"]), ex["convention"],
               ex["endOfMonth"]) if ex else (ql.Period(), ql.NullCalendar(), ql.Unadjusted, False)
    return ql.CPILeg(spec["notionals"], make_schedule(spec["schedule"]), index, null_real(spec["baseCPI"]),
                     ql.Period(spec["observationLag"][0], spec["observationLag"][1]),
                     cf.day_counter(spec["paymentDayCounter"]), spec["paymentAdjustment"], spec["fixedRates"],
                     spec["caps"], spec["floors"], *ex_args, cf.calendar(spec["paymentCalendar"]),
                     spec["subtractInflationNominal"], spec["interpolation"])


SEMIANNUAL = cf.schedule_spec(ql.Date(15, ql.June, 2024), ql.Date(15, ql.June, 2029), ql.Period(6, ql.Months), UK,
                              ql.ModifiedFollowing, ql.DateGeneration.Backward, False)
ANNUAL_STUB = cf.schedule_spec(ql.Date(31, ql.May, 2024), ql.Date(30, ql.November, 2030), ql.Period(1, ql.Years), UK,
                               ql.Unadjusted, ql.DateGeneration.Backward, True)
PAST_START = cf.schedule_spec(ql.Date(1, ql.October, 2022), ql.Date(1, ql.October, 2027), ql.Period(6, ql.Months), UK,
                              ql.ModifiedFollowing, ql.DateGeneration.Forward, False)
ONE_DATE = {"dates": [v.enc(ql.Date(15, ql.June, 2029))], "calendar": UK, "convention": int(ql.ModifiedFollowing)}
EX_COUPON = {"period": [7, int(ql.Days)], "calendar": UK, "convention": int(ql.Preceding), "endOfMonth": False}

LEGS = [
    ("base CPI, flat", leg_spec(SEMIANNUAL, 306.1, LAG3, [1e6], [0.02], A365, ql.ModifiedFollowing, UK, ql.CPI.Flat,
                                True)),
    ("base CPI, linear, ratio", leg_spec(SEMIANNUAL, 306.1, LAG3, [1e6], [0.02], A365, ql.ModifiedFollowing, UK,
                                         ql.CPI.Linear, False)),
    ("no base, flat", leg_spec(SEMIANNUAL, None, LAG3, [1e6], [0.0125], THIRTY360_BB, ql.ModifiedFollowing, UK,
                               ql.CPI.Flat, True)),
    ("no base, flat, month-end stub", leg_spec(ANNUAL_STUB, None, LAG3, [1e6, 9e5, 8e5], [0.01, 0.015], A365,
                                               ql.Following, UK, ql.CPI.Flat, True)),
    ("past start, two-month lag", leg_spec(PAST_START, None, LAG2, [2e6], [0.03], A365, ql.ModifiedFollowing, UK,
                                           ql.CPI.Flat, False)),
    ("zero rates are fixed coupons", leg_spec(SEMIANNUAL, 306.1, LAG3, [1e6], [0.02, 0.0, 0.0, 0.025], A365,
                                              ql.ModifiedFollowing, UK, ql.CPI.Linear, True)),
    ("zero rates, floored", leg_spec(SEMIANNUAL, 306.1, LAG3, [1e6], [0.0], A365, ql.ModifiedFollowing, UK,
                                     ql.CPI.Flat, True, caps=[0.01], floors=[0.002])),
    ("ex-coupon", leg_spec(SEMIANNUAL, 306.1, LAG3, [1e6], [0.02], A365, ql.ModifiedFollowing, UK, ql.CPI.Flat, True,
                           ex_coupon=EX_COUPON)),
    ("one date, base CPI", leg_spec(ONE_DATE, 306.1, LAG3, [1e6], [0.02], A365, ql.ModifiedFollowing, UK, ql.CPI.Flat,
                                    True)),
    ("one date, no base", leg_spec(ONE_DATE, None, LAG3, [1e6], [0.02], A365, ql.ModifiedFollowing, UK, ql.CPI.Flat,
                                   True)),
    ("caps on a rate", leg_spec(SEMIANNUAL, 306.1, LAG3, [1e6], [0.02], A365, ql.ModifiedFollowing, UK, ql.CPI.Flat,
                                True, caps=[0.05])),
    ("no fixed rates", leg_spec(SEMIANNUAL, 306.1, LAG3, [1e6], [], A365, ql.ModifiedFollowing, UK, ql.CPI.Flat,
                                True)),
]


def flow_kind(flow):
    if ql.as_cpi_coupon(flow) is not None:
        return "CPICoupon"
    if ql.as_cpi_cashflow(flow) is not None:
        return "CPICashFlow"
    if ql.as_fixed_rate_coupon(flow) is not None:
        return "FixedRateCoupon"
    raise ValueError("an unexpected cash flow in a CPI leg")


def leg_cases():
    out = []
    for k, (label, spec) in enumerate(LEGS):
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(icf.UKRPI, icf.UK_FIXINGS)
            inputs = {"settings": st, "kind": "leg", "index": icf.UKRPI, "fixings": icf.UK_FIXINGS, "leg": spec}
            try:
                leg = make_leg(spec, index)
            except RuntimeError:
                out.append({"name": f"leg {label} / {k}", "inputs": inputs,
                            "expected": {"construction": dict(v.THROWS)}})
                continue
            coupons = [ql.as_coupon(f) for f in leg]
            out.append({"name": f"leg {label} / {k}", "inputs": inputs,
                        "expected": {
                            "kinds": [flow_kind(f) for f in leg],
                            "dates": v.enc([f.date() for f in leg]),
                            "amounts": [v.attempt(f.amount) for f in leg],
                            "accrualStartDates": [v.enc(c.accrualStartDate()) if c else None for c in coupons],
                            "accrualEndDates": [v.enc(c.accrualEndDate()) if c else None for c in coupons],
                            "rates": [v.attempt(c.rate) if c else None for c in coupons]}})
    return out


def make_cases():
    return coupon_cases() + cashflow_cases() + leg_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/cpicoupon.hpp", make_cases)
