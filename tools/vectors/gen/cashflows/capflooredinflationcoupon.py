# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's capped and floored year-on-year inflation coupon
(ql/cashflows/capflooredinflationcoupon.hpp), through the wheel's CappedFlooredYoYInflationCoupon on its own terms and
yoyInflationLeg with caps and floors.

- "coupon" cases: the coupons of cashflows/inflationcouponpricer.py, fixed or forecast, on ratio and quoted indexes,
  and two with a negative gearing, each capped, floored, collared or with neither, under the Black, unit-displaced
  Black and Bachelier pricers (each that generator's first volatility): isCapped, isFloored, cap and floor (null for
  none), effectiveCap where the fixing is capped and effectiveFloor where it is floored, underlyingRate, rate, amount,
  fixingDate and indexFixing. QuantLib's effectiveCap of a fixing that isn't capped reads a member nothing set
  (capflooredinflationcoupon.cpp:127, .hpp:135), and its effectiveFloor likewise, so neither is recorded there
  (tests/NanoQL.Tests/Divergences/CappedYoYEffectiveStrikeTests.cs). Every effective strike is positive: a Black
  optionlet struck at or below 0 is the S-cms plan's CMR5 (xxiii)
  (tests/NanoQL.Tests/Divergences/BlackYoYBelowShiftTests.cs).
- "leg" cases: yoyInflationLeg with caps, floors or both, one value or one per coupon, a null entry leaving its coupon
  without an option, and a zero gearing, whose fixed coupon pays the spread capped and floored; the Black pricer set by
  setCouponPricer: per flow its kind ("CappedFlooredYoYInflationCoupon", "YoYInflationCoupon" or "FixedRateCoupon"),
  date, rate and amount, a coupon's fixing date, and a capped coupon's cap and floor. A leg QuantLib refuses records
  only "construction": {"throws": true}. Each flow's rate and amount are read in turn: QuantLib's coupon prices its
  caplet and floorlet on the pricer's state, which its swaplet rate sets only when first computed
  (inflationcoupon.cpp:64-75, capflooredinflationcoupon.cpp:92-105), so with the leg's one pricer a coupon read again
  after another reads that other's state (tests/NanoQL.Tests/Divergences/CappedYoYPricerStateTests.cs).

