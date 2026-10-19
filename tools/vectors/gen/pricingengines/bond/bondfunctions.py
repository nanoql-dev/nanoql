# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BondFunctions (ql/pricingengines/bond/bondfunctions.hpp): the 26 function names the wheel
binds, on 11 bonds (tools/vectors/gen/instruments/bond.py's specs: semiannual, annual, quarterly compounded and
ex-coupon fixed-rate bonds, one settling on the evaluation date, a zero-coupon bond, an amortizing bond, a
floating-rate bond with stored fixings, bonds from QuantLib's two Bond constructors, and one issued after the
evaluation date's settlement), each under one evaluation context drawn with the seed, at 10 settlement dates (a null
one, which means the bond's settlement date, and 9 drawn around the bond's payment, accrual and issue dates, the
evaluation date and dates before its start and after its maturity, among them dates at which it isn't tradable).

At each date a case's outputs are: isTradable; the previous and next cash-flow dates and amounts and coupon rates; the
accrual start and end dates, the accrual period and days and the accrued period and days; the accrued amount; on a
flat curve whose reference date is the evaluation date, the clean and dirty prices, the BPS and the at-the-money rate,
with no price and for the case's price; at the case's rate, the clean price and the BPS through the InterestRate and
the rate forms, the modified and simple durations (InterestRate) and Macaulay's (rate form, its compounding
compounded), the convexity, basis-point value and yield value of a basis point through both forms; the yield that
reproduces the case's price by the default bondYield (NewtonSafe, accuracy 1e-10, 100 iterations, guess 0.05,
C++'s defaults) and by yieldNewtonSafe and yieldBrent (accuracy 1e-10, guess 0.05, a fresh solver with its default
100 evaluations); and on the curve with the case's Z-spread, the clean and dirty prices and the Z-spread that
reproduces the case's price (accuracy 1e-10, 100 iterations, guess 0.0, C++'s defaults). The wheel's other five
yield<Solver> instances aren't vectored: nanoql has no Bisection, FalsePosition, Ridder, Secant or Newton, and another
algorithm's root agrees only within its accuracy. The yield-based dirty price and the reference periods, which the
wheel doesn't bind, are the harness's cxx/pricingengines/bond/bondfunctions. A refused value is {"throws": true}: a
price or yield at a date the bond isn't tradable ("non tradable at ..."). BPS and basis-point values are compared at
the bond's face amount times 1e-4, amounts at the face amount.

