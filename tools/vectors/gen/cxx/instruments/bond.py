# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Bond (ql/instruments/bond.hpp) and FixedRateBond (ql/instruments/bonds/fixedratebond.hpp)
where the wheel can't reach, each output computed by the C++ harness: bonds on QuantLib's empty calendar and day
counter, which the wheel can't make, a fixed-rate bond's first-period day counter, which its FixedRateBond doesn't
take, and the theoretical yield's guess and price type, which its bondYield doesn't pass.

- 11 bonds of tools/vectors/gen/instruments/bond.py's specs, a null calendar or day counter being QuantLib's empty
  one: both constructors' bonds on the empty calendar, whose settlement date QuantLib can't compute ("no calendar
  implementation provided") while the members given a date work; zero-coupon bonds on it, unadjusted and following
  (whose construction QuantLib refuses); fixed-rate bonds with a first-period day counter on a short first period and
  on a schedule from dates, on its own and with a payment calendar; ex-coupon periods on the empty calendar, a month
  (unadjusted, which QuantLib computes) and a week (whose construction it refuses); and a floating-rate bond with no
  accrual day counter, which takes its index's. Each under one of bond.py's evaluation contexts before its last
  evaluation date, drawn with the seed (the floating-rate bond under the first evaluation date, before its first
  fixing, so that no fixing is stored), with bond.py's outputs: the inspectors with no date, the members at 10 dates
  (a null one and 9 drawn from around the cash flows, the issue date, the evaluation date and dates before the start
  and after the maturity), and the members reading DiscountingBondEngine's results on a flat curve from the
  evaluation date (the engine's includeSettlementDateFlows null); a fixed-rate bond adds its frequency, day counter
  and first-period day counter (their names, null for the empty day counter). A refused construction is the case's
  one output, "construct".
- Every case, and one case for each of the first 8 bonds of bond.py that QuantLib builds (under one of its contexts
  before its last evaluation date), has the theoretical yield (bondYield(dc, comp, freq, accuracy, maxEvaluations,
  guess, priceType)) for the case's rate's conventions at 7 settings: C++'s defaults; a dirty price; guesses of 0
  (whose first step is 0: "unable to bracket root in 100 function evaluations"), 15% and -2% at tighter accuracies;
  and 2 and 10 evaluations, the first too few to bracket the root.

A refused value is {"throws": true}. Amounts, notionals and settlement values are compared at the bond's face amount.
Each case's inputs hold its settings, "bond", "rate", "curve" and "yields" ({"accuracy", "maxEvaluations", "guess",
"priceType"}), with "dates" and "price" where the case has bond.py's outputs. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/BondCxxVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bd = _load("wheel_bond", "instruments/bond.py")
frb = _load("wheel_floatingratebond", "instruments/bonds/floatingratebond.py")

D = ql.Date
TARGET, UK = bd.TARGET, bd.UK_SETTLEMENT
CLEAN, DIRTY = int(ql.BondPrice.Clean), int(ql.BondPrice.Dirty)

# (accuracy, maxEvaluations, guess, priceType) for the theoretical yield
YIELDS = [(1.0e-8, 100, 0.05, CLEAN), (1.0e-8, 100, 0.05, DIRTY), (1.0e-10, 100, 0.0, CLEAN),
          (1.0e-10, 100, 0.15, DIRTY), (1.0e-12, 100, -0.02, CLEAN), (1.0e-12, 2, 0.05, CLEAN),
          (1.0e-12, 10, 0.05, DIRTY)]


