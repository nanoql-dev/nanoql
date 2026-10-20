# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's credit default swap (ql/instruments/creditdefaultswap.hpp), through the wheel's
CreditDefaultSwap, its two constructors, the mid-point engine and cdsMaturity.

- "swap" cases: swaps under the 2015, 2009 and older CDS rules and pre-2009 IMM and forward schedules, buying or
  selling protection, quoted as a running spread or an upfront, with or without a protection start, a trade date, an
  upfront date, the accrual rebate, cash settlement days and a last-period day counter, live or expired: the
  protection start and end dates, the trade date, the upfront payment's date and amount, the accrual rebate's (null
  without one), the coupons' dates, accrual dates and amounts, whether the swap has expired, and the mid-point
  engine's results on a flat 2% hazard rate and a flat 3% discount curve. On a live swap whose coupons the ISDA engine
  accepts, the implied hazard rate of the swap's mid-point NPV and the conventional spread at a 40% recovery, under
  both models.
- "maturity" cases: cdsMaturity on trade dates around the rolls, for tenors of 0 to 10 years, under the three CDS
  rules; null where the 2015 rule gives no maturity; and its refusals.

The implied hazard rate and the conventional spread are solved, so the stability filter of the S-infl plan's SID20
(the S7 plan's S7R6) runs on them: each is solved again with the target NPV, the discount rate and the recovery moved
one ulp up, then down, together and each alone, and written only where every move leaves it within
1e-12 x max(|a|, |b|, 1); a dropped one is written null and listed in the case's inputs "dropped". On an expired swap,
or one with a zero running spread, QuantLib's implied quantities return values it hasn't solved, which nanoql refuses
(tests/NanoQL.Tests/Divergences/CdsImpliedHazardRateExpiredTests.cs, CdsConventionalSpreadNullTests.cs), so no case
asks for them there; nor on a weekend, where QuantLib's ISDA model refuses its own curve
(tests/NanoQL.Tests/Divergences/IsdaImpliedWeekendTests.cs).

A case's inputs hold its settings and the swap spec {"side", "notional", "upfront" (null for the running-spread
constructor), "spread", "schedule", "paymentConvention", "dayCounter", "settlesAccrual", "paysAtDefaultTime",
"protectionStart", "upfrontDate", "lastPeriodDayCounter", "rebatesAccrual", "tradeDate", "cashSettlementDays"}, the
curve specs of tools/vectors/gen/termstructures/defaulttermstructure.py and the recovery. A refused value is
{"throws": true}. Read back by tests/NanoQL.Tests/Vectors/Instruments/CreditDefaultSwapVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_defaulttermstructure",
                                               os.path.join(v.GEN_ROOT, "termstructures", "defaulttermstructure.py"))
dts = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dts)

TODAY = dts.TODAY
BUYER, SELLER = int(ql.Protection.Buyer), int(ql.Protection.Seller)
M, Y = int(ql.Months), int(ql.Years)
STABLE = 1e-12


def schedule_spec(eff, mat, tenor, cal, rule, convention=ql.Following, termination=ql.Unadjusted):
    return {"effectiveDate": v.enc(eff), "terminationDate": v.enc(mat), "tenor": [tenor[0], int(tenor[1])],
            "calendar": cal, "convention": int(convention), "terminationDateConvention": int(termination),
            "rule": int(rule)}


def schedule(spec):
    return ql.Schedule(ql.Date(spec["effectiveDate"]), ql.Date(spec["terminationDate"]),
                       ql.Period(spec["tenor"][0], spec["tenor"][1]), dts.calendar(spec["calendar"]),
                       spec["convention"], spec["terminationDateConvention"], spec["rule"], False)


def cds_spec(side, notional, spread, sched, upfront=None, convention=ql.Following, dc=dts.A360, settles=True,
             pays=True, protection_start=None, upfront_date=None, last_dc=None, rebates=True, trade_date=None,
             cash_days=3):
    return {"side": side, "notional": notional, "upfront": upfront, "spread": spread, "schedule": sched,
            "paymentConvention": int(convention), "dayCounter": dc, "settlesAccrual": settles,
            "paysAtDefaultTime": pays, "protectionStart": v.enc(protection_start), "upfrontDate": v.enc(upfront_date),
            "lastPeriodDayCounter": last_dc, "rebatesAccrual": rebates, "tradeDate": v.enc(trade_date),
            "cashSettlementDays": cash_days}


