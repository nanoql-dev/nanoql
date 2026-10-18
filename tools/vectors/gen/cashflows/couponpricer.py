# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackIborCouponPricer and setCouponPricer (ql/cashflows/couponpricer.hpp), on the evaluation
date 15 January 2025. The wheel binds the pricer's queries but not its initialize(coupon), so they are reached through
the coupons that hold it.

The pricer cases: the two timing adjustments (Black76 and BivariateLognormal) x three correlations (1, 0.5 and -0.3)
x a lognormal and a normal ConstantOptionletVolatility; two more whose volatility is given by setCapletVolatility after
the pricer is built; and one under BivariateLognormal with no volatility, which QuantLib asks for only once it has
checked that the coupon isn't paid at the index's maturity, so that only such coupons give a rate. Each prices the same
21 coupons, every coupon with a pricer of its own built from the case's spec without an explicit choice, under the
at-par switch's two settings in turn. The coupons fix in advance and in arrears, paid at the index's maturity, after
it, before it, and before the index's value date, on Euribor-style 1M, 3M, 6M and 1Y indexes, a negative curve and a
calendar with no holidays, with gearings and spreads, capped, floored and collared, before, on and after the
evaluation date; the last one's accrual period ends on its start, which the pricer refuses ("null accrual period").
Each case's outputs are, per coupon, its rate, amount, index fixing, adjusted fixing and convexity adjustment.

The setCouponPricer cases set one pricer on a leg (an IborLeg with caps, or a list of coupons: Ibor coupons, capped
Ibor coupons, a CappedFlooredCoupon over an Ibor coupon, and a leg's fixed-rate coupons, which QuantLib leaves as they
are) and give each cash flow's rate and amount after it; a null pricer, which the Ibor coupons refuse ("pricer not
compatible with Ibor coupon", or "Ibor Coupon" for a CappedFlooredCoupon over one), records "setCouponPricer":
{"throws": true}, and a leg of fixed-rate coupons alone takes it.

A refused value is {"throws": true}; amounts are compared at the coupons' largest nominal. Each case's inputs hold the
pricer spec ("pricer", as iborcoupon.py's functions build it), the volatility set afterwards ("capletVolatility",
null for none), and the coupons ("coupons", a list of coupon specs without pricers) or the leg ("leg"). Read back by
tests/NanoQL.Tests/Vectors/Coupons/CouponPricerVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_iborcoupon", os.path.join(v.GEN_ROOT, "cashflows", "iborcoupon.py"))
ibc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ibc)

irx = ibc.irx
TODAY = ibc.TODAY
d = ibc.d

E1M = ibc.euribor((1, ql.Months), ibc.CURVE_STEEP)
E3M = ibc.euribor((3, ql.Months), ibc.CURVE)
E6M = ibc.euribor((6, ql.Months), ibc.CURVE)
E6M_NEG = ibc.euribor((6, ql.Months), ibc.CURVE_NEGATIVE, "EuriborNeg")
E1Y = ibc.euribor((1, ql.Years), ibc.CURVE_STEEP)
NUL3M = irx.ibor_spec("NulIbor", (3, ql.Months), 0, ibc.NULL_CALENDAR, ql.Following, False, ibc.A365, ibc.CURVE)
BL = ql.BlackIborCouponPricer.BivariateLognormal


def coupons():
    c = ibc.ibor_coupon_spec
    return [
        c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M),
        c(d(21, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M),
        c(d(17, 12, 2025), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M),
        c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, in_arrears=True),
        c(d(24, 9, 2025), 2e6, d(20, 6, 2025), d(22, 9, 2025), 2, E3M, in_arrears=True),
        c(d(17, 1, 2030), 1e6, d(17, 1, 2029), d(17, 1, 2030), 2, E1Y),
        c(d(17, 1, 2030), 1e6, d(17, 1, 2029), d(17, 1, 2030), 2, E1Y, in_arrears=True),
        c(d(18, 3, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), 2, E3M),
        c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, 1.5, 0.001, in_arrears=True),
        c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, cap=0.032, capped=True),
        c(d(21, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, floor=0.03, in_arrears=True, capped=True),
        c(d(17, 12, 2029), 1e6, d(17, 1, 2029), d(17, 1, 2030), 2, E1Y, cap=0.05, floor=0.035, capped=True),
        c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, -1.0, 0.06, cap=0.03, capped=True),
        c(d(17, 3, 2025), 1e6, d(17, 9, 2024), d(17, 3, 2025), 2, E6M),
        c(d(17, 7, 2025), 1e6, d(17, 1, 2025), d(17, 7, 2025), 2, E6M, cap=0.031, capped=True),
        c(d(20, 7, 2025), 1e6, d(20, 1, 2025), d(20, 7, 2025), 3, E6M, 1.0, 0.0, in_arrears=True),
        c(d(14, 8, 2025), 1e6, d(14, 2, 2025), d(14, 8, 2025), 2, E6M_NEG),
        c(d(18, 9, 2025), 1e6, d(18, 8, 2025), d(18, 9, 2025), 2, E1M, 2.0, -0.01),
        c(d(3, 7, 2025), 1e6, d(3, 4, 2025), d(3, 7, 2025), 0, NUL3M, in_arrears=True),
        c(d(3, 7, 2025), 1e6, d(3, 4, 2025), d(3, 7, 2025), 0, NUL3M, floor=0.033, capped=True),
        c(d(17, 7, 2025), 1e6, d(17, 7, 2025), d(17, 7, 2025), 2, E6M),
    ]


