# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's compounding and arithmetic-averaging overnight coupon pricers
(ql/cashflows/overnightindexedcouponpricer.hpp), on the wheel, through 60 coupons and a dozen legs.

Each coupon is priced with a pricer set on it: compounded coupons with CompoundingOvernightIndexedCouponPricer (with
and without an optionlet volatility and the effective-volatility flag, which it doesn't read), averaged ones with
ArithmeticAveragedOvernightIndexedCouponPricer over the approximation both ways, mean reversion 0.03 and 0, and
volatility 0 and 0.01: a mean reversion of 0 gives NaN wherever a fixing is forecast, as QuantLib's formulas divide by
its cube (D14). The coupons are past, spanning and future, on overnight indexes fixing 0 and 2 days back, with
lookbacks, lockouts and the shift on the compounded ones. Each case's outputs are the coupon's rate, amount and
accrued amounts at three dates, its effective spread and index fixing (compounded coupons), and the pricer's
accessors: the effective-volatility flag, whether it holds a volatility, and its five throwing members (swapletPrice,
capletPrice, capletRate, floorletPrice, floorletRate). The effective volatilities, which QuantLib keeps after a caplet
or floorlet, are the harness's (tools/vectors/gen/cxx/cashflows/overnightindexedcouponpricer.py) and the Black file's. A pricer's setCapletVolatility and setEffectiveVolatilityInput are read back
on every fifth case, the coupon priced again after them.

The legs set a pricer with setCouponPricer, QuantLib's visitor: a compounding pricer on compounded coupons, an
averaging pricer on averaged ones, the Black pricers on each, and the refusals of a compounding pricer on averaged
coupons, an averaging pricer on compounded ones, and a mixed leg. Each leg's outputs are the coupons' rates after the
call, or the call's throw.

A compounded coupon whose spread QuantLib's compounding pricer drops over the telescopic forecast range, which nanoql
compounds (the S-rx plan's C3 review, B1-1; overnightindexedcoupon.py's spread_dropped), is generated and left out, so
the random sequence of the cases after it is kept.

The wheel's ArithmeticAveragedOvernightIndexedCouponPricer is built with keyword arguments only. Each case's inputs
hold its settings, its "coupon" (or "coupons") and its "pricer", as tools/vectors/gen/cashflows/overnightindexedcoupon.py
writes them. Read back by tests/NanoQL.Tests/Vectors/Coupons/OvernightIndexedCouponPricerVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_overnightindexedcoupon",
                                               os.path.join(v.GEN_ROOT, "cashflows", "overnightindexedcoupon.py"))
oic = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(oic)

TODAY = ql.Date(3, 3, 2025)


def has_vol(pricer):
    try:
        pricer.capletVolatility().currentLink()
    except RuntimeError:
        return False
    return True


def pricer_outputs(p):
    return {"effectiveVolatilityInput": p.effectiveVolatilityInput(), "hasCapletVolatility": has_vol(p),
            "swapletPrice": v.attempt(p.swapletPrice), "capletPrice": v.attempt(lambda: p.capletPrice(0.03)),
            "capletRate": v.attempt(lambda: p.capletRate(0.03)),
            "floorletPrice": v.attempt(lambda: p.floorletPrice(0.03)),
            "floorletRate": v.attempt(lambda: p.floorletRate(0.03))}


def priced(c, spec, dates):
    out = {"rate": v.attempt(c.rate), "amount": oic.amounts([c.amount], spec["nominal"])[0],
           "accruedAmount": oic.amounts([(lambda d=d: c.accruedAmount(d)) for d in dates], spec["nominal"])}
    if spec["averagingMethod"] == ql.RateAveraging.Compound:
        out["effectiveSpread"] = v.attempt(c.effectiveSpread)
        out["effectiveIndexFixing"] = v.attempt(c.effectiveIndexFixing)
    return out


