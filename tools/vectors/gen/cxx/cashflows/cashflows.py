# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's CashFlows (ql/cashflows/cashflows.hpp) that the wheel doesn't reach, each
output computed by the C++ harness; with ql/cashflow.hpp's earlier_than<CashFlow> and the two derivatives of
InterestRate's discount factor (ql/interestrate.hpp), whose only QuantLib callers are CashFlows' duration and
convexity.

The legs are tools/vectors/gen/cashflows/cashflows.py's 47, imported, and 4 of fixed-rate coupons given one by one:
two coupons on one payment date with the same nominal, accrual period and day counter, whose rates QuantLib sums; two
with different nominals, and two with different day counters, which it refuses to aggregate ("cannot aggregate two
different coupons on ..."); and a coupon and a redemption on one date followed by a redemption alone, where the next
coupon rate after the coupon's date finds no coupon ("no coupon paid at cashflow date ..."). A case is one leg under
one of the wheel generator's 18 evaluation contexts, drawn with the seed, with:

- at 3 settlement settings (a null date, which means the evaluation date, with a null includeSettlementDateFlows,
  then 2 of the leg's accrual dates, as the wheel generator draws them, each with a null, false or true flag), the
  leg's isExpired, the positions of its previous and next cash flows (previousCashFlow's rend() as -1, nextCashFlow's
  end() as the leg's size), its previous and next coupon rates, its nominal, and the accrual start and end dates and
  reference period start and end of the coupon its accrual functions read (one element per setting);
- at one of the wheel generator's 6 settlement settings and one of its interest rates, the yield value of a basis
  point through both of its forms, and the yield that reproduces the leg's NPV at that rate (the wheel's, or a price
  of 95% of the notional where the NPV throws), solved by Brent (accuracy 1e-10, 100 evaluations, guess 0.05);
- earlier_than<CashFlow> on 4 pairs of the leg's flows by position, for a leg of two flows or more.

Then one case per interest rate (each compounding rule, the two mixed ones among them, at several frequencies, and
OtherFrequency) with the first and second derivatives of its discount factor at 7 times, a negative one among them,
which QuantLib refuses, as it refuses the mixed rules ("... is not supported for direct calculations").

Then 5 cases, one per schedule of the wheel generator, whose leg puts another flow before the first coupon on its
payment date: the flat 3% coupons led by an AmortizingPayment or a Redemption of a tenth of the notional on the first
coupon's payment date ("leadingFlows", listed before the coupons), under an evaluation date at the first coupon's
accrual start, so that the functions that read the first coupon on the next payment date skip the flow: the nominal,
the accrual start and end dates, the reference period's start and end, and the next coupon rate, at 6 settlement
settings (a null date, the day after the accrual start, the accrual period's mid-point, the day before the payment
date, and the payment date with includeSettlementDateFlows false and true). Their inputs are "kind"
("firstCoupon"), the leg and "settlements".

A refused value is {"throws": true}. Each case's inputs hold its settings, the leg ("leg": the wheel generator's
{"fixedRateLeg", "extraFlows"}, or {"fixedRateLeg": null, "coupons", "extraFlows"} with the coupons'
{"paymentDate", "nominal", "rate", "dayCounter", "accrualStartDate", "accrualEndDate", "refPeriodStart",
"refPeriodEnd", "exCouponDate"}, a simple annual rate on the day counter), "settlements" ({"includeSettlementDateFlows",
"settlementDate"}), "settlement" ({"includeSettlementDateFlows", "settlementDate", "npvDate"}), "rate", "yieldNpv"
and "pairs"; or "rate" and "times" for a derivatives case. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Coupons/CashFlowsCxxVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_cashflows", os.path.join(v.GEN_ROOT, "cashflows", "cashflows.py"))
cf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cf)

A360, A365, ACT_ISDA = cf.A360, cf.A365, cf.ACT_ISDA
D = ql.Date


def coupon(pay, nominal, rate, dc, start, end):
    return {"paymentDate": v.enc(pay), "nominal": nominal, "rate": rate, "dayCounter": dc,
            "accrualStartDate": v.enc(start), "accrualEndDate": v.enc(end), "refPeriodStart": None,
            "refPeriodEnd": None, "exCouponDate": None}


