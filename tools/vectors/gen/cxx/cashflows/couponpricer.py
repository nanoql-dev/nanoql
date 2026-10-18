# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of ql/cashflows/couponpricer.hpp that the wheel doesn't bind or can't reach, each output
computed by the C++ harness, on the evaluation date 15 January 2025.

- IborCouponPricer::useIndexedCoupon(): a BlackIborCouponPricer built without an explicit choice under both settings
  of the at-par switch (QuantLib negates the switch), and with an explicit choice, which the switch doesn't change.
- A pricer's six queries after initialize(coupon), which the wheel binds without initialize: swapletPrice, swapletRate,
  and capletPrice, capletRate, floorletPrice and floorletRate at three effective strikes, for BlackIborCouponPricers
  (lognormal, normal under the bivariate lognormal adjustment, and with no volatility) on Ibor coupons holding a pricer
  of the same spec: in advance and in arrears, after and before the evaluation date (a fixing stored), and on an index
  with no forecasting curve, where QuantLib refuses the prices ("no forecast curve provided"); on a FloatingRateCoupon
  ("IborCouponPricer: expected IborCoupon"); and on an Ibor coupon with no pricer of its own, whose index fixing
  QuantLib refuses.
- setCouponPricer on coupons the wheel can't build: FloatingRateCoupons, on Ibor and overnight indexes, and
  CappedFlooredCoupons over them, which take any pricer (the visitor's default case), with the harness's
  IndexFixingPricer and with a BlackIborCouponPricer; and an Ibor coupon refusing the harness's pricer.
- setCouponPricers, which the wheel doesn't wrap: the list form (a pricer per cash flow, the last repeated; more pricers
  than cash flows and an empty leg refused, and a pricer an Ibor coupon refuses), and the first-matching forms of two,
  three and four pricers, each cash flow taking the first it accepts and keeping its own when it accepts none.

A setter's output ("pricers") is, per cash flow, the position in the call's pricers of the pricer it holds afterwards,
or null for none. A refused value is {"throws": true}. Each case's inputs hold the pricer spec ("pricer") or specs
("pricers"), the coupon spec ("coupon") or the coupons ("coupons", a list of coupon specs, as
tools/vectors/gen/cashflows/iborcoupon.py's functions build them), and the effective strikes ("strikes").
tests/NanoQL.Tests/Vectors/Cxx/Coupons/CouponPricerCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_iborcoupon", os.path.join(v.GEN_ROOT, "cashflows", "iborcoupon.py"))
ibc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ibc)

irx = ibc.irx
TODAY = ibc.TODAY
d = ibc.d

E3M = ibc.euribor((3, ql.Months), ibc.CURVE)
E6M = ibc.euribor((6, ql.Months), ibc.CURVE)
E6M_BARE = ibc.euribor((6, ql.Months), None, "EuriborBare")
SOFR = irx.overnight_spec("Sofr", 0, irx.US_SOFR, ibc.A360, ibc.CURVE_STEEP)
STRIKES = [0.025, 0.031, 0.04]
QUERIES = ["capletPrice", "capletRate", "floorletPrice", "floorletRate"]
BLACK = ibc.pricer_spec(ibc.VOL_LN)
BLACK_N = ibc.pricer_spec(ibc.VOL_N, ql.BlackIborCouponPricer.BivariateLognormal, 0.5)
BLACK_BARE = ibc.pricer_spec()
FIXING = {"type": "IndexFixingPricer", "adjustment": 0.0002}
FIXING_2 = {"type": "IndexFixingPricer", "adjustment": -0.0001}


def ibor(*args, pricer=None, **kw):
    return ibc.ibor_coupon_spec(*args, pricer=pricer, **kw)


def plain(*args, pricer=None, **kw):
    return dict(ibc.ibor_coupon_spec(*args, pricer=pricer, **kw), type="FloatingRateCoupon")


def query_cases():
    """(name, pricer spec, coupon spec, fixings)."""
    future = (d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2)
    past = (d(17, 3, 2025), 1e6, d(17, 9, 2024), d(17, 3, 2025), 2)
    stored = [(d(13, 9, 2024), 0.0331)]
    out = []
    for label, p in (("lognormal", BLACK), ("normal, bivariate lognormal", BLACK_N), ("no volatility", BLACK_BARE)):
        out += [
            (f"{label}: a future coupon", p, ibor(*future, E6M, pricer=p), None),
            (f"{label}: a future coupon in arrears", p, ibor(*future, E6M, 1.5, 0.001, in_arrears=True, pricer=p),
             None),
            (f"{label}: a coupon paid late", p, ibor(d(23, 1, 2026), *future[1:], E6M, pricer=p), None),
            (f"{label}: a past coupon", p, ibor(*past, E6M, pricer=p), stored),
        ]
    out += [
        ("a past coupon with no forecasting curve", BLACK, ibor(*past, E6M_BARE, pricer=BLACK), stored),
        ("a future coupon with no forecasting curve", BLACK, ibor(*future, E6M_BARE, pricer=BLACK), None),
        ("a floating-rate coupon", BLACK, plain(*future, E6M, pricer=BLACK), None),
        ("an Ibor coupon with no pricer of its own", BLACK, ibor(*future, E6M), None),
    ]
    return out


