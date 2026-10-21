# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the overnight coupon pricers' members that the wheel doesn't reach
(ql/cashflows/overnightindexedcouponpricer.hpp, ql/cashflows/blackovernightindexedcouponpricer.hpp), each output
computed by the C++ harness on a pricer initialized with the coupon, as a coupon initializes its pricer before it
asks: averageRate(const Date&) at four dates, the two-argument capletRate and floorletRate at two strikes, on the period
rate and on the daily rates, the effective caplet and floorlet volatilities after them, and the compounding pricer's
effectiveSpread() and effectiveIndexFixing().

40 pairs of a pricer and a coupon: the compounding pricer and the arithmetic-averaging pricer (the approximation both
ways, convexity volatilities of 0 and 0.01), whose two-argument caplet rates QuantLib doesn't implement, and the two
Black pricers on ConstantOptionletVolatility surfaces (the effective-volatility flag both ways); plain and capped
coupons (a pricer prices a capped coupon's underlying); past, spanning and future periods; lookbacks, lockouts and the
shift on compounded coupons, a spread compounded daily on some. A refused value is {"throws": true}: the Black
averaging pricer on a compounded coupon, a daily caplet with the effective-volatility flag, a missing volatility.

A compounding pricer on a coupon whose spread it drops over the telescopic forecast range, which nanoql compounds (the
S-rx plan's C3 review, B1-1; overnightindexedcoupon.py's spread_dropped), is generated and left out, after its draws,
so the random sequence of the cases after it is kept.

Each case's inputs hold its settings (the fixings stored under the index's name), the "pricer" and "coupon" specs, as
tools/vectors/gen/cashflows/overnightindexedcoupon.py writes them, the "dates" and the "strikes".
tests/NanoQL.Tests/Vectors/Cxx/Coupons/OvernightIndexedCouponPricerCxxVectorTests.cs reads it back."""
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

_spec = importlib.util.spec_from_file_location("wheel_overnightindexedcoupon",
                                               os.path.join(v.GEN_ROOT, "cashflows", "overnightindexedcoupon.py"))
oic = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(oic)

TODAY = ql.Date(1, 7, 2025)
VOL = oic.vol_spec(TODAY, 0.22)
NORMAL = oic.vol_spec(TODAY, 0.008, oic.irx.US_SOFR, ql.Following, oic.A360, ql.Normal)


def pair(k, rng):
    kind = ["Compounding", "ArithmeticAveraged", "BlackCompounding", "BlackAveraging"][k % 4]
    simple = kind in ("ArithmeticAveraged", "BlackAveraging")
    if k in (11, 27):
        simple = not simple  # the Black averaging pricer on a compounded coupon, and its converse
    timing = ["future", "spanning", "past"][(k // 4) % 3]
    weeks = 3 + rng.randrange(8)
    if timing == "past":
        start = TODAY - (7 * weeks + 12)
    elif timing == "spanning":
        start = TODAY - (4 + rng.randrange(7 * weeks - 6))
    else:
        start = TODAY + (3 + rng.randrange(50))
    end = start + 7 * weeks
    ix = 3 if simple and k % 3 == 0 else rng.choice([0, 1])
    index = oic.index_spec(ix, TODAY, [0.039, 0.044, 0.028][k % 3], oic.A360)
    if simple:
        coupon = oic.coupon_spec(index, end, start, end, 1e6, rng.choice([1.0, 1.5]), rng.choice([0.0, 0.0008]),
                                 averaging=ql.RateAveraging.Simple)
    else:
        csd = rng.random() < 0.3
        coupon = oic.coupon_spec(index, end, start, end, 1e6, 1.0 if csd else rng.choice([1.0, 1.25]),
                                 rng.choice([0.0, 0.0011]), lookback=rng.choice([None, 0, 2]),
                                 lockout=rng.choice([0, 0, 2]), shift=rng.random() < 0.35, compound_spread=csd)
    if k % 5 == 1:
        coupon = oic.capped_spec(coupon, 0.045, 0.03, False, rng.random() < 0.5)
    vol = None if k == 22 else (NORMAL if k % 7 == 3 else VOL)
    if kind == "ArithmeticAveraged":
        pricer = oic.pricer_spec(kind, vol, False, 0.03, [0.0, 0.01][(k // 4) % 2], (k // 8) % 2 == 1)
    else:
        pricer = oic.pricer_spec(kind, vol, k % 6 == 2)
    return pricer, coupon


def underlying(spec):
    return spec["underlying"] if "underlying" in spec else spec


def cases():
    rng = random.Random(v.SEED + 3)
    with cxx.connect() as H:
        out = []
        for k in range(40):
            pricer, coupon = pair(k, rng)
            u = underlying(coupon)
            index = oic.make_index(u["index"])
            start, end = ql.Date(u["startDate"]), ql.Date(u["endDate"])
            pairs = oic.fixings_for(index, start - 20, TODAY - 1, rng)
            fixings = {index.name(): [[d.serialNumber(), x] for d, x in pairs]} if pairs else None
            dates = v.enc([start + 1, start + (end - start) // 3, start + 2 * (end - start) // 3, end])
            strikes = [0.035, 0.042]
            kw = {"fixings": fixings} if fixings else {}
            if pricer["type"] in ("Compounding", "BlackCompounding") and oic.spread_dropped(
                    oic.make_coupon(u, index), TODAY):
                continue
            with H.case_settings(v.enc(TODAY), **kw) as st:
                def each(method, *head):
                    return [v.attempt(lambda k_=k_, daily=daily: H.call(method, *head, k_, daily))
                            for daily in (False, True) for k_ in strikes]
                expected = {
                    "averageRate": [v.attempt(lambda d=d: H.call("OvernightIndexedCouponPricer::averageRate(Date)",
                                                                 pricer, coupon, d)) for d in dates],
                    "capletRate": each("OvernightIndexedCouponPricer::capletRate(Rate,bool)", pricer, coupon),
                    "floorletRate": each("OvernightIndexedCouponPricer::floorletRate(Rate,bool)", pricer, coupon),
                    "effectiveCapletVolatility": each("OvernightIndexedCouponPricer::effectiveCapletVolatility",
                                                      pricer, coupon),
                    "effectiveFloorletVolatility": each("OvernightIndexedCouponPricer::effectiveFloorletVolatility",
                                                        pricer, coupon),
                }
                if pricer["type"] in ("Compounding", "BlackCompounding"):
                    expected["effectiveSpread"] = v.attempt(
                        lambda: H.call("CompoundingOvernightIndexedCouponPricer::effectiveSpread", pricer, coupon))
                    expected["effectiveIndexFixing"] = v.attempt(
                        lambda: H.call("CompoundingOvernightIndexedCouponPricer::effectiveIndexFixing", pricer,
                                       coupon))
                out.append({"name": f"{pricer['type']} / {k}",
                            "inputs": {"settings": st, "pricer": pricer, "coupon": coupon, "dates": dates,
                                       "strikes": strikes},
                            "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/overnightindexedcouponpricer.hpp", cases)
