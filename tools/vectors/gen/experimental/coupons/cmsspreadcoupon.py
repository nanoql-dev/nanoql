# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CmsSpreadLeg and its coupons (ql/experimental/coupons/cmsspreadcoupon.hpp, .cpp), the legs'
structure, fixings and rates, priced by the lognormal CMS spread pricer
(ql/experimental/coupons/lognormalcmsspreadpricer.hpp) at a correlation of 0.3 on a linear TSR pricer of a flat 20%
swaption volatility at zero mean reversion. Legs on a 10Y-2Y EUR swap spread index (the swap indexes of
tools/vectors/gen/cashflows/cmscoupon.py, on its curve and evaluation date): plain, with gearings and spreads per
coupon, a payment day counter, convention and fixing days, caps and floors (capped CMS spread coupons), caps per coupon,
in arrears, amortizing nominals, and a zero gearing, which QuantLib's leg builds as a fixed coupon at the spread. And
the pricers' base, CmsSpreadCouponPricer, abstract in QuantLib too, through its lognormal pricer: its correlation as
built and after setCorrelation.

Each case's outputs are, per coupon, whether it is a floating coupon, its payment, accrual and reference-period dates,
its nominal, its rate, and for a floating coupon its fixing date, fixing days, gearing, spread, whether it fixes in
arrears and its index fixing, null for a fixed coupon. A pricer case's outputs are
"correlation", as built, and "correlationAfterSet" where one is set.

Each case's inputs hold its settings and "leg" ({"schedule", "index1", "index2", "nominals", "paymentDayCounter",
"paymentConvention", "fixingDays", "gearings", "spreads", "caps", "floors", "inArrears", "pricer"}), the indexes
cmscoupon.py's swap index specs, combined as SwapSpreadIndex("CMS10Y2Y", index1, index2), the pricer {"correlation"}.
The wheel's CmsSpreadLeg(nominals, schedule, index, paymentDayCounter, paymentConvention, fixingDays, gearings,
spreads, caps, floors, isInArrears) is the C++ builder's settings in one call. A pricer case's inputs hold its settings and "pricer" ({"correlation",
"setCorrelation"}, the value set or null), on a linear TSR pricer of a flat 20% swaption volatility. Read back by
tests/NanoQL.Tests/Vectors/Experimental/Coupons/CmsSpreadCouponVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_cmscoupon", os.path.join(v.GEN_ROOT, "cashflows", "cmscoupon.py"))
cms = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cms)
fvf, swi, irx = cms.fvf, cms.swi, cms.irx
D, M, Y, F, P = cms.D, cms.M, cms.Y, cms.F, cms.P


def leg(schedule, nominals, dc=irx.A365, convention=F, fixing_days=(), gearings=(), spreads=(), caps=(), floors=(),
        in_arrears=False):
    return {"schedule": schedule, "index1": cms.CMS10Y, "index2": cms.CMS2Y, "nominals": list(nominals),
            "paymentDayCounter": dc, "paymentConvention": int(convention), "fixingDays": list(fixing_days),
            "gearings": list(gearings), "spreads": list(spreads), "caps": list(caps), "floors": list(floors),
            "inArrears": in_arrears, "pricer": {"correlation": 0.3}}


def spread_pricer(correlation):
    vol = ql.SwaptionVolatilityStructureHandle(ql.ConstantSwaptionVolatility(
        cms.TODAY, ql.TARGET(), ql.Following, 0.20, ql.Actual365Fixed()))
    cms_pricer = ql.LinearTsrPricer(vol, ql.QuoteHandle(ql.SimpleQuote(0.0)))
    return ql.LognormalCmsSpreadPricer(cms_pricer, ql.QuoteHandle(ql.SimpleQuote(correlation)))


def make_leg(spec):
    index = ql.SwapSpreadIndex("CMS10Y2Y", swi.make_swap_index(spec["index1"]), swi.make_swap_index(spec["index2"]))
    leg = ql.CmsSpreadLeg(spec["nominals"], fvf.schedule(spec["schedule"]), index,
                          irx.day_counter(spec["paymentDayCounter"]), spec["paymentConvention"], spec["fixingDays"],
                          spec["gearings"], spec["spreads"], spec["caps"], spec["floors"], spec["inArrears"])
    ql.setCouponPricer(leg, spread_pricer(spec["pricer"]["correlation"]))
    return leg


