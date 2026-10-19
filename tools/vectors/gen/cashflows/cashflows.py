# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CashFlows (ql/cashflows/cashflows.hpp): startDate, maturityDate, accruedPeriod,
accruedDays, accruedAmount, and npv, bps and npvbps on a yield term structure, over 47 legs; and bps on an interest
rate over the same legs. 35 are fixed-rate legs
(5 schedules x 7 variants, as in the fixed-rate coupon vectors); each schedule adds a bullet leg, its flat 3% coupons
followed by a Redemption of the notional on the last payment date, and an amortizing leg, its amortizing coupons
followed by an AmortizingPayment of each notional step on the date of the coupon before the step and a Redemption of
the last notional on the last payment date. These give the branches for cash flows that are not coupons an oracle.
The last two legs have no coupons: one has no flows at all, and one only a Redemption, so the next cash flow the
accrued functions find is no coupon. They come after the other 45, so the draws for those are unchanged.

A case is one leg under one of 6 of 18 evaluation contexts (3 evaluation dates x includeReferenceDateEvents x
includeTodaysCashFlows null, false or true), drawn with the seed, on a flat 3% continuous Actual/365 curve whose
reference date is the evaluation date. Its outputs are the leg's start and maturity dates, and its NPV, BPS and the
pair from npvbps under 3 of 6 settlement settings, also drawn: includeSettlementDateFlows (false or true; the wheel
takes no null), the settlement date (null, which means the evaluation date, or two days later) and the NPV date
(null, which means the settlement date, or a later date). NPVs are compared at the leg's largest notional and BPS at
that notional times 1e-4.

The accrued functions are read under 4 more settlement settings per case: the evaluation date (a null settlement
date), and 3 of the leg's 18 settlement dates drawn from its accrual dates (each coupon's accrual start and end, the
day after the start, the mid-point, its payment date and the days either side, three days before it, its ex-coupon
date and the day before; each other flow's date and the days either side; dates before the leg's start and after its
end; and, for a leg with fewer, dates every 91 days from 1 January 2026), each of the leg's 6 cases taking 3 of them;
includeSettlementDateFlows is drawn for each. These draws use a second generator, seeded with the seed plus 1, so the
other outputs' draws are those of the file before the accrued outputs were added. Accrued amounts are compared at the
leg's largest notional.

Each case's inputs describe the leg in full, once: "leg" holds "fixedRateLeg" (its schedule and every FixedRateLeg
setting, as in the fixed-rate coupon vectors; null for no coupons) and "extraFlows", the flows that follow the
coupons, in order, each {"type": "Redemption" or "AmortizingPayment", "amount", "date"}; "curve" holds the curve's
reference date, forward rate, day counter, compounding and frequency; "settlements" holds the three settlement
settings, in the order of the npv, bps and npvbps outputs; "accruals" holds the four settlement settings
({"includeSettlementDateFlows", "settlementDate"}), in the order of the accruedPeriod, accruedDays and accruedAmount
outputs. Calendars are {"type": <class>}, with a "market" integer for UnitedStates and UnitedKingdom; day counters
{"type": <class>}, with a "convention" integer for Thirty360 and ActualActual; enums and time units are QuantLib's
integers.

After those cases, one case per leg gives bps on an interest rate, the leg's coupons discounted period by period at a
constant rate: under one of the 18 evaluation contexts, at 4 of 7 interest rates (each compounding rule, Simple,
Compounded at three frequencies, Continuous, SimpleThenCompounded and CompoundedThenSimple, on four day counters, a
negative rate among them) and 3 of the 6 settlement settings, all drawn with a third generator, seeded with the seed
plus 2, so the earlier cases' bytes are unchanged. Its inputs are the leg, "rates" (each {"rate", "dayCounter",
"compounding", "frequency"}) and "settlements", and its output "bpsInterestRate" holds the BPS per rate and settlement,
[rate][settlement], compared at the leg's largest notional times 1e-4.

