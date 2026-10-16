# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CashFlows (ql/cashflows/cashflows.hpp): startDate, maturityDate, and npv, bps and npvbps on
a yield term structure, over 45 legs. 35 are fixed-rate legs (5 schedules x 7 variants, as in the fixed-rate coupon
vectors); each schedule adds a bullet leg, its flat 3% coupons followed by a Redemption of the notional on the last
payment date, and an amortizing leg, its amortizing coupons followed by an AmortizingPayment of each notional step on
the date of the coupon before the step and a Redemption of the last notional on the last payment date. These give
the branches for cash flows that are not coupons an oracle.

A case is one leg under one of 6 of 18 evaluation contexts (3 evaluation dates x includeReferenceDateEvents x
includeTodaysCashFlows null, false or true), drawn with the seed, on a flat 3% continuous Actual/365 curve whose
reference date is the evaluation date. Its outputs are the leg's start and maturity dates, and its NPV, BPS and the
pair from npvbps under 3 of 6 settlement settings, also drawn: includeSettlementDateFlows (false or true; the wheel
takes no null), the settlement date (null, which means the evaluation date, or two days later) and the NPV date
(null, which means the settlement date, or a later date). NPVs are compared at the leg's largest notional and BPS at
that notional times 1e-4.

Each case's inputs describe the leg in full, once: "leg" holds "fixedRateLeg" (its schedule and every FixedRateLeg
setting, as in the fixed-rate coupon vectors; null for no coupons) and "extraFlows", the flows that follow the
coupons, in order, each {"type": "Redemption" or "AmortizingPayment", "amount", "date"}; "curve" holds the curve's
reference date, forward rate, day counter, compounding and frequency; "settlements" holds the three settlement
settings, in the order of the npv, bps and npvbps outputs. Calendars are {"type": <class>}, with a "market" integer
for UnitedStates and UnitedKingdom; day counters {"type": <class>}, with a "convention" integer for Thirty360 and
ActualActual; enums and time units are QuantLib's integers.