def date_or_null(serial):
    return ql.Date(serial) if serial is not None else ql.Date()


def make_cds(spec):
    """The wheel's swap. The wheel takes no keyword and has no empty day counter, so a spec without a last-period day
    counter must leave the arguments after it at their defaults, and the call stops before them."""
    sched = schedule(spec["schedule"])
    dc = dts.day_counter(spec["dayCounter"])
    head = [spec["side"], spec["notional"]] + ([] if spec["upfront"] is None else [spec["upfront"]])
    middle = [spec["spread"], sched, spec["paymentConvention"], dc, spec["settlesAccrual"], spec["paysAtDefaultTime"],
              date_or_null(spec["protectionStart"])]
    if spec["upfront"] is not None:
        middle.append(date_or_null(spec["upfrontDate"]))
    if spec["lastPeriodDayCounter"] is None:
        assert spec["rebatesAccrual"] and spec["tradeDate"] is None and spec["cashSettlementDays"] == 3
        return ql.CreditDefaultSwap(*head, *middle)
    return ql.CreditDefaultSwap(*head, *middle, None, dts.day_counter(spec["lastPeriodDayCounter"]),
                                spec["rebatesAccrual"], date_or_null(spec["tradeDate"]), spec["cashSettlementDays"])


def inspectors(cds):
    """The swap's inspectors, as every CDS file writes them."""
    coupons = [ql.as_coupon(c) for c in cds.coupons()]
    rebate = cds.accrualRebate()
    return {"protectionStartDate": v.enc(cds.protectionStartDate()), "protectionEndDate": v.enc(cds.protectionEndDate()),
            "tradeDate": v.enc(cds.tradeDate()), "cashSettlementDays": cds.cashSettlementDays(),
            "upfront": v.enc(cds.upfront()), "upfrontDate": v.enc(cds.upfrontPayment().date()),
            "upfrontAmount": cds.upfrontPayment().amount(),
            "rebateDate": None if rebate is None else v.enc(rebate.date()),
            "rebateAmount": None if rebate is None else rebate.amount(),
            "couponDates": v.enc([c.date() for c in coupons]),
            "accrualStartDates": v.enc([c.accrualStartDate() for c in coupons]),
            "accrualEndDates": v.enc([c.accrualEndDate() for c in coupons]),
            "couponAmounts": [c.amount() for c in coupons], "isExpired": cds.isExpired()}


def results(cds, scale):
    """The engine's results, as every CDS file writes them: null where QuantLib's are "not available", each
    {"throws": true} where the engine refuses; amounts at the swap's notional. An expired swap's accrual-rebate NPV,
    which QuantLib leaves unset and reads uninitialized (tests/NanoQL.Tests/Divergences/
    CdsExpiredAccrualRebateTests.cs), isn't written."""
    def value(f):
        try:
            return v.result(f)
        except RuntimeError:
            return dict(v.THROWS)

    def amount(f):
        x = value(f)
        return x if isinstance(x, dict) else v.amount(x, scale)
    out = {"NPV": amount(cds.NPV), "couponLegNPV": amount(cds.couponLegNPV),
           "defaultLegNPV": amount(cds.defaultLegNPV), "upfrontNPV": amount(cds.upfrontNPV),
           "accrualRebateNPV": amount(cds.accrualRebateNPV), "fairSpread": value(cds.fairSpread),
           "fairUpfront": value(cds.fairUpfront), "couponLegBPS": amount(cds.couponLegBPS),
           "upfrontBPS": amount(cds.upfrontBPS)}
    if cds.isExpired():
        del out["accrualRebateNPV"]
    return out


def stable(base, moved):
    if isinstance(base, dict) or isinstance(moved, dict):
        return base == moved
    return abs(base - moved) <= STABLE * max(abs(base), abs(moved), 1.0)


def moves(inputs):
    out = [[math.nextafter(x, math.inf) for x in inputs], [math.nextafter(x, -math.inf) for x in inputs]]
    for i in range(len(inputs)):
        for direction in (math.inf, -math.inf):
            moved = list(inputs)
            moved[i] = math.nextafter(inputs[i], direction)
            if moved not in out:
                out.append(moved)
    return out