def coupon_legs():
    """(name, leg spec, scale) for the 4 legs of coupons given one by one."""
    s1, p1, p2 = D(15, 1, 2026), D(15, 7, 2026), D(15, 1, 2027)
    out = [
        ("two coupons on one date, the same terms",
         [coupon(p1, 1e6, 0.03, A360, s1, p1), coupon(p1, 1e6, 0.01, A360, s1, p1),
          coupon(p2, 1e6, 0.03, A360, p1, p2)], []),
        ("two coupons on one date, different nominals",
         [coupon(p1, 1e6, 0.03, A360, s1, p1), coupon(p1, 5e5, 0.03, A360, s1, p1)], []),
        ("two coupons on one date, different day counters",
         [coupon(p1, 1e6, 0.03, A360, s1, p1), coupon(p1, 1e6, 0.03, A365, s1, p1)], []),
        ("a coupon and a redemption, then a redemption alone",
         [coupon(p1, 1e6, 0.03, A365, s1, p1)],
         [cf.flow("Redemption", 5e5, p1), cf.flow("Redemption", 5e5, p2)]),
    ]
    return [(name, {"fixedRateLeg": None, "coupons": coupons, "extraFlows": extra}, 1e6)
            for name, coupons, extra in out]


def wheel_leg(spec):
    """The wheel's leg a spec describes, for the NPV the yield reproduces."""
    flows = list(cf.fixed_rate_leg(spec["fixedRateLeg"])) if spec["fixedRateLeg"] is not None else []
    for c in spec.get("coupons", []):
        flows.append(ql.FixedRateCoupon(ql.Date(c["paymentDate"]), c["nominal"], c["rate"],
                                        cf.day_counter(c["dayCounter"]), ql.Date(c["accrualStartDate"]),
                                        ql.Date(c["accrualEndDate"])))
    flows += [cf.EXTRA_FLOWS[f["type"]](f["amount"], ql.Date(f["date"])) for f in spec["extraFlows"]]
    return ql.Leg(flows)


INSPECTORS = ["isExpired", "previousCashFlow", "nextCashFlow", "previousCouponRate", "nextCouponRate", "nominal",
              "accrualStartDate", "accrualEndDate", "referencePeriodStart", "referencePeriodEnd"]

# (rate, day counter, compounding, frequency) for the discount factor's derivatives.
DERIVATIVE_RATES = [(0.03, A365, ql.Simple, ql.Annual), (0.02, A360, ql.Simple, ql.NoFrequency),
                    (0.03, A365, ql.Compounded, ql.Annual), (0.045, A360, ql.Compounded, ql.Semiannual),
                    (-0.01, A365, ql.Compounded, ql.Quarterly), (0.12, A365, ql.Compounded, ql.Monthly),
                    (0.03, A365, ql.Compounded, ql.OtherFrequency), (0.03, A365, ql.Continuous, ql.Annual),
                    (-0.005, A360, ql.Continuous, ql.NoFrequency), (0.05, ACT_ISDA, ql.SimpleThenCompounded,
                                                                     ql.Quarterly),
                    (0.035, A365, ql.CompoundedThenSimple, ql.Annual)]
TIMES = [0.0, 0.25, 1.0, 2.5, 10.0, 30.0, -1.0]


def rate_spec(r, dc, comp, freq):
    return {"rate": r, "dayCounter": dc, "compounding": int(comp), "frequency": int(freq)}