def legs():
    two6 = fvf.sched(D(17, 1, 2025), D(17, 1, 2027), ql.Period(6, M), irx.TARGET)
    five1 = fvf.sched(D(17, 1, 2025), D(17, 1, 2030), ql.Period(1, Y), irx.TARGET)
    stub = fvf.sched(D(17, 1, 2025), D(15, 9, 2026), ql.Period(6, M), irx.TARGET)
    return [
        ("plain", leg(two6, [100.0])),
        ("gearings and spreads", leg(two6, [1e6], gearings=[1.0, 0.9, 0.8, 0.7], spreads=[0.001, 0.002, 0.003, 0.004])),
        ("day counter, Preceding, 3 fixing days", leg(five1, [100.0], irx.A360, P, [3])),
        ("caps and floors", leg(two6, [100.0], caps=[0.02], floors=[-0.01])),
        ("caps only, per coupon", leg(five1, [100.0], caps=[0.004, 0.0045, 0.005, 0.0055, 0.006])),
        ("in arrears", leg(two6, [100.0], in_arrears=True)),
        ("amortizing, a short front stub", leg(stub, [100.0, 75.0, 50.0, 25.0])),
        ("a zero gearing", leg(two6, [100.0], gearings=[0.0, 1.0], spreads=[0.01])),
    ]


def outputs(cashflows):
    out = {k: [] for k in ["floating", "dates", "accrualStartDates", "accrualEndDates", "referenceStarts",
                           "referenceEnds", "nominals", "rates", "fixingDates", "fixingDays", "gearings", "spreads",
                           "inArrears", "indexFixings"]}
    for cf in cashflows:
        c = ql.as_coupon(cf)
        f = ql.as_floating_rate_coupon(cf)
        out["floating"].append(f is not None)
        out["dates"].append(v.enc(c.date()))
        out["accrualStartDates"].append(v.enc(c.accrualStartDate()))
        out["accrualEndDates"].append(v.enc(c.accrualEndDate()))
        out["referenceStarts"].append(v.enc(c.referencePeriodStart()))
        out["referenceEnds"].append(v.enc(c.referencePeriodEnd()))
        out["nominals"].append(c.nominal())
        out["rates"].append(v.attempt(c.rate))
        out["fixingDates"].append(v.enc(f.fixingDate()) if f is not None else None)
        out["fixingDays"].append(f.fixingDays() if f is not None else None)
        out["gearings"].append(f.gearing() if f is not None else None)
        out["spreads"].append(f.spread() if f is not None else None)
        out["inArrears"].append(f.isInArrears() if f is not None else None)
        out["indexFixings"].append(v.attempt(f.indexFixing) if f is not None else None)
    return out


def pricer_cases(st):
    vol = ql.SwaptionVolatilityStructureHandle(ql.ConstantSwaptionVolatility(
        cms.TODAY, ql.TARGET(), ql.Following, 0.20, ql.Actual365Fixed()))
    cms_pricer = ql.LinearTsrPricer(vol, ql.QuoteHandle(ql.SimpleQuote(0.0)))
    out = []
    for built, reset in ((0.3, None), (0.3, -0.2), (-0.5, 0.0), (1.0, 0.75)):
        p = ql.LognormalCmsSpreadPricer(cms_pricer, ql.QuoteHandle(ql.SimpleQuote(built)))
        expected = {"correlation": p.correlation().value()}
        if reset is not None:
            p.setCorrelation(ql.QuoteHandle(ql.SimpleQuote(reset)))
            expected["correlationAfterSet"] = p.correlation().value()
        name = f"pricer: correlation {built}" + ("" if reset is None else f", set to {reset}")
        out.append({"name": name, "inputs": {"settings": st, "pricer": {"correlation": built, "setCorrelation": reset}},
                    "expected": expected})
    return out


def cases():
    st = v.settings(cms.TODAY)
    return [{"name": name, "inputs": {"settings": st, "leg": spec}, "expected": outputs(make_leg(spec))}
            for name, spec in legs()] + pricer_cases(st)


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/coupons/cmsspreadcoupon.hpp", cases)
