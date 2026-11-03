# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Black overnight coupon pricers (ql/cashflows/blackovernightindexedcouponpricer.hpp) and the
capped and floored overnight coupon they price (ql/cashflows/overnightindexedcoupon.hpp), on the wheel.

80 capped, floored and collared coupons, each with its own pricer: BlackCompoundingOvernightIndexedCouponPricer on
compounded coupons and BlackAveragingOvernightIndexedCouponPricer on averaged ones, on ConstantOptionletVolatility
surfaces (shifted lognormal with and without a displacement, and normal), the effective-volatility flag both ways, caps
and floors on the period rate or on the daily rates, naked or not, a spread added or compounded daily, gearings of 1,
1.5 and -1, past (determined), spanning and future periods. The compounded coupons take lookbacks and lockouts, which
choose the daily pricer's period on the lockout; the averaged ones sit on an index fixing two days back and on periods
starting or ending on holidays, where the averaging pricer chooses it on the fixing days. Each coupon's outputs are its
rate, amount, accrued amounts at two dates, price on a flat discount curve, cap, floor, effective cap and floor,
effective caplet and floorlet volatilities, the pricer's effective volatilities and its one-argument caplet and floorlet
rates at 4% after the rate, the underlying's rate, and its inspectors. A volatility left out, the flag with daily caps
and floors, and the averaging pricer on a compounded coupon record QuantLib's throws; the compounding pricer on an
averaged coupon, which QuantLib doesn't refuse, prices.

The cap-and-floor check at a negative gearing: with cap 0.01 and floor 0.05 the coupon builds at gearing -1 without
daily caps and floors, where QuantLib compares the floor with itself, and is refused at gearing 1 or with daily caps and
floors.

Legs of capped coupons given a pricer by setCouponPricer, which sets each coupon and then its underlying: the Black
pricers on their coupons (a daily cap and floor at gearing -1 among them, whose stored cap and floor setCouponPricer
must keep as they are), the plain compounding and averaging pricers (whose two-argument caplet rates QuantLib doesn't
implement), and the refusals of a pricer of the other averaging. Each leg's outputs are the capped coupons' and their
underlyings' rates, or the call's throw.