def with_pricer(spec, p):
    return dict(spec, pricer=p)


def fixings():
    index = irx.make_index(E6M)
    return [(index, ibc.history(E6M, d(6, 9, 2024), d(20, 9, 2024), v.SEED))]


def pricer_cases():
    out = []
    for timing in (ql.BlackIborCouponPricer.Black76, BL):
        for correlation in (1.0, 0.5, -0.3):
            for vol in (ibc.VOL_LN, ibc.VOL_N):
                out.append((ibc.pricer_spec(vol, timing, correlation), None))
    out.append((ibc.pricer_spec(None, BL, 0.8), ibc.VOL_SLN))
    out.append((ibc.pricer_spec(None), ibc.VOL_LN))
    out.append((ibc.pricer_spec(None, BL, 0.8), None))
    return out


def setter_cases():
    """(name, coupons or leg, pricer spec or None)."""
    c = ibc.ibor_coupon_spec
    eur6 = ibc.sched(d(17, 7, 2025), d(17, 7, 2027), ibc.SIX_M)
    capped_leg = ibc.leg_spec(eur6, E6M, [1e6], caps=[0.033, None], floors=[None, None, 0.029])
    zero_leg = ibc.leg_spec(eur6, E6M, [1e6], gearings=[0.0], spreads=[0.02], caps=[0.05], payment_dc=ibc.A360)
    mixed = [c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M),
             c(d(17, 7, 2026), 1e6, d(19, 1, 2026), d(17, 7, 2026), 2, E6M, cap=0.031, capped=True),
             ibc.capped_coupon_spec(c(d(19, 1, 2027), 1e6, d(17, 7, 2026), d(19, 1, 2027), 2, E6M), floor=0.03)]
    only_capped = mixed[1:2]
    wrapped = mixed[2:]
    black = ibc.pricer_spec(ibc.VOL_LN)
    return [
        ("an IborLeg with caps and floors", {"leg": capped_leg}, black),
        ("Ibor, capped Ibor and wrapped capped coupons", {"coupons": mixed}, black),
        ("Ibor, capped Ibor and wrapped capped coupons, bivariate lognormal",
         {"coupons": mixed}, ibc.pricer_spec(ibc.VOL_N, BL, 0.4)),
        ("a null pricer on an IborLeg", {"leg": capped_leg}, None),
        ("a null pricer on a capped Ibor coupon", {"coupons": only_capped}, None),
        ("a null pricer on a CappedFlooredCoupon over an Ibor coupon", {"coupons": wrapped}, None),
        ("a null pricer on fixed-rate coupons", {"leg": zero_leg}, None),
        ("a pricer on fixed-rate coupons", {"leg": zero_leg}, black),
    ]


def outputs(cashflows, scale):
    return {
        "rate": [v.attempt(lambda cf=cf: ql.as_coupon(cf).rate()) for cf in cashflows],
        "amount": [ibc.scaled(lambda cf=cf: cf.amount(), scale) for cf in cashflows],
    }


def floating_outputs(cs, scale):
    out = outputs(cs, scale)
    out.update({
        "indexFixing": [v.attempt(lambda cf=cf: cf.indexFixing()) for cf in cs],
        "adjustedFixing": [v.attempt(lambda cf=cf: cf.adjustedFixing()) for cf in cs],
        "convexityAdjustment": [v.attempt(lambda cf=cf: cf.convexityAdjustment()) for cf in cs]})
    return out


def cases():
    out = []
    specs = coupons()
    scale = max(s["nominal"] for s in specs)
    for k, (p, vol) in enumerate(pricer_cases()):
        with v.case_settings(TODAY, use_at_par_coupons=k % 2 == 0, fixings=fixings()) as st:
            built = []
            for s in specs:
                cp = ibc.coupon(dict(s, pricer=None))
                pricer = ibc.pricer(p)
                if vol is not None:
                    pricer.setCapletVolatility(ibc.volatility(vol))
                cp.setPricer(pricer)
                built.append(cp)
            out.append({"name": f"pricer {k}: timing {p['timingAdjustment']}, correlation {p['correlation']}",
                        "inputs": {"settings": st, "pricer": p, "capletVolatility": vol, "coupons": v.enc(specs)},
                        "expected": floating_outputs(built, scale)})
    for k, (name, what, p) in enumerate(setter_cases()):
        with v.case_settings(TODAY, use_at_par_coupons=k % 2 == 0) as st:
            if "leg" in what:
                cashflows = ibc.leg(what["leg"])
                nominal = max(what["leg"]["notionals"])
            else:
                cashflows = ql.Leg([ibc.coupon(s) for s in what["coupons"]])
                nominal = max(s.get("nominal", s.get("underlying", {}).get("nominal", 0)) for s in what["coupons"])
            inputs = {"settings": st, "pricer": p, "capletVolatility": None, **v.enc(what)}
            try:
                ql.setCouponPricer(cashflows, ibc.pricer(p))
            except RuntimeError:
                out.append({"name": f"setCouponPricer: {name}", "inputs": inputs,
                            "expected": {"setCouponPricer": dict(v.THROWS)}})
                continue
            out.append({"name": f"setCouponPricer: {name}", "inputs": inputs, "expected": outputs(cashflows, nominal)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/couponpricer.hpp", cases)