def coupon_and_pricer(k, rng):
    simple = k % 2 == 1
    ix = [0, 3][(k // 2) % 2]
    timing = ["past", "spanning", "future"][(k // 4) % 3]
    weeks = 2 + rng.randrange(4)
    if timing == "past":
        start = TODAY - (7 * weeks + 15)
    elif timing == "spanning":
        start = TODAY - (3 + rng.randrange(7 * weeks - 4))
    else:
        start = TODAY + (2 + rng.randrange(30))
    end = start + 7 * weeks
    index = oic.index_spec(ix, TODAY, [0.042, 0.0315, 0.051][k % 3], oic.A360, [ql.Continuous, ql.Simple][k % 2])
    vol = oic.vol_spec(TODAY, 0.25) if k % 3 == 0 else None
    if simple:
        spec = oic.coupon_spec(index, end, start, end, 1e6, rng.choice([1.0, 1.25]), rng.choice([0.0, 0.001]),
                               averaging=ql.RateAveraging.Simple, rounding=rng.choice([None, 6]))
        mrs = [0.03, 0.0][(k // 3) % 2]
        vol_hw = [0.0, 0.01][(k // 5) % 2]
        pricer = oic.pricer_spec("ArithmeticAveraged", vol, k % 4 == 1, mrs, vol_hw, (k // 7) % 2 == 1)
    else:
        lookback = rng.choice([None, 0, 2])
        shift = rng.random() < 0.4
        spec = oic.coupon_spec(index, end, start, end, 1e6, 1.0, rng.choice([0.0, 0.0015]), lookback=lookback,
                               lockout=rng.choice([0, 0, 3]), shift=shift, compound_spread=rng.random() < 0.4)
        pricer = oic.pricer_spec("Compounding", vol, k % 4 == 0)
    dates = [start + 2, start + (end - start) // 2, end - 1]
    return spec, pricer, dates


def coupon_case(k, rng):
    """The case, or None where QuantLib drops the coupon's spread (B1-1), after the fixings are drawn."""
    spec, pspec, dates = coupon_and_pricer(k, rng)
    index = oic.make_index(spec["index"])
    pairs = oic.fixings_for(index, ql.Date(spec["startDate"]) - 20, TODAY - 1, rng)
    with v.case_settings(TODAY, fixings=[(index, pairs)]) as st:
        c = oic.make_coupon(spec, index)
        if pspec["type"] == "Compounding" and oic.spread_dropped(c, TODAY):
            return None
        p = oic.make_pricer(pspec)
        c.setPricer(p)
        expected = priced(c, spec, dates)
        expected.update(pricer_outputs(p))
        inputs = {"settings": st, "coupon": spec, "pricer": pspec, "accrualDates": v.enc(dates)}
        if k % 5 == 0:
            # setCapletVolatility and setEffectiveVolatilityInput, then the coupon priced again
            nv = oic.vol_spec(TODAY, 0.4, irx_target(), ql.Following, oic.A360)
            p.setCapletVolatility(oic.vol_handle(nv))
            p.setEffectiveVolatilityInput(not pspec["effectiveVolatilityInput"])
            inputs["newVol"] = nv
            expected["setEffectiveVolatilityInput"] = p.effectiveVolatilityInput()
            expected["setHasCapletVolatility"] = has_vol(p)
            expected["setRate"] = v.attempt(c.rate)
            p.setCapletVolatility()
            expected["resetHasCapletVolatility"] = has_vol(p)
        return {"name": f"coupon {k}: {pspec['type']}", "inputs": inputs, "expected": expected}


def irx_target():
    return oic.irx.TARGET


def leg_cases(rng):
    index = oic.index_spec(0, TODAY, 0.043)
    compound = [oic.coupon_spec(index, TODAY + 40 + 30 * i, TODAY - 10 + 30 * i, TODAY + 40 + 30 * i)
                for i in range(3)]
    simple = [oic.coupon_spec(index, TODAY + 40 + 30 * i, TODAY - 10 + 30 * i, TODAY + 40 + 30 * i,
                              averaging=ql.RateAveraging.Simple) for i in range(3)]
    vol = oic.vol_spec(TODAY, 0.3)
    out = [
        ("compounding pricer on compounded coupons", compound, oic.pricer_spec("Compounding")),
        ("averaging pricer on averaged coupons", simple, oic.pricer_spec("ArithmeticAveraged", by_approx=True)),
        ("averaging pricer with a convexity adjustment on averaged coupons", simple,
         oic.pricer_spec("ArithmeticAveraged", mean_reversion=0.05, volatility=0.01)),
        ("Black compounding pricer on compounded coupons", compound, oic.pricer_spec("BlackCompounding", vol)),
        ("Black averaging pricer on averaged coupons", simple, oic.pricer_spec("BlackAveraging", vol)),
        ("compounding pricer on averaged coupons", simple, oic.pricer_spec("Compounding")),
        ("averaging pricer on compounded coupons", compound, oic.pricer_spec("ArithmeticAveraged")),
        ("Black averaging pricer on compounded coupons", compound, oic.pricer_spec("BlackAveraging", vol)),
        ("compounding pricer on a mixed leg", compound[:1] + simple[1:], oic.pricer_spec("Compounding")),
        ("averaging pricer on a mixed leg", simple[:1] + compound[1:], oic.pricer_spec("ArithmeticAveraged")),
    ]
    cases = []
    for name, specs, pspec in out:
        idx = oic.make_index(index)
        pairs = oic.fixings_for(idx, TODAY - 40, TODAY - 1, rng)
        with v.case_settings(TODAY, fixings=[(idx, pairs)]) as st:
            leg = [oic.make_coupon(s, idx) for s in specs]
            try:
                ql.setCouponPricer(leg, oic.make_pricer(pspec))
            except RuntimeError:
                expected = {"setCouponPricer": dict(v.THROWS)}
            else:
                expected = {"setCouponPricer": True, "rates": [v.attempt(c.rate) for c in leg]}
            cases.append({"name": name, "inputs": {"settings": st, "coupons": specs, "pricer": pspec},
                          "expected": expected})
    return cases


def cases():
    rng = random.Random(v.SEED + 1)
    out = [c for c in (coupon_case(k, rng) for k in range(60)) if c is not None]
    out += leg_cases(rng)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/overnightindexedcouponpricer.hpp", cases)