EXPIRED = ql.Date(20, ql.March, 2021), ql.Date(20, ql.December, 2023)
SWAPS = [
    ("buyer 5Y 2015 rule", cds_spec(BUYER, 1e7, 0.01, schedule_spec(TODAY, ql.Date(20, ql.December, 2028), (3, M),
                                                                     dts.WEEKENDS_ONLY, ql.DateGeneration.CDS2015),
                                    last_dc=dts.A360_INC)),
    ("seller 3Y 2009 rule on TARGET, protection start given",
     cds_spec(SELLER, 5e6, 0.0175, schedule_spec(TODAY, ql.Date(20, ql.March, 2027), (3, M), dts.TARGET,
                                                 ql.DateGeneration.CDS),
              protection_start=TODAY + 1, last_dc=dts.A360_INC)),
    ("buyer upfront 2%, trade date given",
     cds_spec(BUYER, 1e7, 0.05, schedule_spec(TODAY, ql.Date(20, ql.June, 2029), (3, M), dts.WEEKENDS_ONLY,
                                              ql.DateGeneration.CDS2015), upfront=0.02, trade_date=TODAY - 3,
              last_dc=dts.A360_INC)),
    ("seller upfront -1.5%, upfront date given",
     cds_spec(SELLER, 2e6, 0.01, schedule_spec(TODAY, ql.Date(20, ql.December, 2026), (3, M), dts.WEEKENDS_ONLY,
                                               ql.DateGeneration.CDS), upfront=-0.015,
              upfront_date=ql.Date(22, ql.January, 2024))),
    ("old rule 1Y from tomorrow",
     cds_spec(BUYER, 1e6, 0.02, schedule_spec(TODAY + 1, ql.Date(20, ql.March, 2025), (3, M), dts.TARGET,
                                              ql.DateGeneration.OldCDS), protection_start=TODAY + 1)),
    ("IMM forward schedule, A365",
     cds_spec(SELLER, 1e6, 0.008, schedule_spec(ql.Date(20, ql.December, 2023), ql.Date(20, ql.December, 2028),
                                                (6, M), dts.TARGET, ql.DateGeneration.TwentiethIMM),
              dc=dts.A365)),
    ("no accrual rebate, no cash settlement days",
     cds_spec(BUYER, 1e7, 0.01, schedule_spec(TODAY, ql.Date(20, ql.December, 2028), (3, M), dts.WEEKENDS_ONLY,
                                              ql.DateGeneration.CDS2015), rebates=False, cash_days=0,
              last_dc=dts.A360_INC)),
    ("accrual not settled, paid at the period's end",
     cds_spec(BUYER, 1e7, 0.012, schedule_spec(TODAY, ql.Date(20, ql.December, 2028), (3, M), dts.WEEKENDS_ONLY,
                                               ql.DateGeneration.CDS2015), settles=False, pays=False)),
    ("30/360 coupons", cds_spec(SELLER, 1e6, 0.01, schedule_spec(TODAY, ql.Date(20, ql.June, 2027), (3, M),
                                                                  dts.TARGET, ql.DateGeneration.CDS),
                                dc=dts.THIRTY360_BB)),
    ("expired", cds_spec(BUYER, 1e6, 0.01, schedule_spec(EXPIRED[0], EXPIRED[1], (3, M), dts.TARGET,
                                                         ql.DateGeneration.CDS))),
]
EVALUATIONS = [TODAY, ql.Date(20, ql.March, 2024)]
DISCOUNT = 0.03
HAZARD = 0.02
RECOVERY = 0.4


def implied(spec, ev, target, rate, recovery):
    """The implied hazard rates of a target NPV and the conventional spreads, under both models."""
    out = {}
    with v.case_settings(ev):
        cds = make_cds(spec)
        disc = dts.discount_handle(dts.discount_spec(ev, rate))
        for name, model in (("Midpoint", ql.CreditDefaultSwap.Midpoint), ("ISDA", ql.CreditDefaultSwap.ISDA)):
            out["impliedHazardRate" + name] = v.attempt(
                lambda m=model: cds.impliedHazardRate(target, disc, ql.Actual365Fixed(), recovery, 1e-8, m))
            out["conventionalSpread" + name] = v.attempt(
                lambda m=model: cds.conventionalSpread(recovery, disc, ql.Actual365Fixed(), m))
    return out