def harness_bonds():
    """(name, bond spec, face amount, whether only the first evaluation date fits) for the 11 bonds."""
    s6m = bd.fixed(bd.S6M, [100.0], [0.05])
    redeemed = bd.leg_spec(s6m, extra=[bd.flow("Redemption", 100.0, D(15, 3, 2028))])
    short_first = bd.schedule_spec(D(10, 2, 2025), D(15, 3, 2029), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    from_dates = bd.dates_schedule_spec([D(16, 6, 2025), D(1, 9, 2025), D(16, 6, 2026), D(16, 6, 2027)])
    month = bd.ex_coupon_spec(1, ql.Months, None, ql.Unadjusted)
    week = bd.ex_coupon_spec(7, ql.Days, None, ql.Unadjusted)
    frn_schedule = bd.schedule_spec(D(20, 8, 2025), D(20, 8, 2029), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    euribor = frb.euribor((6, ql.Months), bd.EVALUATION_DATES[0], 0.03)
    return [
        ("coupons on the empty calendar", bd.bond_spec(2, None, D(15, 3, 2024), bd.leg_spec(s6m)), 100.0, False),
        ("face amount on the empty calendar", bd.face_bond_spec(2, None, 100.0, None, None, redeemed), 100.0, False),
        ("zero coupon on the empty calendar, unadjusted",
         bd.zero_bond_spec(2, None, 1e6, D(16, 6, 2028), ql.Unadjusted), 1e6, False),
        ("zero coupon on the empty calendar, following", bd.zero_bond_spec(2, None, 100.0, D(16, 6, 2028)), 100.0,
         False),
        ("fixed rate, first-period Actual/365 on 30/360", bd.fixed_bond_spec(
            2, 100.0, short_first, [0.045], bd.THIRTY360_BB, ql.Following, 100.0, None, None, None, bd.A365), 100.0,
         False),
        ("fixed rate, first-period Actual/360 on Actual/Actual (ISMA)", bd.fixed_bond_spec(
            2, 100.0, short_first, [0.04], bd.ACT_ISMA, ql.ModifiedFollowing, 100.0, D(10, 2, 2025), None, None,
            bd.A360), 100.0, False),
        ("fixed rate from dates", bd.fixed_bond_spec(
            0, 100.0, from_dates, [0.03], bd.A365, ql.Unadjusted, 100.0, None, None, None, bd.A360), 100.0, False),
        ("fixed rate from dates, a payment calendar", bd.fixed_bond_spec(
            1, 1e6, from_dates, [0.035], bd.A360, ql.Following, 100.0, None, UK, None, bd.ACT_ISDA), 1e6, False),
        ("fixed rate, ex-coupon a month on the empty calendar", bd.fixed_bond_spec(
            2, 100.0, bd.S6M_UK, [0.04], bd.A365, ql.Following, 100.0, None, None, month), 100.0, False),
        ("fixed rate, ex-coupon a week on the empty calendar", bd.fixed_bond_spec(
            2, 100.0, bd.S6M_UK, [0.04], bd.A365, ql.Following, 100.0, None, None, week), 100.0, False),
        ("floating rate, no accrual day counter", bd.floating_bond_spec(2, 100.0, frn_schedule, euribor, None), 100.0,
         True),
    ]


def call(H, method, *args):
    return v.attempt(lambda: H.call(method, *args))


def scaled(H, scale, method, *args):
    r = call(H, method, *args)
    return r if isinstance(r, dict) else v.amount(r, scale)


def bond_dates(H, spec, ev, rnd, n):
    """n dates drawn with rnd after a null one: each cash flow's date and the days either side, the issue date and the
    days either side, the evaluation date, and dates before the start and after the maturity."""
    out = set()
    for d in H.call("Bond::cashflowDates", spec):
        out |= {d - 1, d, d + 1}
    issue = H.call("Bond::issueDate", spec)
    if issue is not None:
        out |= {issue - 1, issue, issue + 1}
    out.add(ev.serialNumber())
    start, maturity = call(H, "Bond::startDate", spec), call(H, "Bond::maturityDate", spec)
    if isinstance(start, int):
        out.add(start - 40)
    if isinstance(maturity, int):
        out.add(maturity + 40)
    candidates = sorted(out)
    return [None] + sorted(rnd.sample(candidates, min(n - 1, len(candidates))))


def static_outputs(H, spec, scale):
    n = len(H.call("Bond::cashflowDates", spec))
    return {"settlementDays": H.call("Bond::settlementDays", spec),
            "notionals": [v.amount(x, scale) for x in H.call("Bond::notionals", spec)],
            "cashflowDates": H.call("Bond::cashflowDates", spec),
            "cashflowAmounts": [scaled(H, scale, "Bond::cashflowAmount", spec, i) for i in range(n)],
            "redemptionDates": H.call("Bond::redemptionDates", spec),
            "redemptionAmounts": [v.amount(x, scale) for x in H.call("Bond::redemptionAmounts", spec)],
            "redemptionDate": call(H, "Bond::redemption().date", spec),
            "redemptionAmount": scaled(H, scale, "Bond::redemption().amount", spec),
            "startDate": call(H, "Bond::startDate", spec),
            "maturityDate": call(H, "Bond::maturityDate", spec),
            "issueDate": H.call("Bond::issueDate", spec),
            "isExpired": H.call("Bond::isExpired", spec)}


def dated_outputs(H, spec, dates, rate, price, scale):
    out = {m: [] for m in bd.DATED}
    for d in dates:
        for m in ["settlementDate", "isTradable", "accruedAmount", "nextCouponRate", "previousCouponRate",
                  "nextCashFlowDate", "previousCashFlowDate"]:
            out[m].append(call(H, f"Bond::{m}", spec, d))
        out["notional"].append(scaled(H, scale, "Bond::notional", spec, d))
        out["cleanPrice"].append(call(H, "Bond::cleanPrice(Rate,DayCounter,Compounding,Frequency,Date)", spec, rate, d))
        out["dirtyPrice"].append(call(H, "Bond::dirtyPrice(Rate,DayCounter,Compounding,Frequency,Date)", spec, rate, d))
        out["yield"].append(call(H, "Bond::yield(Price,DayCounter,Compounding,Frequency,Date,Real,Size,Real)", spec,
                                 price, rate["dayCounter"], rate["compounding"], rate["frequency"], d, 1.0e-8, 100,
                                 0.05))
    return out


def engine_outputs(H, spec, curve, rate, scale):
    """bond.py's engine outputs, the engine's includeSettlementDateFlows null as the wheel's."""
    def result(method):
        try:
            return v.result(lambda: H.call(method, spec, curve, None))
        except RuntimeError:
            return dict(v.THROWS)
    npv = result("DiscountingBondEngine::NPV")
    settlement = result("DiscountingBondEngine::settlementValue")
    return {"NPV": npv if isinstance(npv, dict) else v.amount(npv, scale),
            "errorEstimate": result("DiscountingBondEngine::errorEstimate"),
            "valuationDate": result("DiscountingBondEngine::valuationDate"),
            "settlementValue": settlement if isinstance(settlement, dict) else v.amount(settlement, scale),
            "cleanPriceEngine": call(H, "Bond::cleanPrice()", spec, curve, None),
            "dirtyPriceEngine": call(H, "Bond::dirtyPrice()", spec, curve, None),
            "yieldEngine": call(H, "Bond::yield(DayCounter,Compounding,Frequency,Real,Size,Real,Price::Type)", spec,
                                curve, None, rate["dayCounter"], rate["compounding"], rate["frequency"], 1.0e-8, 100,
                                0.05, CLEAN),
            "settlementValueFromClean": scaled(H, scale, "Bond::settlementValue(Real)", spec, curve, None, 99.0)}


def yields(H, spec, curve, rate):
    return [call(H, "Bond::yield(DayCounter,Compounding,Frequency,Real,Size,Real,Price::Type)", spec, curve, None,
                 rate["dayCounter"], rate["compounding"], rate["frequency"], accuracy, n, guess, kind)
            for accuracy, n, guess, kind in YIELDS]


def yield_inputs():
    return [{"accuracy": a, "maxEvaluations": n, "guess": g, "priceType": k} for a, n, g, k in YIELDS]


def cases():
    rnd = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        for k, (name, spec, scale, first_only) in enumerate(harness_bonds()):
            contexts = [c for c in bd.contexts() if c[0] == bd.EVALUATION_DATES[0] or not first_only
                        and c[0] != bd.EVALUATION_DATES[-1]]
            ev, ref_events, todays = rnd.choice(contexts)
            rate, price = bd.RATES[k % len(bd.RATES)], bd.PRICES[k % len(bd.PRICES)]
            with H.case_settings(v.enc(ev), include_reference_date_events=ref_events,
                                 include_todays_cash_flows=todays) as st:
                label = f"{name} / {ev.ISO()} {ref_events} {todays}"
                try:
                    H.call("Bond::issueDate", spec)
                except RuntimeError:
                    out.append({"name": label, "inputs": {"settings": st, "bond": spec},
                                "expected": {"construct": dict(v.THROWS)}})
                    continue
                curve = bd.curve_spec(ev, bd.CURVE_RATE)
                dates = bond_dates(H, spec, ev, rnd, 10)
                expected = static_outputs(H, spec, scale)
                expected.update(dated_outputs(H, spec, dates, rate, price, scale))
                expected.update(engine_outputs(H, spec, curve, rate, scale))
                if spec["type"] == "FixedRateBond":
                    for m in ["frequency", "dayCounter", "firstPeriodDayCounter"]:
                        expected[m] = H.call(f"FixedRateBond::{m}", spec)
                expected["theoreticalYield"] = yields(H, spec, curve, rate)
                out.append({"name": label,
                            "inputs": {"settings": st, "bond": spec, "dates": dates, "rate": rate, "price": price,
                                       "curve": curve, "yields": yield_inputs()},
                            "expected": expected})

        wheel = [(n, s) for n, s, _ in bd.bonds()
                 if not any(w in n for w in ("issue on", "issue after", "redemption alone"))][:8]
        for k, (name, spec) in enumerate(wheel):
            ev, ref_events, todays = rnd.choice([c for c in bd.contexts() if c[0] != bd.EVALUATION_DATES[-1]])
            rate = bd.RATES[k % len(bd.RATES)]
            with H.case_settings(v.enc(ev), include_reference_date_events=ref_events,
                                 include_todays_cash_flows=todays) as st:
                curve = bd.curve_spec(ev, bd.CURVE_RATE)
                out.append({"name": f"theoretical yield / {name} / {ev.ISO()} {ref_events} {todays}",
                            "inputs": {"settings": st, "bond": spec, "rate": rate, "curve": curve,
                                       "yields": yield_inputs()},
                            "expected": {"theoreticalYield": yields(H, spec, curve, rate)}})
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold, whatever the draws."""
    assert sum("construct" in c["expected"] for c in out) == 2
    assert any(c["expected"].get("settlementDate") == [v.THROWS] * 10 for c in out)
    assert any(c["expected"].get("firstPeriodDayCounter") is not None for c in out)
    yields = [c["expected"]["theoreticalYield"] for c in out if "theoreticalYield" in c["expected"]]
    solved = [y for y in yields if isinstance(y[0], float) and y[0] != 0.0]
    assert solved and all(y[2] == v.THROWS and y[5] == v.THROWS for y in solved)
    # another guess or accuracy moves the root in its last bits
    assert any(y[0] != y[3] for y in solved)


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bond.hpp", cases)