def leg_cases():
    """(name, setter, coupons, pricers)."""
    f6 = plain(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M)
    fon = plain(d(21, 4, 2025), 1e6, d(21, 1, 2025), d(21, 4, 2025), 0, SOFR)
    i6 = ibor(d(17, 7, 2026), 1e6, d(19, 1, 2026), d(17, 7, 2026), 2, E6M)
    i3 = ibor(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), 2, E3M, pricer=BLACK_N)
    capped_ibor = ibor(d(19, 1, 2027), 1e6, d(17, 7, 2026), d(19, 1, 2027), 2, E6M, cap=0.04, capped=True)
    capped_plain = ibc.capped_coupon_spec(f6, floor=0.02)
    capped_over_ibor = ibc.capped_coupon_spec(i6, cap=0.05)
    one = "setCouponPricer(Leg,FloatingRateCouponPricer)"
    many = "setCouponPricers(Leg,vector<FloatingRateCouponPricer>)"
    two, three, four = "setCouponPricers(Leg,p1,p2)", "setCouponPricers(Leg,p1,p2,p3)", "setCouponPricers(Leg,p1,p2,p3,p4)"
    mixed = [f6, i6, capped_ibor, capped_plain, capped_over_ibor, fon, i3]
    return [
        ("the default case with the harness's pricer", one, [f6, fon, capped_plain], [FIXING]),
        ("the default case with a Black pricer", one, [f6, i6, capped_plain, fon], [BLACK]),
        ("an Ibor coupon refusing the harness's pricer", one, [f6, i6], [FIXING]),
        ("a capped coupon over an Ibor coupon refusing the harness's pricer", one, [capped_over_ibor], [FIXING]),
        ("a pricer per cash flow, the last repeated", many, [i6, i3, capped_ibor, i6], [BLACK, BLACK_N]),
        ("a pricer per cash flow over mixed coupons", many, [f6, i6, capped_plain], [FIXING, BLACK, FIXING_2]),
        ("as many pricers as cash flows", many, [i6, i3], [BLACK_N, BLACK]),
        ("more pricers than cash flows", many, [i6, i3], [BLACK, BLACK, BLACK]),
        ("an empty leg", many, [], [BLACK]),
        ("an Ibor coupon refusing its pricer in the list", many, [f6, i6], [FIXING]),
        ("first matching of two, the harness's first", two, mixed, [FIXING, BLACK]),
        ("first matching of two, Black first", two, mixed, [BLACK, FIXING]),
        ("first matching of two, neither for an Ibor coupon", two, mixed, [FIXING, FIXING_2]),
        ("first matching of three, a null first", three, mixed, [None, FIXING, BLACK]),
        ("first matching of three", three, mixed, [FIXING_2, BLACK_N, BLACK]),
        ("first matching of four", four, mixed, [FIXING, FIXING_2, BLACK_BARE, BLACK]),
        ("first matching of four over an empty leg", four, [], [FIXING, FIXING_2, BLACK_BARE, BLACK]),
    ]


def cases():
    with cxx.connect() as H:
        out = []
        for at_par in (True, False):
            for label, p in (("no explicit choice", ibc.pricer_spec()), ("indexed", ibc.pricer_spec(use_indexed=True)),
                             ("at par", ibc.pricer_spec(use_indexed=False))):
                with H.case_settings(v.enc(TODAY), use_at_par_coupons=at_par) as st:
                    out.append({"name": f"useIndexedCoupon: {label}, switch {'at par' if at_par else 'indexed'}",
                                "inputs": {"settings": st, "pricer": p},
                                "expected": {"useIndexedCoupon": H.call("IborCouponPricer::useIndexedCoupon", p)}})
        for k, (name, p, coupon, stored) in enumerate(query_cases()):
            fixings = None
            if stored is not None:
                fixings = {H.call("Index::name", coupon["index"]): [[v.enc(x), y] for x, y in stored]}
            with H.case_settings(v.enc(TODAY), use_at_par_coupons=k % 2 == 0, fixings=fixings) as st:
                expected = {
                    "swapletPrice": v.attempt(lambda: H.call("FloatingRateCouponPricer::swapletPrice", p, coupon)),
                    "swapletRate": v.attempt(lambda: H.call("FloatingRateCouponPricer::swapletRate", p, coupon))}
                for q in QUERIES:
                    expected[q] = [v.attempt(lambda q=q, x=x: H.call(f"FloatingRateCouponPricer::{q}", p, coupon, x))
                                   for x in STRIKES]
                out.append({"name": f"query: {name}",
                            "inputs": {"settings": st, "pricer": p, "coupon": coupon, "strikes": STRIKES},
                            "expected": expected})
        for k, (name, method, coupons, pricers) in enumerate(leg_cases()):
            with H.case_settings(v.enc(TODAY), use_at_par_coupons=k % 2 == 0) as st:
                if method.startswith("setCouponPricer("):
                    result = v.attempt(lambda: H.call(method, coupons, pricers[0]))
                elif method.startswith("setCouponPricers(Leg,vector"):
                    result = v.attempt(lambda: H.call(method, coupons, pricers))
                else:
                    result = v.attempt(lambda: H.call(method, coupons, *pricers))
                out.append({"name": f"{method}: {name}",
                            "inputs": {"settings": st, "setter": method, "coupons": coupons, "pricers": pricers},
                            "expected": {"pricers": result}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/couponpricer.hpp", cases)