Each case's inputs hold its settings, "bond", "curve", "rate", "price", "zSpread" ({"spread", "compounding",
"frequency"}) and "dates". Read back by tests/NanoQL.Tests/Vectors/PricingEngines/BondFunctionsVectorTests.cs."""
import importlib.util
import os
import random
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


bd = _load("wheel_bond", "instruments/bond.py")
frb = _load("wheel_floatingratebond", "instruments/bonds/floatingratebond.py")

D = ql.Date
BF = ql.BondFunctions
TARGET, NULL_CALENDAR, UK, US = bd.TARGET, bd.NULL_CALENDAR, bd.UK_SETTLEMENT, bd.US_GOVERNMENT
A360, A365, BB, ISMA = bd.A360, bd.A365, bd.THIRTY360_BB, bd.ACT_ISMA


def bonds():
    """(name, bond spec, face amount, evaluation date or None for a drawn one) for the 11 bonds."""
    s6m = bd.schedule_spec(D(15, 3, 2024), D(15, 3, 2028), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    s1y = bd.schedule_spec(D(16, 6, 2025), D(16, 6, 2030), (1, ql.Years), NULL_CALENDAR, ql.Unadjusted,
                           ql.DateGeneration.Forward)
    s3m = bd.schedule_spec(D(30, 11, 2024), D(30, 11, 2027), (3, ql.Months), US, ql.Unadjusted)
    s6m_uk = bd.schedule_spec(D(20, 1, 2025), D(20, 1, 2029), (6, ql.Months), UK, ql.ModifiedFollowing)
    sinking = bd.schedule_spec(D(15, 1, 2025), D(15, 1, 2029), (1, ql.Years), TARGET)
    s6m_late = bd.schedule_spec(D(15, 9, 2026), D(15, 9, 2031), (6, ql.Months), TARGET, ql.Following)
    leg6m = bd.fixed(s6m, [100.0], [0.05])
    last = list(bd.fixed_rate_leg(leg6m))[-1].date()
    _, floating, _, ev, _ = frb.bonds()[0]
    return [
        ("fixed 6M", bd.fixed_bond_spec(2, 100.0, s6m, [0.05], A360, ql.Following, 100.0, D(15, 3, 2024)), 100.0,
         None),
        ("fixed 1Y settling on the evaluation date", bd.fixed_bond_spec(0, 100.0, s1y, [0.03], BB, ql.Unadjusted),
         100.0, D(16, 6, 2027)),
        ("fixed 3M compounded", bd.bond_spec(3, US, None, bd.leg_spec(bd.fixed(s3m, [5e5], [0.035], A365,
                                                                               ql.Compounded, ql.Quarterly))), 5e5,
         None),
        ("fixed ex-coupon", bd.fixed_bond_spec(2, 1e6, s6m_uk, [0.045], A365, ql.ModifiedFollowing, 100.0, None, UK,
                                               bd.ex_coupon_spec(7, ql.Days, TARGET)), 1e6, None),
        ("zero coupon", bd.zero_bond_spec(2, TARGET, 100.0, D(16, 6, 2028)), 100.0, None),
        ("amortizing", bd.amortizing_bond_spec(2, [100.0, 77.0, 52.0, 27.0], sinking, [0.04], A365), 100.0, None),
        ("floating", floating, 100.0, ev),
        ("Bond from coupons", bd.bond_spec(2, TARGET, D(15, 3, 2024), bd.leg_spec(leg6m)), 100.0, None),
        ("Bond from a face amount", bd.face_bond_spec(2, TARGET, 100.0, None, None,
                                                      bd.leg_spec(leg6m, extra=[bd.flow("Redemption", 101.0, last)])),
         100.0, None),
        ("issued after the settlement", bd.fixed_bond_spec(2, 100.0, s6m_late, [0.04], ISMA, ql.Following, 100.0,
                                                           D(15, 9, 2026)), 100.0, D(16, 6, 2026)),
        ("fixed 6M, a second context", bd.fixed_bond_spec(2, 100.0, s6m, [0.05], A360, ql.Following, 100.0,
                                                          D(15, 3, 2024)), 100.0, None),
    ]


RATES = bd.RATES
PRICES = [bd.price_spec(99.5, ql.BondPrice.Clean), bd.price_spec(101.25, ql.BondPrice.Dirty),
          bd.price_spec(97.0, ql.BondPrice.Clean)]
Z_SPREADS = [{"spread": 0.0045, "compounding": int(ql.Continuous), "frequency": int(ql.Annual)},
             {"spread": -0.002, "compounding": int(ql.Compounded), "frequency": int(ql.Semiannual)}]
EVALUATION_DATES = [D(16, 6, 2025), D(14, 3, 2026), D(15, 9, 2027)]
NAMES = ["isTradable", "previousCashFlowDate", "nextCashFlowDate", "previousCashFlowAmount", "nextCashFlowAmount",
         "previousCouponRate", "nextCouponRate", "accrualStartDate", "accrualEndDate", "accrualPeriod", "accrualDays",
         "accruedPeriod", "accruedDays", "accruedAmount", "cleanPriceCurve", "dirtyPriceCurve", "bpsCurve",
         "atmRate", "atmRatePrice", "cleanPriceRate", "cleanPriceRateForm", "bpsRate", "bpsRateForm",
         "durationModified", "durationSimple", "durationMacaulay", "convexity", "convexityRateForm",
         "basisPointValue", "basisPointValueRateForm", "yieldValueBasisPoint", "yieldValueBasisPointRateForm",
         "bondYield", "yieldNewtonSafe", "yieldBrent", "cleanPriceZSpread", "dirtyPriceZSpread", "zSpread"]


def at(bond, d, curve, rate, price, z, scale):
    """The outputs at one settlement date d (QuantLib's null date for the bond's settlement date)."""
    r = bd.make_rate(rate)
    rd, comp, freq = r.dayCounter(), r.compounding(), r.frequency()
    p = bd.make_price(price)
    zc, zf = z["compounding"], z["frequency"]
    a = v.attempt
    out = {
        "isTradable": a(lambda: BF.isTradable(bond, d)),
        "previousCashFlowDate": a(lambda: BF.previousCashFlowDate(bond, d)),
        "nextCashFlowDate": a(lambda: BF.nextCashFlowDate(bond, d)),
        "previousCashFlowAmount": bd.scaled(lambda: BF.previousCashFlowAmount(bond, d), scale),
        "nextCashFlowAmount": bd.scaled(lambda: BF.nextCashFlowAmount(bond, d), scale),
        "previousCouponRate": a(lambda: BF.previousCouponRate(bond, d)),
        "nextCouponRate": a(lambda: BF.nextCouponRate(bond, d)),
        "accrualStartDate": a(lambda: BF.accrualStartDate(bond, d)),
        "accrualEndDate": a(lambda: BF.accrualEndDate(bond, d)),
        "accrualPeriod": a(lambda: BF.accrualPeriod(bond, d)),
        "accrualDays": a(lambda: BF.accrualDays(bond, d)),
        "accruedPeriod": a(lambda: BF.accruedPeriod(bond, d)),
        "accruedDays": a(lambda: BF.accruedDays(bond, d)),
        "accruedAmount": a(lambda: BF.accruedAmount(bond, d)),
        "cleanPriceCurve": a(lambda: BF.cleanPrice(bond, curve, d)),
        "dirtyPriceCurve": a(lambda: BF.dirtyPrice(bond, curve, d)),
        "bpsCurve": bd.scaled(lambda: BF.bps(bond, curve, d), scale * 1e-4),
        "atmRate": a(lambda: BF.atmRate(bond, curve, d)),
        "atmRatePrice": a(lambda: BF.atmRate(bond, curve, d, p)),
        "cleanPriceRate": a(lambda: BF.cleanPrice(bond, r, d)),
        "cleanPriceRateForm": a(lambda: BF.cleanPrice(bond, r.rate(), rd, comp, freq, d)),
        "bpsRate": bd.scaled(lambda: BF.bps(bond, r, d), scale * 1e-4),
        "bpsRateForm": bd.scaled(lambda: BF.bps(bond, r.rate(), rd, comp, freq, d), scale * 1e-4),
        "durationModified": a(lambda: BF.duration(bond, r, ql.Duration.Modified, d)),
        "durationSimple": a(lambda: BF.duration(bond, r, ql.Duration.Simple, d)),
        "durationMacaulay": a(lambda: BF.duration(bond, r.rate(), rd, ql.Compounded, ql.Semiannual,
                                                  ql.Duration.Macaulay, d)),
        "convexity": a(lambda: BF.convexity(bond, r, d)),
        "convexityRateForm": a(lambda: BF.convexity(bond, r.rate(), rd, comp, freq, d)),
        "basisPointValue": bd.scaled(lambda: BF.basisPointValue(bond, r, d), scale * 1e-4),
        "basisPointValueRateForm": bd.scaled(lambda: BF.basisPointValue(bond, r.rate(), rd, comp, freq, d),
                                             scale * 1e-4),
        "yieldValueBasisPoint": a(lambda: BF.yieldValueBasisPoint(bond, r, d)),
        "yieldValueBasisPointRateForm": a(lambda: BF.yieldValueBasisPoint(bond, r.rate(), rd, comp, freq, d)),
        "bondYield": a(lambda: BF.bondYield(bond, p, rd, comp, freq, d, 1.0e-10, 100, 0.05)),
        "yieldNewtonSafe": a(lambda: BF.yieldNewtonSafe(ql.NewtonSafe(), bond, p, rd, comp, freq, d, 1.0e-10, 0.05)),
        "yieldBrent": a(lambda: BF.yieldBrent(ql.Brent(), bond, p, rd, comp, freq, d, 1.0e-10, 0.05)),
        "cleanPriceZSpread": a(lambda: BF.cleanPrice(bond, curve, z["spread"], zc, zf, d)),
        "dirtyPriceZSpread": a(lambda: BF.dirtyPrice(bond, curve, z["spread"], zc, zf, d)),
        "zSpread": a(lambda: BF.zSpread(bond, p, curve, zc, zf, d, 1.0e-10, 100, 0.0)),
    }
    assert list(out) == NAMES
    return out


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for k, (name, spec, scale, fixed_ev) in enumerate(bonds()):
        ev = fixed_ev if fixed_ev is not None else rnd.choice(EVALUATION_DATES)
        ref_events, todays = rnd.choice([False, True]), rnd.choice([None, False, True])
        stored = None
        with v.case_settings(ev):
            if spec["type"] == "FloatingRateBond":
                stored = frb.past_fixings(spec, ev)(bd.make_bond(spec))
        with v.case_settings(ev, include_reference_date_events=ref_events, include_todays_cash_flows=todays,
                             fixings=stored) as st:
            bond = bd.make_bond(spec)
            curve = bd.curve_spec(ev, bd.CURVE_RATE)
            crv = bd.make_curve(curve)
            rate, price, z = RATES[k % len(RATES)], PRICES[k % len(PRICES)], Z_SPREADS[k % len(Z_SPREADS)]
            dates = bd.bond_dates(bond, ev, rnd, 10)
            expected = {"startDate": v.attempt(lambda: BF.startDate(bond)),
                        "maturityDate": v.attempt(lambda: BF.maturityDate(bond))}
            expected.update({m: [] for m in NAMES})
            for serial in dates:
                for m, value in at(bond, bd.date_or_null(serial), crv, rate, price, z, scale).items():
                    expected[m].append(value)
            out.append({"name": f"{name} / {ev.ISO()} {ref_events} {todays}",
                        "inputs": {"settings": st, "bond": spec, "curve": curve, "rate": rate, "price": price,
                                   "zSpread": z, "dates": dates},
                        "expected": expected})
    assert any(v.THROWS in c["expected"]["cleanPriceCurve"] for c in out)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/bond/bondfunctions.hpp", cases)