A capped coupon whose underlying's spread QuantLib's Black compounding pricer drops over the telescopic forecast range,
which nanoql compounds (overnightindexedcoupon.py's spread_dropped), is generated and left out; the random draws all
come before it, so the other cases are kept.

Each case's inputs hold its settings and its "capped" spec (or "coupons") and "pricer", as
tools/vectors/gen/cashflows/overnightindexedcoupon.py writes them. Read back by
tests/NanoQL.Tests/Vectors/Coupons/BlackOvernightIndexedCouponPricerVectorTests.cs."""
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

TODAY = ql.Date(1, 7, 2025)
VOLS = [oic.vol_spec(TODAY, 0.2), oic.vol_spec(TODAY, 0.35, displacement=0.01),
        oic.vol_spec(TODAY, 0.0075, oic.irx.US_SOFR, ql.ModifiedFollowing, oic.A360, ql.Normal),
        oic.vol_spec(TODAY - 5, 0.3)]


def capped_case(k, rng):
    simple = k % 3 == 2
    timing = ["future", "spanning", "past", "future"][(k // 3) % 4]
    weeks = 3 + rng.randrange(10)
    if timing == "past":
        start = TODAY - (7 * weeks + 10)
    elif timing == "spanning":
        start = TODAY - (5 + rng.randrange(7 * weeks - 8))
    else:
        start = TODAY + (3 + rng.randrange(60))
    if simple and k % 2 == 0:
        start = start - (start.weekday() % 7)  # a Saturday start: the value dates begin on the Friday before
    end = start + 7 * weeks
    if simple and k % 5 == 0 and ql.TARGET().isBusinessDay(end):
        end = end + (7 - end.weekday()) % 7  # a Saturday end
    ix = 3 if simple else rng.choice([0, 0, 1])
    index = oic.index_spec(ix, TODAY, [0.04, 0.032, 0.047][k % 3], oic.A360)
    compound_spread = not simple and rng.random() < 0.3
    gearing = 1.0 if compound_spread else rng.choice([1.0, 1.0, 1.5, -1.0])
    spread = rng.choice([0.0, 0.0012, -0.0005])
    if simple:
        underlying = oic.coupon_spec(index, end, start, end, 1e6, gearing, spread,
                                     averaging=ql.RateAveraging.Simple)
    else:
        underlying = oic.coupon_spec(index, end, start, end, 1e6, gearing, spread, lookback=rng.choice([None, 2]),
                                     lockout=rng.choice([0, 0, 2]), shift=rng.random() < 0.3,
                                     compound_spread=compound_spread)
    shape = k % 4
    cap = None if shape == 1 else round(0.038 + 0.012 * rng.random(), 4)
    floor = None if shape == 0 else round(0.025 + 0.012 * rng.random(), 4)
    daily = rng.random() < 0.45
    naked = rng.random() < 0.2
    vol = None if k in (13, 46) else VOLS[k % len(VOLS)]
    effective = rng.random() < 0.2
    kind = "BlackAveraging" if simple else "BlackCompounding"
    if k in (30, 70):
        kind = "BlackAveraging"  # the averaging pricer on a compounded coupon, which QuantLib refuses
    if k in (29, 71):
        kind = "BlackCompounding"  # the compounding pricer on an averaged coupon, which QuantLib accepts
    return oic.capped_spec(underlying, cap, floor, naked, daily), oic.pricer_spec(kind, vol, effective)


DISCOUNT = oic.irx.curve_spec(TODAY, 0.035, oic.A365)


def capped_outputs(c, p, nominal, accrual_dates):
    return {"built": True,
            "rate": v.attempt(c.rate),
            "amount": oic.amounts([c.amount], nominal)[0],
            "accruedAmount": oic.amounts([(lambda d=d: c.accruedAmount(d)) for d in accrual_dates], nominal),
            "price": oic.amounts([lambda: c.price(oic.irx.handle(DISCOUNT))], nominal)[0],
            "cap": v.enc(c.cap()), "floor": v.enc(c.floor()),
            "effectiveCap": v.attempt(c.effectiveCap), "effectiveFloor": v.attempt(c.effectiveFloor),
            "effectiveCapletVolatility": v.attempt(c.effectiveCapletVolatility),
            "effectiveFloorletVolatility": v.attempt(c.effectiveFloorletVolatility),
            "underlyingRate": v.attempt(c.underlying().rate),
            "isCapped": c.isCapped(), "isFloored": c.isFloored(), "nakedOption": c.nakedOption(),
            "dailyCapFloor": c.dailyCapFloor(), "compoundSpreadDaily": c.compoundSpreadDaily(),
            "averagingMethod": int(c.averagingMethod()), "fixingDate": v.enc(c.fixingDate()),
            "nominal": c.nominal(), "convexityAdjustment": v.attempt(c.convexityAdjustment),
            **pricer_volatilities(c, p)}


def pricer_volatilities(c, p):
    """The pricer's effective volatilities after the coupon's rate, read from the pricer itself, then its one-argument
    caplet and floorlet rates at 4%, on the period rate of the coupon it was initialized with; only where the rate
    didn't throw, since a refused rate leaves the pricer's state partway."""
    try:
        c.rate()
    except RuntimeError:
        return {}
    return {"pricerEffectiveCapletVolatility": v.enc(p.effectiveCapletVolatility()),
            "pricerEffectiveFloorletVolatility": v.enc(p.effectiveFloorletVolatility()),
            "pricerCapletRate": v.attempt(lambda: p.capletRate(0.04)),
            "pricerFloorletRate": v.attempt(lambda: p.floorletRate(0.04))}


def fixings(index, spec):
    start = ql.Date(spec["underlying"]["startDate"] if "underlying" in spec else spec["startDate"])
    return oic.fixings_for(index, start - 20, TODAY - 1, random.Random(start.serialNumber()))


def capped_case_doc(name, spec, pspec):
    """The case, or None where QuantLib drops the underlying's spread."""
    index = oic.make_index(spec["underlying"]["index"])
    start, end = ql.Date(spec["underlying"]["startDate"]), ql.Date(spec["underlying"]["endDate"])
    accrual_dates = [start + (end - start) // 2, end]
    with v.case_settings(TODAY, fixings=[(index, fixings(index, spec))]) as st:
        try:
            c = oic.make_capped(spec, index)
        except RuntimeError:
            expected = {"built": dict(v.THROWS)}
        else:
            if pspec["type"] in ("Compounding", "BlackCompounding") and oic.spread_dropped(
                    oic.make_coupon(spec["underlying"], index), TODAY):
                return None
            p = oic.make_pricer(pspec)
            c.setPricer(p)
            expected = capped_outputs(c, p, spec["underlying"]["nominal"], accrual_dates)
        return {"name": name, "inputs": {"settings": st, "capped": spec, "pricer": pspec,
                                         "accrualDates": v.enc(accrual_dates), "discountCurve": DISCOUNT},
                "expected": expected}


def d7_cases():
    index = oic.index_spec(0, TODAY, 0.04)
    start, end = TODAY + 30, TODAY + 120
    vol = VOLS[0]
    out = []
    for gearing, daily in ((-1.0, False), (1.0, False), (-1.0, True), (1.0, True)):
        u = oic.coupon_spec(index, end, start, end, 1e6, gearing)
        out.append((f"D7 gearing {gearing:+.0f}, daily {daily}, cap 0.01 below floor 0.05",
                    oic.capped_spec(u, 0.01, 0.05, False, daily), oic.pricer_spec("BlackCompounding", vol)))
    return out


def leg_cases():
    index = oic.index_spec(0, TODAY, 0.041)
    vol = VOLS[1]
    comp = [oic.coupon_spec(index, TODAY + 60 + 45 * i, TODAY - 15 + 45 * i, TODAY + 60 + 45 * i, 1e6, g)
            for i, g in enumerate((1.0, -1.0, 1.5))]
    simp = [oic.coupon_spec(index, TODAY + 60 + 45 * i, TODAY - 15 + 45 * i, TODAY + 60 + 45 * i, 1e6, 1.0,
                            averaging=ql.RateAveraging.Simple) for i in range(3)]
    capped_comp = [oic.capped_spec(comp[0], 0.045, 0.03), oic.capped_spec(comp[1], 0.05, 0.01, False, True),
                   oic.capped_spec(comp[2], None, 0.035, True, True)]
    capped_simp = [oic.capped_spec(simp[0], 0.045, None), oic.capped_spec(simp[1], 0.05, 0.03, False, True),
                   oic.capped_spec(simp[2], None, 0.035, True)]
    return [
        ("Black compounding pricer on capped compounded coupons", capped_comp,
         oic.pricer_spec("BlackCompounding", vol)),
        ("Black averaging pricer on capped averaged coupons", capped_simp, oic.pricer_spec("BlackAveraging", vol)),
        ("compounding pricer on capped compounded coupons", capped_comp, oic.pricer_spec("Compounding", vol)),
        ("averaging pricer on capped averaged coupons", capped_simp,
         oic.pricer_spec("ArithmeticAveraged", vol, by_approx=True)),
        ("Black averaging pricer on capped compounded coupons", capped_comp, oic.pricer_spec("BlackAveraging", vol)),
        ("Black compounding pricer on capped averaged coupons", capped_simp,
         oic.pricer_spec("BlackCompounding", vol)),
    ]


def leg_case_doc(name, specs, pspec):
    index = oic.make_index(specs[0]["underlying"]["index"])
    with v.case_settings(TODAY, fixings=[(index, fixings(index, specs[0]))]) as st:
        leg = [oic.make_capped(s, index) for s in specs]
        try:
            ql.setCouponPricer(leg, oic.make_pricer(pspec))
        except RuntimeError:
            expected = {"setCouponPricer": dict(v.THROWS)}
        else:
            expected = {"setCouponPricer": True,
                        "caps": [v.enc(c.cap()) for c in leg], "floors": [v.enc(c.floor()) for c in leg],
                        "rates": [v.attempt(c.rate) for c in leg],
                        "underlyingRates": [v.attempt(c.underlying().rate) for c in leg]}
        return {"name": name, "inputs": {"settings": st, "coupons": specs, "pricer": pspec}, "expected": expected}


def cases():
    rng = random.Random(v.SEED + 2)
    out = []
    for k in range(80):
        spec, pspec = capped_case(k, rng)
        out.append(capped_case_doc(f"capped {k}: {pspec['type']}", spec, pspec))
    for name, spec, pspec in d7_cases():
        out.append(capped_case_doc(name, spec, pspec))
    for name, specs, pspec in leg_cases():
        out.append(leg_case_doc(name, specs, pspec))
    return [c for c in out if c is not None]


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/blackovernightindexedcouponpricer.hpp", cases)