def cases():
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        out = []
        for name, spec, scale in cf.legs() + coupon_legs():
            ev, ref_events, todays = rnd.choice(cf.contexts())
            wheel = wheel_leg(spec)
            with H.case_settings(v.enc(ev), include_reference_date_events=ref_events,
                                 include_todays_cash_flows=todays) as st:
                dates = rnd.sample(cf.accrual_dates(wheel), 2)
                settlements = [(None, None)] + [(rnd.choice([None, False, True]), v.enc(d)) for d in dates]
                expected = {m: [] for m in INSPECTORS}
                for include, d in settlements:
                    for m in INSPECTORS:
                        expected[m].append(v.attempt(lambda m=m: H.call(f"CashFlows::{m}", spec, include, d)))

                include, sd, nd = rnd.choice(cf.SETTLEMENTS)
                settlement = v.enc(ev + sd) if sd is not None else None
                npv_date = v.enc(ev + nd) if nd is not None else None
                r, rdc, comp, freq = rnd.choice(cf.RATES)
                rate = rate_spec(r, rdc, comp, freq)
                expected["yieldValueBasisPoint"] = v.attempt(lambda: H.call(
                    "CashFlows::yieldValueBasisPoint(Leg,InterestRate)", spec, rate, include, settlement, npv_date))
                expected["yieldValueBasisPointRate"] = v.attempt(lambda: H.call(
                    "CashFlows::yieldValueBasisPoint(Leg,Rate,DayCounter,Compounding,Frequency)", spec, r, rdc,
                    int(comp), int(freq), include, settlement, npv_date))
                # the wheel's NPV at the rate, under the same settings
                assert v.settings(ev, ref_events, todays) == st
                npv = v.attempt(lambda: ql.CashFlows.npv(
                    wheel, ql.InterestRate(r, cf.day_counter(rdc), comp, freq), include,
                    ql.Date(settlement) if settlement is not None else ql.Date(),
                    ql.Date(npv_date) if npv_date is not None else ql.Date()))
                target = npv if isinstance(npv, float) else 0.95 * scale
                expected["yieldBrent"] = v.attempt(lambda: H.call(
                    "CashFlows::yield<Brent>", spec, target, rdc, int(comp), int(freq), include, settlement,
                    npv_date, 1.0e-10, 100, 0.05))

                n = len(wheel)
                pairs = [[0, 1], [1, 0], [0, 0], [n - 1, 0]] if n >= 2 else []
                expected["earlierThan"] = [H.call("earlier_than<CashFlow>", spec, i, j) for i, j in pairs]
                out.append({"name": f"{name} / {ev.ISO()} {ref_events} {todays}",
                            "inputs": {"settings": st, "leg": spec,
                                       "settlements": [{"includeSettlementDateFlows": i, "settlementDate": d}
                                                       for i, d in settlements],
                                       "settlement": {"includeSettlementDateFlows": include,
                                                      "settlementDate": settlement, "npvDate": npv_date},
                                       "rate": rate, "yieldNpv": target, "pairs": pairs},
                            "expected": expected})

        st = H.settings(v.enc(D(15, 10, 2026)))
        for r, rdc, comp, freq in DERIVATIVE_RATES:
            rate = rate_spec(r, rdc, comp, freq)
            out.append({"name": f"discount factor derivatives / {r} {rdc['type']} {int(comp)} {int(freq)}",
                        "inputs": {"settings": st, "rate": rate, "times": TIMES},
                        "expected": {
                            "discountFactorFirstDerivative": [v.attempt(lambda t=t: H.call(
                                "InterestRate::discountFactorFirstDerivative(Time)", rate, t)) for t in TIMES],
                            "discountFactorSecondDerivative": [v.attempt(lambda t=t: H.call(
                                "InterestRate::discountFactorSecondDerivative(Time)", rate, t)) for t in TIMES]}})
        out += first_coupon_cases(H)
        return out


FIRST_COUPON = ["nominal", "accrualStartDate", "accrualEndDate", "referencePeriodStart", "referencePeriodEnd",
                "nextCouponRate"]


def first_coupon_cases(H):
    """One case per schedule of the wheel generator: its flat 3% coupons led by another flow on the first coupon's
    payment date, read at 6 settlement settings before and on that date."""
    out = []
    flat = dict(cf.VARIANTS)[cf.FLAT]
    for (sname, sspec), kind in zip(cf.SCHEDULES, ["AmortizingPayment", "Redemption", "AmortizingPayment",
                                                   "Redemption", "AmortizingPayment"]):
        lspec = dict(flat, schedule=sspec)
        first = ql.as_coupon(cf.fixed_rate_leg(lspec)[0])
        start, pay = first.accrualStartDate(), first.date()
        spec = {"leadingFlows": [cf.flow(kind, 1e5, pay)], "fixedRateLeg": lspec, "extraFlows": []}
        settlements = [(None, None), (False, v.enc(start + 1)),
                       (None, v.enc(start + (first.accrualEndDate() - start) // 2)), (True, v.enc(pay - 1)),
                       (False, v.enc(pay)), (True, v.enc(pay))]
        with H.case_settings(v.enc(start)) as st:
            expected = {m: [v.attempt(lambda m=m, i=i, d=d: H.call(f"CashFlows::{m}", spec, i, d))
                            for i, d in settlements]
                        for m in FIRST_COUPON}
        out.append({"name": f"{sname} / {cf.FLAT} led by a {kind} / first coupon after another flow",
                    "inputs": {"settings": st, "kind": "firstCoupon", "leg": spec,
                               "settlements": [{"includeSettlementDateFlows": i, "settlementDate": d}
                                               for i, d in settlements]},
                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/cashflows.hpp", cases)