def swap_cases():
    out = []
    total = [0, 0]
    k = 0
    for label, spec in SWAPS:
        for ev in EVALUATIONS:
            with v.case_settings(ev) as st:
                cds = make_cds(spec)
                credit = dts.flat_spec(ev, HAZARD)
                disc = dts.discount_spec(ev, DISCOUNT)
                cds.setPricingEngine(ql.MidPointCdsEngine(dts.credit_handle(credit), RECOVERY,
                                                          dts.discount_handle(disc)))
                expected = inspectors(cds)
                expected.update(results(cds, spec["notional"]))
                inputs = {"settings": st, "swap": spec, "credit": credit, "discount": disc, "recoveryRate": RECOVERY,
                          "implied": False}
                target = cds.NPV()
                live = not cds.isExpired() and spec["spread"] != 0.0 and ev.weekday() not in (ql.Saturday, ql.Sunday)
            if live:
                base = implied(spec, ev, target, DISCOUNT, RECOVERY)
                moved = [implied(spec, ev, m[0], m[1], m[2]) for m in moves([target, DISCOUNT, RECOVERY])]
                dropped = [name for name in base if not all(stable(base[name], m[name]) for m in moved)]
                for name in dropped:
                    base[name] = None
                total[0] += len(dropped)
                total[1] += len(base)
                print(f"swap {label} {ev.ISO()}: dropped {len(dropped)} of {len(base)} outputs")
                expected.update(base)
                inputs.update({"implied": True, "target": target, "dropped": dropped})
            out.append({"name": f"swap {label} / {ev.ISO()} / {k}", "inputs": inputs, "expected": expected})
            k += 1
    print(f"instruments/creditdefaultswap: dropped {total[0]} of {total[1]} outputs")
    return out


TRADE_DATES = [ql.Date(19, ql.March, 2024), ql.Date(20, ql.March, 2024), ql.Date(21, ql.March, 2024),
               ql.Date(15, ql.May, 2024), ql.Date(20, ql.June, 2024), ql.Date(19, ql.September, 2024),
               ql.Date(20, ql.December, 2024), ql.Date(27, ql.December, 2024)]
TENORS = [(0, M), (3, M), (6, M), (1, Y), (5, Y), (10, Y)]
RULES = [ql.DateGeneration.CDS2015, ql.DateGeneration.CDS, ql.DateGeneration.OldCDS]
REFUSED = [("forward rule", ql.Date(15, ql.May, 2024), (5, Y), ql.DateGeneration.Forward),
           ("two months", ql.Date(15, ql.May, 2024), (2, M), ql.DateGeneration.CDS),
           ("weeks", ql.Date(15, ql.May, 2024), (13, int(ql.Weeks)), ql.DateGeneration.CDS2015),
           ("back three months", ql.Date(15, ql.May, 2024), (-3, M), ql.DateGeneration.CDS)]


def maturity_cases():
    out = []
    k = 0
    with v.case_settings(TODAY) as st:
        for d in TRADE_DATES:
            for n, unit in TENORS:
                for rule in RULES:
                    out.append({"name": f"maturity {d.ISO()} {n}{'M' if unit == M else 'Y'} {int(rule)} / {k}",
                                "inputs": {"settings": st, "tradeDate": v.enc(d), "tenor": [n, unit],
                                           "rule": int(rule)},
                                "expected": {"maturity": v.attempt(
                                    lambda: ql.cdsMaturity(d, ql.Period(n, unit), rule))}})
                    k += 1
        for label, d, (n, unit), rule in REFUSED:
            out.append({"name": f"maturity refused {label} / {k}",
                        "inputs": {"settings": st, "tradeDate": v.enc(d), "tenor": [n, unit], "rule": int(rule)},
                        "expected": {"maturity": v.attempt(lambda: ql.cdsMaturity(d, ql.Period(n, unit), rule))}})
            k += 1
    return out


def make_cases():
    return swap_cases() + maturity_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/creditdefaultswap.hpp", make_cases)