A case's inputs hold its settings, the index spec and fixings (cashflows/indexedcashflow.py's), the pricer's kind, its
volatility and nominal curve (cashflows/inflationcouponpricer.py's), and the coupon's or leg's arguments, a cap or
floor null for none (QuantLib's Null<Rate>()), a leg's schedule as cashflows/cashflows.py's. Read back by
tests/NanoQL.Tests/Vectors/Coupons/CappedFlooredYoYInflationCouponVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, *path):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


icp = _load("wheel_inflationcouponpricer", "cashflows", "inflationcouponpricer.py")
icf = icp.icf
cf = icf.cf

TODAY = icp.TODAY
UK = cf.UK_SETTLEMENT
A365 = cf.A365
M = icp.M
NULL = ql.nullDouble()

# (label, index, payment, nominal, start, end, fixing days, lag months, interpolation, gearing, spread, cap, floor)
COUPONS = icp.COUPONS + [
    ("forecast, negative gearing", "ratio", ql.Date(16, ql.March, 2026), 1e6, ql.Date(15, ql.March, 2025),
     ql.Date(15, ql.March, 2026), 0, 2, ql.CPI.Flat, -0.8, 0.04, 0.03, 0.01),
    ("fixed, negative gearing", "quoted", ql.Date(15, ql.May, 2024), 1e6, ql.Date(15, ql.May, 2023),
     ql.Date(15, ql.May, 2024), 0, 2, ql.CPI.Flat, -1.0, 0.05, 0.04, 0.02),
]
OPTIONS = [("capped", True, False), ("floored", False, True), ("collared", True, True), ("plain", False, False)]


def optional(x):
    return None if x == NULL else x


def coupon_cases():
    out = []
    k = 0
    for kind, vol_specs in icp.VOLS.items():
        vol_spec = vol_specs[0]
        for label, which, payment, nominal, start, end, days, lag, interp, gearing, spread, cap, floor in COUPONS:
            spec, fixings = icp.INDEXES[which]
            for option, has_cap, has_floor in OPTIONS:
                c_cap, c_floor = (cap if has_cap else None), (floor if has_floor else None)
                with v.case_settings(TODAY) as st:
                    index = icf.index_with_fixings(spec, fixings)
                    c = icp.capped(index, payment, nominal, start, end, days, lag, interp, gearing, spread, c_cap,
                                   c_floor)
                    ql.setCouponPricer([c], icp.pricer(kind, vol_spec))
                    expected = {"isCapped": c.isCapped(), "isFloored": c.isFloored(), "cap": optional(c.cap()),
                                "floor": optional(c.floor())}
                    if c.isCapped():
                        expected["effectiveCap"] = c.effectiveCap()
                    if c.isFloored():
                        expected["effectiveFloor"] = c.effectiveFloor()
                    expected.update({"underlyingRate": v.attempt(c.underlyingRate), "rate": v.attempt(c.rate),
                                     "amount": v.attempt(c.amount), "fixingDate": v.enc(c.fixingDate()),
                                     "indexFixing": v.attempt(c.indexFixing)})
                    out.append({"name": f"coupon {kind} {label}, {option} / {k}",
                                "inputs": {"settings": st, "kind": "coupon", "index": spec, "fixings": fixings,
                                           "pricer": kind, "volatility": vol_spec, "nominalCurve": icf.NOMINAL,
                                           "paymentDate": v.enc(payment), "nominal": nominal,
                                           "startDate": v.enc(start), "endDate": v.enc(end), "fixingDays": days,
                                           "observationLag": [lag, M], "interpolation": int(interp),
                                           "dayCounter": A365, "gearing": gearing, "spread": spread, "cap": c_cap,
                                           "floor": c_floor},
                                "expected": expected})
                k += 1
    return out


ANNUAL = cf.schedule_spec(ql.Date(15, ql.June, 2024), ql.Date(15, ql.June, 2030), ql.Period(1, ql.Years), UK,
                          ql.Unadjusted, ql.DateGeneration.Backward, False)
PAST = cf.schedule_spec(ql.Date(1, ql.May, 2022), ql.Date(1, ql.May, 2027), ql.Period(1, ql.Years), UK,
                        ql.Following, ql.DateGeneration.Forward, False)


def leg_spec(schedule, lag, interp, notionals, adjustment, fixing_days=(), gearings=(), spreads=(), caps=(),
             floors=()):
    return {"schedule": schedule, "paymentCalendar": UK, "observationLag": [lag, M], "interpolation": int(interp),
            "notionals": list(notionals), "paymentDayCounter": A365, "paymentAdjustment": int(adjustment),
            "fixingDays": list(fixing_days), "gearings": list(gearings), "spreads": list(spreads),
            "caps": list(caps), "floors": list(floors)}


# (label, index, spec)
LEGS = [
    ("one cap", "ratio", leg_spec(ANNUAL, 2, ql.CPI.Flat, [1e6], ql.ModifiedFollowing, caps=[0.035])),
    ("one floor", "ratio", leg_spec(ANNUAL, 2, ql.CPI.Linear, [1e6], ql.ModifiedFollowing, floors=[0.02])),
    ("collars per coupon", "ratio",
     leg_spec(ANNUAL, 2, ql.CPI.Flat, [1e6, 9e5], ql.Following, (), (1.0, 1.2), (0.0, 0.001),
              (0.04, 0.035, 0.03), (0.01, 0.015))),
    ("a null cap leaves a coupon plain", "ratio",
     leg_spec(ANNUAL, 2, ql.CPI.Flat, [1e6], ql.ModifiedFollowing, caps=[0.035, None, 0.03])),
    ("a zero gearing is a fixed coupon, capped and floored", "ratio",
     leg_spec(ANNUAL, 2, ql.CPI.Flat, [1e6], ql.ModifiedFollowing, (), (1.0, 0.0, 1.0, 0.0), (0.0, 0.05, 0.0, 0.001),
              (0.04,), (0.02,))),
    ("quoted, past start", "quoted",
     leg_spec(PAST, 2, ql.CPI.Flat, [2e6], ql.ModifiedFollowing, (1,), (), (), (0.045,), (0.015,))),
    ("too many caps", "ratio", leg_spec(ANNUAL, 2, ql.CPI.Flat, [1e6], ql.ModifiedFollowing, caps=[0.04] * 7)),
    ("too many floors", "ratio", leg_spec(ANNUAL, 2, ql.CPI.Flat, [1e6], ql.ModifiedFollowing, floors=[0.0] * 7)),
]


def nulls(values):
    return [NULL if x is None else x for x in values]


def make_leg(spec, index):
    return ql.yoyInflationLeg(cf.schedule(spec["schedule"]), cf.calendar(spec["paymentCalendar"]), index,
                              ql.Period(spec["observationLag"][0], spec["observationLag"][1]), spec["interpolation"],
                              spec["notionals"], cf.day_counter(spec["paymentDayCounter"]), spec["paymentAdjustment"],
                              spec["fixingDays"][0] if spec["fixingDays"] else 0, spec["gearings"], spec["spreads"],
                              nulls(spec["caps"]), nulls(spec["floors"]))


def leg_cases():
    out = []
    vol_spec = icp.VOLS["Black"][0]
    for k, (label, kind, spec) in enumerate(LEGS):
        index_spec, fixings = icp.INDEXES[kind]
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(index_spec, fixings)
            inputs = {"settings": st, "kind": "leg", "index": index_spec, "fixings": fixings, "pricer": "Black",
                      "volatility": vol_spec, "nominalCurve": icf.NOMINAL, "leg": spec}
            try:
                leg = make_leg(spec, index)
            except RuntimeError:
                out.append({"name": f"leg {label} / {k}", "inputs": inputs,
                            "expected": {"construction": dict(v.THROWS)}})
                continue
            ql.setCouponPricer(leg, icp.pricer("Black", vol_spec))
            capped = [ql.as_capped_floored_yoy_inflation_coupon(f) for f in leg]
            yoy = [ql.as_yoy_inflation_coupon(f) for f in leg]
            coupons = [ql.as_coupon(f) for f in leg]
            rates, amounts = [], []
            for c, f in zip(coupons, leg):
                rates.append(v.attempt(c.rate))
                amounts.append(v.attempt(f.amount))
            kinds = ["CappedFlooredYoYInflationCoupon" if c is not None else
                     "YoYInflationCoupon" if y is not None else "FixedRateCoupon" for c, y in zip(capped, yoy)]
            out.append({"name": f"leg {label} / {k}", "inputs": inputs,
                        "expected": {
                            "kinds": kinds, "dates": v.enc([f.date() for f in leg]),
                            "fixingDates": [v.enc(y.fixingDate()) if y is not None else None for y in yoy],
                            "caps": [optional(c.cap()) if c is not None else None for c in capped],
                            "floors": [optional(c.floor()) if c is not None else None for c in capped],
                            "rates": rates, "amounts": amounts}})
    return out


def make_cases():
    return coupon_cases() + leg_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/capflooredinflationcoupon.hpp", make_cases)