The schedules are built with ql.Schedule, the wheel's proxy of C++ QuantLib::Schedule's rule-based constructor. The
legs are built with ql.FixedRateLeg, which forwards to the wheel's C++ helper _FixedRateLeg (QuantLib-SWIG 1.44,
SWIG/cashflows.i). It builds a C++ QuantLib::FixedRateLeg and calls withNotionals, withPaymentAdjustment,
withPaymentCalendar (the schedule's calendar when none is given), withPaymentLag, withFirstPeriodDayCounter (an empty
day counter when none is given, which QuantLib ignores), withExCouponPeriod (an empty period when none is given,
which means no ex-coupon dates) and withCouponRates(rates, dayCount, compounding, frequency), and converts the
builder to a Leg. Read back by tests/NanoQL.Tests/Vectors/Coupons/CashFlowsVectorTests.cs."""
import os
import random
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


FLAT = "flat 3% A360"
AMORTIZING = "amortizing 30/360"
VARIANTS = [
    (FLAT, leg_spec(A360, [1e6], [0.03])),
    (AMORTIZING, leg_spec(THIRTY360_BB, [1e6, 9e5, 8e5, 7e5], [0.025, 0.03])),
    ("A365 compounded", leg_spec(A365, [5e6], [0.04], ql.Compounded, ql.Semiannual)),
    ("ActAct first period dc", leg_spec(ACT_ISDA, [1e6], [0.05], first_period_dc=A365)),
    ("payment lag and calendar", leg_spec(A360, [1e6], [0.03], payment_calendar=UK_SETTLEMENT, payment_lag=2,
                                          payment_adjustment=ql.ModifiedFollowing)),
    ("ex-coupon", leg_spec(A360, [1e6], [0.03], ex_coupon={"period": [5, int(ql.Days)], "calendar": TARGET,
                                                           "convention": int(ql.Following), "endOfMonth": False})),
    ("negative rate", leg_spec(A360, [2e6], [-0.004])),
]

EVALUATION_DATES = [ql.Date(15, ql.October, 2026), ql.Date(31, ql.July, 2026), ql.Date(10, ql.March, 2030)]
# (includeSettlementDateFlows, settlement date offset, NPV date offset), offsets from the evaluation date
SETTLEMENTS = [(False, None, None), (True, None, None), (False, 2, None), (True, 2, None), (False, 2, 32),
               (True, 2, 2)]
CURVE_RATE = 0.03


def calendar(spec):
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


EXTRA_FLOWS = {"Redemption": ql.Redemption, "AmortizingPayment": ql.AmortizingPayment}


def leg(spec):
    """The leg a "leg" spec describes: its fixed-rate coupons, then its extra flows in order."""
    flows = list(fixed_rate_leg(spec["fixedRateLeg"])) if spec["fixedRateLeg"] is not None else []
    flows += [EXTRA_FLOWS[f["type"]](f["amount"], ql.Date(f["date"])) for f in spec["extraFlows"]]
    return ql.Leg(flows)


def flow(kind, amount, date):
    return {"type": kind, "amount": amount, "date": v.enc(date)}


def legs():
    """(name, leg spec, scale) for the 45 legs."""
    out = []
    for sname, sspec in SCHEDULES:
        for vname, lspec in VARIANTS:
            out.append((f"{sname} / {vname}", {"fixedRateLeg": dict(lspec, schedule=sspec), "extraFlows": []},
                        max(lspec["notionals"])))
    variants = dict(VARIANTS)
    for sname, sspec in SCHEDULES:
        flat = dict(variants[FLAT], schedule=sspec)
        coupons = fixed_rate_leg(flat)
        last = coupons[len(coupons) - 1].date()
        out.append((f"{sname} / {FLAT} + redemption",
                    {"fixedRateLeg": flat, "extraFlows": [flow("Redemption", flat["notionals"][0], last)]}, 1e6))
        amortizing = dict(variants[AMORTIZING], schedule=sspec)
        coupons = fixed_rate_leg(amortizing)
        nominals = amortizing["notionals"]
        extra = [flow("AmortizingPayment", nominals[i] - nominals[i + 1], coupons[i].date())
                 for i in range(min(len(nominals) - 1, len(coupons) - 1))]
        extra.append(flow("Redemption", nominals[-1], coupons[len(coupons) - 1].date()))
        out.append((f"{sname} / {AMORTIZING} + payments", {"fixedRateLeg": amortizing, "extraFlows": extra},
                    max(nominals)))
    return out


def contexts():
    return [(ev, ref_events, todays) for ev in EVALUATION_DATES for ref_events in (False, True)
            for todays in (None, False, True)]


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for name, spec, scale in legs():
        cfs = leg(spec)
        for ev, ref_events, todays in rnd.sample(contexts(), 6):
            st = v.settings(ev, ref_events, todays)
            curve = {"referenceDate": v.enc(ev), "forward": CURVE_RATE, "dayCounter": A365,
                     "compounding": int(ql.Continuous), "frequency": int(ql.Annual)}
            crv = ql.FlatForward(ev, CURVE_RATE, day_counter(A365), ql.Continuous, ql.Annual)
            settlements, npv, bps, npvbps = [], [], [], []
            for include, sd, nd in rnd.sample(SETTLEMENTS, 3):
                settlement = ev + sd if sd is not None else ql.Date()
                npv_date = ev + nd if nd is not None else ql.Date()
                settlements.append({"includeSettlementDateFlows": include, "settlementDate": v.enc(settlement),
                                    "npvDate": v.enc(npv_date)})
                args = (cfs, crv, include, settlement, npv_date)
                npv.append(v.amount(v.attempt(lambda: ql.CashFlows.npv(*args)), scale))
                bps.append(v.amount(v.attempt(lambda: ql.CashFlows.bps(*args)), scale * 1e-4))
                pair = ql.CashFlows.npvbps(*args)  # never throws on these legs and curves
                npvbps.append([v.amount(pair[0], scale), v.amount(pair[1], scale * 1e-4)])
            out.append({
                "name": f"{name} / {ev.ISO()} {ref_events} {todays}",
                "inputs": {"settings": st, "leg": spec, "curve": curve, "settlements": settlements},
                "expected": {"startDate": v.attempt(lambda: ql.CashFlows.startDate(cfs)),
                             "maturityDate": v.attempt(lambda: ql.CashFlows.maturityDate(cfs)),
                             "npv": npv, "bps": bps, "npvbps": npvbps}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/cashflows.hpp", cases)