After those, one case per leg gives the functions ported with the bonds, all drawn with a fourth generator,
seeded with the seed plus 3, so the earlier cases' bytes are unchanged: under one of the 12 evaluation contexts
before 2030, at 20 settlement dates (a null one, which means the evaluation date, and 19 of the leg's accrual dates, drawn as for the
accrued functions, among them its last payment date and the day after it for a leg with flows after its coupons, so
that some dates pay several flows), each with a drawn includeSettlementDateFlows, the previous and next cash-flow
dates and amounts, the accrual period and the accrual days ("previousCashFlowDate", "nextCashFlowDate",
"previousCashFlowAmount", "nextCashFlowAmount", "accrualPeriod", "accrualDays", one element per date); and, under one
of the 6 settlement settings and at one of the interest rates above, the functions of a constant rate: the NPV, and
the yield that reproduces it, or a price of 95% of the notional where the NPV throws (NewtonSafe, accuracy 1e-10, 100 iterations, guess 0.05, C++'s defaults, where the
wheel's own default is 10000 iterations); the simple, modified and Macaulay durations (the wheel's form on an
InterestRate takes no NPV date, so these use none); the convexity and the basis-point value; the NPV, BPS, modified
duration, convexity and basis-point value again through the forms taking the rate's value, day counter, compounding
and frequency; the NPV on the flat curve with a Z-spread added in one of 4 compoundings, and the Z-spread that
reproduces it (Brent, accuracy 1e-10, 100 iterations, guess 0.0, C++'s defaults); and the at-the-money rate on the
curve, without a target NPV and with one 1% above the leg's. Its inputs are the leg, "curve", "dates" and "includes"
(the settlement dates and their flags), "settlement" (one {"includeSettlementDateFlows", "settlementDate",
"npvDate"}), "rate" ({"rate", "dayCounter", "compounding", "frequency"}), "zSpread" ({"spread", "compounding",
"frequency"}), and the targets "yieldNpv" of the yield, "zSpreadNpv" of the Z-spread and "atmNpv" of the
at-the-money rate. NPVs, amounts and basis-point values are compared at the
leg's largest notional, BPS at that notional times 1e-4.

Then 5 cases, one per schedule, whose leg puts another flow before the first coupon on its payment date: the flat 3%
coupons led by a Redemption or an AmortizingPayment of a tenth of the notional on the first coupon's payment date
("leadingFlows" in the leg, listed before the coupons), under an evaluation date at the first coupon's accrual start,
so that the accrual and accrued functions read the coupon after the flow on that date: the accrual period, accrual
days, accrued period, accrued days and accrued amount (the outputs ending in "FirstCoupon", one element per
setting) at 6 settlement
settings ("accruals": a null date, the day after the accrual start, the accrual period's mid-point, the day before the
payment date, and the payment date with includeSettlementDateFlows false and true).

And 2 cases of the solvers' edges: on an empty leg, the Z-spread for a target NPV of 50 (which QuantLib can't
bracket) and of 0 from guesses 0.01 and -0.02 (which QuantLib returns, the empty leg's NPV being 0 at every spread);
and on the "6M MF EOM / flat 3% A360 + redemption" leg, the yield from a zero guess (whose first step, a tenth of the
guess, is 0, so QuantLib can't bracket) and with one iteration, and the Z-spread with one iteration, which QuantLib
refuses. Their inputs are the leg, the curve, the settlement setting, and each call's "target", "guess" and
"maxIterations".

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
    """The leg a "leg" spec describes: its leading flows, if any, then its fixed-rate coupons, then its extra flows,
    each in order."""
    flows = [EXTRA_FLOWS[f["type"]](f["amount"], ql.Date(f["date"])) for f in spec.get("leadingFlows", [])]
    flows += list(fixed_rate_leg(spec["fixedRateLeg"])) if spec["fixedRateLeg"] is not None else []
    flows += [EXTRA_FLOWS[f["type"]](f["amount"], ql.Date(f["date"])) for f in spec["extraFlows"]]
    return ql.Leg(flows)


def flow(kind, amount, date):
    return {"type": kind, "amount": amount, "date": v.enc(date)}


def legs():
    """(name, leg spec, scale) for the 47 legs."""
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
    out.append(("no flows", {"fixedRateLeg": None, "extraFlows": []}, 1e6))
    out.append(("a redemption only",
                {"fixedRateLeg": None, "extraFlows": [flow("Redemption", 1e6, ql.Date(15, ql.June, 2028))]}, 1e6))
    return out


def accrual_dates(cfs):
    """The settlement dates at which a leg's accrued functions are read: around each coupon's accrual period and
    payment date and each other flow's date, before the leg's start and after its end, and for a leg with fewer than
    18, dates every 91 days from 1 January 2026."""
    out = set()
    for cf in cfs:
        c = ql.as_coupon(cf)
        if c is None:
            out |= {cf.date() - 1, cf.date(), cf.date() + 1}
            continue
        start, end, pay = c.accrualStartDate(), c.accrualEndDate(), c.date()
        out |= {start, start + 1, start + (end - start) // 2, end, pay - 3, pay - 1, pay, pay + 1}
        if c.exCouponDate() != ql.Date():
            out |= {c.exCouponDate() - 1, c.exCouponDate()}
    if len(cfs) > 0:
        out |= {ql.CashFlows.startDate(cfs) - 10, ql.CashFlows.maturityDate(cfs) + 10}
    n = 0
    while len(out) < 18:
        out.add(ql.Date(1, ql.January, 2026) + 91 * n)
        n += 1
    return sorted(out)


def contexts():
    return [(ev, ref_events, todays) for ev in EVALUATION_DATES for ref_events in (False, True)
            for todays in (None, False, True)]


def cases():
    rnd = random.Random(v.SEED)
    acc = random.Random(v.SEED + 1)
    out = []
    for name, spec, scale in legs():
        cfs = leg(spec)
        accrual = acc.sample(accrual_dates(cfs), 18)
        for k, (ev, ref_events, todays) in enumerate(rnd.sample(contexts(), 6)):
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
            accruals, accrued_period, accrued_days, accrued_amount = [], [], [], []
            for settlement in [ql.Date()] + accrual[3 * k:3 * k + 3]:
                include = acc.random() < 0.5
                accruals.append({"includeSettlementDateFlows": include, "settlementDate": v.enc(settlement)})
                accrued_period.append(v.attempt(lambda: ql.CashFlows.accruedPeriod(cfs, include, settlement)))
                accrued_days.append(v.attempt(lambda: ql.CashFlows.accruedDays(cfs, include, settlement)))
                accrued_amount.append(v.amount(v.attempt(lambda: ql.CashFlows.accruedAmount(cfs, include, settlement)),
                                               scale))
            out.append({
                "name": f"{name} / {ev.ISO()} {ref_events} {todays}",
                "inputs": {"settings": st, "leg": spec, "curve": curve, "settlements": settlements,
                           "accruals": accruals},
                "expected": {"startDate": v.attempt(lambda: ql.CashFlows.startDate(cfs)),
                             "maturityDate": v.attempt(lambda: ql.CashFlows.maturityDate(cfs)),
                             "npv": npv, "bps": bps, "npvbps": npvbps, "accruedPeriod": accrued_period,
                             "accruedDays": accrued_days, "accruedAmount": accrued_amount}})
    out += interest_rate_cases()
    out += later_cases()
    out += first_coupon_cases()
    out += solver_edge_cases()
    return out


# (rate, day counter, compounding, frequency) for bps on an interest rate.
RATES = [(0.03, A365, ql.Continuous, ql.Annual), (0.045, A360, ql.Compounded, ql.Semiannual),
         (0.02, THIRTY360_BB, ql.Simple, ql.Annual), (0.05, ACT_ISDA, ql.SimpleThenCompounded, ql.Quarterly),
         (0.035, A365, ql.CompoundedThenSimple, ql.Annual), (-0.002, A365, ql.Compounded, ql.Annual),
         (0.04, A360, ql.Compounded, ql.Monthly)]


def interest_rate_cases():
    """One case per leg: bps on an interest rate at 4 rates and 3 settlement settings, under a context, all drawn
    with a third generator."""
    rnd = random.Random(v.SEED + 2)
    out = []
    for name, spec, scale in legs():
        cfs = leg(spec)
        ev, ref_events, todays = rnd.choice(contexts())
        st = v.settings(ev, ref_events, todays)
        rates = [{"rate": r, "dayCounter": dc, "compounding": int(comp), "frequency": int(freq)}
                 for r, dc, comp, freq in rnd.sample(RATES, 4)]
        settlements, values = [], []
        chosen = rnd.sample(SETTLEMENTS, 3)
        for include, sd, nd in chosen:
            settlements.append({"includeSettlementDateFlows": include,
                                "settlementDate": v.enc(ev + sd if sd is not None else ql.Date()),
                                "npvDate": v.enc(ev + nd if nd is not None else ql.Date())})
        for r in rates:
            y = ql.InterestRate(r["rate"], day_counter(r["dayCounter"]), r["compounding"], r["frequency"])
            row = []
            for include, sd, nd in chosen:
                settlement = ev + sd if sd is not None else ql.Date()
                npv_date = ev + nd if nd is not None else ql.Date()
                row.append(v.amount(v.attempt(lambda: ql.CashFlows.bps(cfs, y, include, settlement, npv_date)),
                                    scale * 1e-4))
            values.append(row)
        out.append({"name": f"{name} / bps on an interest rate / {ev.ISO()} {ref_events} {todays}",
                    "inputs": {"settings": st, "leg": spec, "rates": rates, "settlements": settlements},
                    "expected": {"bpsInterestRate": values}})
    return out


# The evaluation contexts before 2030, under which most legs still have flows to pay.
LATER_CONTEXTS = [c for c in contexts() if c[0] < ql.Date(1, ql.January, 2030)]

# (spread, compounding, frequency) for the Z-spread functions.
Z_SPREADS = [(0.0125, ql.Continuous, ql.Annual), (-0.004, ql.Compounded, ql.Annual),
             (0.02, ql.Compounded, ql.Semiannual), (0.008, ql.Simple, ql.Annual)]


def settlement_dates(cfs, spec, rnd):
    """19 of the leg's accrual dates, sorted, among them its last payment date and the day after it when flows follow
    its coupons."""
    candidates = accrual_dates(cfs)
    n = 0
    while len(candidates) < 21:
        candidates = sorted(set(candidates) | {ql.Date(1, ql.January, 2026) + 91 * n})
        n += 1
    forced = []
    if spec["extraFlows"] and len(cfs) > 0:
        last = cfs[len(cfs) - 1].date()
        forced = [last, last + 1]
    rest = [d for d in candidates if d not in forced]
    return sorted(forced + rnd.sample(rest, 19 - len(forced)))


def scaled(value, scale):
    """An amount with its scale, or QuantLib's throw as it is."""
    return value if isinstance(value, dict) else v.amount(value, scale)


def later_cases():
    """One case per leg: the previous and next cash flows and the accrual period and days at 20 settlement dates, and
    the functions of a constant rate, a Z-spread and the at-the-money rate at one settlement setting, all drawn with a
    fourth generator."""
    rnd = random.Random(v.SEED + 3)
    out = []
    for name, spec, scale in legs():
        cfs = leg(spec)
        ev, ref_events, todays = rnd.choice(LATER_CONTEXTS)
        st = v.settings(ev, ref_events, todays)
        crv = ql.FlatForward(ev, CURVE_RATE, day_counter(A365), ql.Continuous, ql.Annual)
        curve = {"referenceDate": v.enc(ev), "forward": CURVE_RATE, "dayCounter": A365,
                 "compounding": int(ql.Continuous), "frequency": int(ql.Annual)}
        dates = [ql.Date()] + settlement_dates(cfs, spec, rnd)
        includes = [rnd.random() < 0.5 for _ in dates]
        expected = {key: [] for key in ("previousCashFlowDate", "nextCashFlowDate", "previousCashFlowAmount",
                                        "nextCashFlowAmount", "accrualPeriod", "accrualDays")}
        for d, include in zip(dates, includes):
            expected["previousCashFlowDate"].append(
                v.attempt(lambda: ql.CashFlows.previousCashFlowDate(cfs, include, d)))
            expected["nextCashFlowDate"].append(v.attempt(lambda: ql.CashFlows.nextCashFlowDate(cfs, include, d)))
            expected["previousCashFlowAmount"].append(
                scaled(v.attempt(lambda: ql.CashFlows.previousCashFlowAmount(cfs, include, d)), scale))
            expected["nextCashFlowAmount"].append(
                scaled(v.attempt(lambda: ql.CashFlows.nextCashFlowAmount(cfs, include, d)), scale))
            expected["accrualPeriod"].append(v.attempt(lambda: ql.CashFlows.accrualPeriod(cfs, include, d)))
            expected["accrualDays"].append(v.attempt(lambda: ql.CashFlows.accrualDays(cfs, include, d)))

        include, sd, nd = rnd.choice(SETTLEMENTS)
        settlement = ev + sd if sd is not None else ql.Date()
        npv_date = ev + nd if nd is not None else ql.Date()
        r, rdc, comp, freq = rnd.choice(RATES)
        dc = day_counter(rdc)
        y = ql.InterestRate(r, dc, comp, freq)
        z, zcomp, zfreq = rnd.choice(Z_SPREADS)
        at = (include, settlement, npv_date)

        npv = v.attempt(lambda: ql.CashFlows.npv(cfs, y, *at))
        expected["npvYield"] = scaled(npv, scale)
        # the yield that reproduces the NPV, or a price of 95% of the notional where the NPV throws
        target = npv if isinstance(npv, float) else 0.95 * scale
        expected["yield"] = v.attempt(
            lambda: ql.CashFlows.yieldRate(cfs, target, dc, comp, freq, *at, 1.0e-10, 100, 0.05))
        expected["durationSimple"] = v.attempt(
            lambda: ql.CashFlows.duration(cfs, y, ql.Duration.Simple, include, settlement))
        expected["durationModified"] = v.attempt(
            lambda: ql.CashFlows.duration(cfs, y, ql.Duration.Modified, include, settlement))
        expected["durationMacaulay"] = v.attempt(
            lambda: ql.CashFlows.duration(cfs, y, ql.Duration.Macaulay, include, settlement))
        expected["convexity"] = v.attempt(lambda: ql.CashFlows.convexity(cfs, y, *at))
        expected["basisPointValue"] = scaled(v.attempt(lambda: ql.CashFlows.basisPointValue(cfs, y, *at)), scale)
        expected["npvRate"] = scaled(v.attempt(lambda: ql.CashFlows.npv(cfs, r, dc, comp, freq, *at)), scale)
        expected["bpsRate"] = scaled(v.attempt(lambda: ql.CashFlows.bps(cfs, r, dc, comp, freq, *at)),
                                       scale * 1e-4)
        expected["durationRate"] = v.attempt(
            lambda: ql.CashFlows.duration(cfs, r, dc, comp, freq, ql.Duration.Modified, *at))
        expected["convexityRate"] = v.attempt(lambda: ql.CashFlows.convexity(cfs, r, dc, comp, freq, *at))
        expected["basisPointValueRate"] = scaled(
            v.attempt(lambda: ql.CashFlows.basisPointValue(cfs, r, dc, comp, freq, *at)), scale)
        z_npv = ql.CashFlows.npv(cfs, crv, z, zcomp, zfreq, *at)  # never throws on these legs and curves
        expected["zSpreadNpv"] = scaled(z_npv, scale)
        expected["zSpread"] = v.attempt(
            lambda: ql.CashFlows.zSpread(cfs, z_npv, crv, zcomp, zfreq, *at, 1.0e-10, 100, 0.0))
        expected["atmRate"] = v.attempt(lambda: ql.CashFlows.atmRate(cfs, crv, *at))
        atm_npv = ql.CashFlows.npv(cfs, crv, *at) * 1.01
        expected["atmRateNpv"] = v.attempt(lambda: ql.CashFlows.atmRate(cfs, crv, *at, atm_npv))
        out.append({"name": f"{name} / later functions / {ev.ISO()} {ref_events} {todays}",
                    "inputs": {"settings": st, "leg": spec, "curve": curve, "dates": v.enc(dates),
                               "includes": includes,
                               "settlement": {"includeSettlementDateFlows": include,
                                              "settlementDate": v.enc(settlement), "npvDate": v.enc(npv_date)},
                               "rate": {"rate": r, "dayCounter": rdc, "compounding": int(comp),
                                        "frequency": int(freq)},
                               "zSpread": {"spread": z, "compounding": int(zcomp), "frequency": int(zfreq)},
                               "yieldNpv": target, "zSpreadNpv": z_npv, "atmNpv": atm_npv},
                    "expected": expected})
    return out


def first_coupon_cases():
    """One case per schedule: a leg whose first coupon's payment date pays another flow first, read by the accrual and
    accrued functions at 6 settlement settings before and on that date."""
    out = []
    flat = dict(VARIANTS)[FLAT]
    for (sname, sspec), kind in zip(SCHEDULES, ["Redemption", "AmortizingPayment", "Redemption", "AmortizingPayment",
                                                "Redemption"]):
        lspec = dict(flat, schedule=sspec)
        coupons = fixed_rate_leg(lspec)
        first = ql.as_coupon(coupons[0])
        start, pay = first.accrualStartDate(), first.date()
        spec = {"leadingFlows": [flow(kind, 1e5, pay)], "fixedRateLeg": lspec, "extraFlows": []}
        cfs = leg(spec)
        st = v.settings(start)
        accruals = [(False, ql.Date()), (False, start + 1), (True, start + (first.accrualEndDate() - start) // 2),
                    (False, pay - 1), (False, pay), (True, pay)]
        expected = {key: [] for key in ("accrualPeriodFirstCoupon", "accrualDaysFirstCoupon",
                                        "accruedPeriodFirstCoupon", "accruedDaysFirstCoupon",
                                        "accruedAmountFirstCoupon")}
        for include, d in accruals:
            expected["accrualPeriodFirstCoupon"].append(v.attempt(lambda: ql.CashFlows.accrualPeriod(cfs, include, d)))
            expected["accrualDaysFirstCoupon"].append(v.attempt(lambda: ql.CashFlows.accrualDays(cfs, include, d)))
            expected["accruedPeriodFirstCoupon"].append(v.attempt(lambda: ql.CashFlows.accruedPeriod(cfs, include, d)))
            expected["accruedDaysFirstCoupon"].append(v.attempt(lambda: ql.CashFlows.accruedDays(cfs, include, d)))
            expected["accruedAmountFirstCoupon"].append(
                scaled(v.attempt(lambda: ql.CashFlows.accruedAmount(cfs, include, d)), 1e6))
        out.append({"name": f"{sname} / {FLAT} led by a {kind} / first coupon after another flow",
                    "inputs": {"settings": st, "kind": "firstCoupon", "leg": spec,
                               "accruals": [{"includeSettlementDateFlows": i, "settlementDate": v.enc(d)}
                                            for i, d in accruals]},
                    "expected": expected})
    return out


def solver_edge_cases():
    """The Z-spread on an empty leg, and the yield and Z-spread from a zero guess or with one iteration."""
    ev = ql.Date(15, ql.October, 2026)
    crv = ql.FlatForward(ev, CURVE_RATE, day_counter(A365), ql.Continuous, ql.Annual)
    curve = {"referenceDate": v.enc(ev), "forward": CURVE_RATE, "dayCounter": A365,
             "compounding": int(ql.Continuous), "frequency": int(ql.Annual)}
    settlement = {"includeSettlementDateFlows": False, "settlementDate": None, "npvDate": None}
    out = []

    st = v.settings(ev)
    empty = {"fixedRateLeg": None, "extraFlows": []}
    calls = [{"target": 50.0, "guess": 0.0, "maxIterations": 100}, {"target": 0.0, "guess": 0.01, "maxIterations": 100},
             {"target": 0.0, "guess": -0.02, "maxIterations": 100}]
    values = [v.attempt(lambda c=c: ql.CashFlows.zSpread(ql.Leg(), c["target"], crv, ql.Continuous, ql.Annual, False,
                                                         ql.Date(), ql.Date(), 1.0e-10, c["maxIterations"],
                                                         c["guess"]))
              for c in calls]
    out.append({"name": "no flows / solver edges",
                "inputs": {"settings": st, "kind": "solverEdges", "leg": empty, "curve": curve,
                           "settlement": settlement, "zSpreadCalls": calls, "yieldCalls": []},
                "expected": {"zSpreadEdges": values, "yieldEdges": []}})

    st = v.settings(ev)
    name, spec, scale = [x for x in legs() if x[0] == f"6M MF EOM / {FLAT} + redemption"][0]
    cfs = leg(spec)
    y = ql.InterestRate(0.04, day_counter(A360), ql.Compounded, ql.Annual)
    target = ql.CashFlows.npv(cfs, y, False)
    yield_calls = [{"target": target, "guess": 0.0, "maxIterations": 100},
                   {"target": target, "guess": 0.05, "maxIterations": 1}]
    yields = [v.attempt(lambda c=c: ql.CashFlows.yieldRate(cfs, c["target"], day_counter(A360), ql.Compounded,
                                                           ql.Annual, False, ql.Date(), ql.Date(), 1.0e-10,
                                                           c["maxIterations"], c["guess"]))
              for c in yield_calls]
    z_target = ql.CashFlows.npv(cfs, crv, 0.01, ql.Continuous, ql.Annual, False)
    z_calls = [{"target": z_target, "guess": 0.0, "maxIterations": 1}]
    zs = [v.attempt(lambda c=c: ql.CashFlows.zSpread(cfs, c["target"], crv, ql.Continuous, ql.Annual, False,
                                                     ql.Date(), ql.Date(), 1.0e-10, c["maxIterations"], c["guess"]))
          for c in z_calls]
    out.append({"name": f"{name} / solver edges",
                "inputs": {"settings": st, "kind": "solverEdges", "leg": spec, "curve": curve,
                           "settlement": settlement, "zSpreadCalls": z_calls,
                           "yieldCalls": [dict(c, rate={"rate": 0.04, "dayCounter": A360,
                                                        "compounding": int(ql.Compounded),
                                                        "frequency": int(ql.Annual)}) for c in yield_calls]},
                "expected": {"zSpreadEdges": zs, "yieldEdges": yields}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/cashflows.hpp", cases)
