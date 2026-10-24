# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's range accrual floater (ql/cashflows/rangeaccrual.hpp), through the wheel's
RangeAccrualFloatersCoupon, RangeAccrualPricerByBgm and RangeAccrualLeg, on the evaluation date 15 January 2025 and a
Euribor 6M index on a flat 3.1% curve.

- "coupon" cases: a coupon starting after the evaluation date, every observation's fixing still to come (a coupon in
  progress is the S-cms plan's CMR5 (xviii), tests/NanoQL.Tests/Divergences/RangeAccrualStartedCouponTests.cs), its
  observations daily, weekly or monthly, its triggers around the forward, inside it, above it or wide enough to be
  infinite, with a gearing and a spread, priced by the BGM pricer on each of its three routes (no smile; the smile by
  a call spread; the smile by its correction), on flat smiles and on interpolated ones, at a correlation of 1, 0.5 or
  0: fixingDate, indexFixing, rate, amount, price on the curve and accrualPeriod; and the pricer's own members, read
  after the coupon has initialized it: swapletPrice, and capletPrice, capletRate, floorletPrice and floorletRate at 3%,
  which QuantLib's range accrual pricer refuses ("not implemented").
- "leg" cases: RangeAccrualLeg with every setting given, its coupons each holding one BGM pricer on interpolated
  smiles: per flow its kind ("RangeAccrualFloatersCoupon", or "FixedRateCoupon" for a zero gearing, which pays the
  spread), payment and accrual dates, rate and amount, and a range accrual's fixing date. QuantLib's leg holds n null
  cash flows before its n coupons (the S-cms plan's CMR5 (xii), tests/NanoQL.Tests/Divergences/RangeAccrualLegTests.cs),
  so its coupons are its second half, which the wheel's setCouponPricer can't reach past the nulls: each coupon's
  pricer is set on it. The default observation tenor ((xiii), no observation dates) isn't sampled. A leg QuantLib
  refuses records only "construction": {"throws": true}.

A smile is a spec of termstructures/volatility/flatsmilesection.py or interpolatedsmilesection.py, in its floating
form (the evaluation date its reference date). A case's inputs hold its settings, the index (a spec of
indexes/interestrateindex.py), the curve the price discounts on, and the coupon's or leg's arguments, a schedule as
cashflows/cashflows.py's. Read back by tests/NanoQL.Tests/Vectors/Coupons/RangeAccrualVectorTests.cs."""
import importlib.util
import math
import os
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


irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")
cf = _load("wheel_cashflows", "cashflows/cashflows.py")
fss = _load("wheel_flatsmilesection", "termstructures/volatility/flatsmilesection.py")
iss = _load("wheel_interpolatedsmilesection", "termstructures/volatility/interpolatedsmilesection.py")

TODAY = irx.TODAY
D = ql.Date
TARGET, A360, A365 = cf.TARGET, cf.A360, cf.A365
MF, F = ql.ModifiedFollowing, ql.Following
CURVE = irx.curve_spec(TODAY, 0.031)
E6M = irx.ibor_spec("Euribor", (6, ql.Months), 2, irx.TARGET, MF, True, irx.A360, CURVE)

# The interpolated smiles' strikes reach the infinite triggers, so no volatility is extrapolated.
STRIKES = [0.001, 0.0025, 0.005, 0.0075, 0.01, 0.015, 0.02, 0.025, 0.03, 0.035, 0.04, 0.05, 0.06, 0.08, 0.1, 0.15,
           0.2, 0.3, 0.5, 0.75, 1.0, 1.25]


def skew(level, slope):
    return [level + slope * (k - 0.03) ** 2 + 0.02 * max(0.03 - k, 0.0) / 0.03 for k in STRIKES]


def interpolated(when, vols):
    t = ql.Actual365Fixed().yearFraction(TODAY, when)
    return iss.section("LinearInterpolatedSmileSection", "floating", when, STRIKES,
                       [vol * math.sqrt(t) for vol in vols], 0.031, A365)


def smiles(kind, start, end):
    if kind == "flat":
        return fss.floating_spec(start, 0.20, A365), fss.floating_spec(end, 0.25, A365)
    return interpolated(start, skew(0.19, 0.6)), interpolated(end, skew(0.22, 0.4))


def make_smile(spec):
    if spec["type"] == "FlatSmileSection":
        return fss.make_section(spec)
    return iss.make_section(spec)


def observations(start, end, tenor, convention=MF):
    return cf.schedule_spec(start, end, ql.Period(*tenor), TARGET, convention, ql.DateGeneration.Forward, False)


ROUTES = [("no smile", False, False), ("call spread", True, True), ("smile correction", True, False)]

# (label, payment, nominal, start, end, fixing days, gearing, spread, observation tenor, lower, upper, correlation)
COUPONS = [
    ("6M daily, around the forward", D(15, ql.January, 2026), 1e6, D(15, ql.July, 2025), D(15, ql.January, 2026), 2,
     1.0, 0.0, (1, ql.Days), 0.02, 0.04, 1.0),
    ("6M weekly, narrow", D(17, ql.September, 2026), 1e6, D(17, ql.March, 2026), D(17, ql.September, 2026), 2, 1.0,
     0.0, (1, ql.Weeks), 0.03, 0.035, 0.5),
    ("6M monthly, above the forward", D(15, ql.July, 2027), 1e6, D(15, ql.January, 2027), D(15, ql.July, 2027), 2,
     1.0, 0.0, (1, ql.Months), 0.05, 0.08, 1.0),
    ("1Y weekly, infinite, geared", D(18, ql.June, 2026), 2.5e6, D(16, ql.June, 2025), D(16, ql.June, 2026), 2, 1.5,
     0.002, (1, ql.Weeks), 1e-9, 1.0, 0.0),
    ("3M daily, below the forward, 0 fixing days", D(19, ql.August, 2025), 1e6, D(19, ql.May, 2025),
     D(19, ql.August, 2025), 0, 1.0, 0.0, (1, ql.Days), 0.0, 0.025, 1.0),
    ("6M weekly, negative gearing", D(16, ql.November, 2026), 1e6, D(15, ql.May, 2026), D(16, ql.November, 2026), 1,
     -0.75, 0.04, (1, ql.Weeks), 0.025, 0.045, 0.5),
]


def coupon_cases():
    out = []
    k = 0
    for label, payment, nominal, start, end, days, gearing, spread, tenor, lower, upper, correlation in COUPONS:
        for smile in ("flat", "interpolated"):
            on_expiry, on_payment = smiles(smile, start, end)
            obs = observations(start, end, tenor)
            for route, with_smile, by_call_spread in ROUTES:
                with v.case_settings(TODAY) as st:
                    index = irx.make_index(E6M)
                    pricer = ql.RangeAccrualPricerByBgm(correlation, make_smile(on_expiry), make_smile(on_payment),
                                                        with_smile, by_call_spread)
                    c = ql.RangeAccrualFloatersCoupon(payment, nominal, index, start, end, days, ql.Actual360(),
                                                      gearing, spread, start, end, cf.schedule(obs), lower, upper)
                    c.setPricer(pricer)
                    out.append({"name": f"coupon {label}, {smile} smiles, {route} / {k}",
                                "inputs": {"settings": st, "kind": "coupon", "index": E6M, "discountCurve": CURVE,
                                           "paymentDate": v.enc(payment), "nominal": nominal,
                                           "startDate": v.enc(start), "endDate": v.enc(end), "fixingDays": days,
                                           "dayCounter": A360, "gearing": gearing, "spread": spread,
                                           "observationSchedule": obs, "lowerTrigger": lower,
                                           "upperTrigger": upper,
                                           "pricer": {"correlation": correlation, "smilesOnExpiry": on_expiry,
                                                      "smilesOnPayment": on_payment, "withSmile": with_smile,
                                                      "byCallSpread": by_call_spread}},
                                "expected": {
                                    "fixingDate": v.enc(c.fixingDate()), "indexFixing": v.attempt(c.indexFixing),
                                    "rate": v.attempt(c.rate), "amount": v.attempt(c.amount),
                                    "price": v.attempt(lambda: c.price(irx.handle(CURVE))),
                                    "accrualPeriod": c.accrualPeriod(),
                                    "swapletPrice": v.attempt(pricer.swapletPrice),
                                    "capletPrice": v.attempt(lambda: pricer.capletPrice(0.03)),
                                    "capletRate": v.attempt(lambda: pricer.capletRate(0.03)),
                                    "floorletPrice": v.attempt(lambda: pricer.floorletPrice(0.03)),
                                    "floorletRate": v.attempt(lambda: pricer.floorletRate(0.03))}})
                k += 1
    return out


def leg_spec(schedule, notionals, dc, adjustment, fixing_days, gearings, spreads, lowers, uppers, tenor, convention):
    return {"schedule": schedule, "notionals": list(notionals), "paymentDayCounter": dc,
            "paymentAdjustment": int(adjustment), "fixingDays": list(fixing_days), "gearings": list(gearings),
            "spreads": list(spreads), "lowerTriggers": list(lowers), "upperTriggers": list(uppers),
            "observationTenor": [tenor[0], int(tenor[1])], "observationConvention": int(convention)}


SEMIANNUAL = cf.schedule_spec(D(15, ql.July, 2025), D(15, ql.July, 2027), ql.Period(6, ql.Months), TARGET, MF,
                              ql.DateGeneration.Forward, False)
STUB = cf.schedule_spec(D(20, ql.March, 2025), D(15, ql.June, 2026), ql.Period(6, ql.Months), TARGET, MF,
                        ql.DateGeneration.Backward, False)
LEG_SMILES = (interpolated(D(15, ql.July, 2026), skew(0.19, 0.6)), interpolated(D(15, ql.January, 2027),
                                                                                  skew(0.22, 0.4)))

# (label, spec, correlation, with smile, by call spread)
LEGS = [
    ("semiannual, weekly", leg_spec(SEMIANNUAL, [1e6], A360, MF, [2], [1.0], [0.0], [0.02], [0.04], (1, ql.Weeks), MF),
     1.0, True, True),
    ("per-coupon terms, monthly",
     leg_spec(SEMIANNUAL, [1e6, 8e5], A365, F, [2, 1], [1.0, 1.2], [0.0, 0.001], [0.025, 0.02, 0.015],
              [0.04, 0.045], (1, ql.Months), F), 0.5, True, False),
    ("a zero gearing is fixed",
     leg_spec(SEMIANNUAL, [1e6], A360, MF, [2], [1.0, 0.0, 1.0], [0.0, 0.01], [0.02], [0.04], (2, ql.Weeks), MF),
     1.0, False, False),
    ("front stub, daily", leg_spec(STUB, [2e6], A360, MF, [2], [1.0], [0.0], [0.01], [0.05], (1, ql.Days), MF),
     1.0, True, True),
    ("too many notionals",
     leg_spec(SEMIANNUAL, [1e6] * 5, A360, MF, [2], [1.0], [0.0], [0.02], [0.04], (1, ql.Weeks), MF), 1.0, True, True),
    ("too many upper triggers",
     leg_spec(SEMIANNUAL, [1e6], A360, MF, [2], [1.0], [0.0], [0.02], [0.04] * 6, (1, ql.Weeks), MF), 1.0, True, True),
]


def make_leg(spec, index):
    return ql.RangeAccrualLeg(spec["notionals"], cf.schedule(spec["schedule"]), index,
                              cf.day_counter(spec["paymentDayCounter"]), spec["paymentAdjustment"], spec["fixingDays"],
                              spec["gearings"], spec["spreads"], spec["lowerTriggers"], spec["upperTriggers"],
                              ql.Period(spec["observationTenor"][0], spec["observationTenor"][1]),
                              spec["observationConvention"])


def leg_cases():
    out = []
    for k, (label, spec, correlation, with_smile, by_call_spread) in enumerate(LEGS):
        with v.case_settings(TODAY) as st:
            index = irx.make_index(E6M)
            pricer_spec = {"correlation": correlation, "smilesOnExpiry": LEG_SMILES[0],
                           "smilesOnPayment": LEG_SMILES[1], "withSmile": with_smile, "byCallSpread": by_call_spread}
            inputs = {"settings": st, "kind": "leg", "index": E6M, "leg": spec, "pricer": pricer_spec}
            try:
                leg = make_leg(spec, index)
            except RuntimeError:
                out.append({"name": f"leg {label} / {k}", "inputs": inputs,
                            "expected": {"construction": dict(v.THROWS)}})
                continue
            n = len(leg) // 2
            flows = list(leg)[n:]
            pricer = ql.RangeAccrualPricerByBgm(correlation, make_smile(LEG_SMILES[0]), make_smile(LEG_SMILES[1]),
                                                with_smile, by_call_spread)
            floating = [ql.as_floating_rate_coupon(f) for f in flows]
            for fc in floating:
                if fc is not None:
                    fc.setPricer(pricer)
            coupons = [ql.as_coupon(f) for f in flows]
            out.append({"name": f"leg {label} / {k}", "inputs": inputs,
                        "expected": {
                            "kinds": ["RangeAccrualFloatersCoupon" if fc is not None else "FixedRateCoupon"
                                      for fc in floating],
                            "dates": v.enc([f.date() for f in flows]),
                            "accrualStartDates": v.enc([c.accrualStartDate() for c in coupons]),
                            "accrualEndDates": v.enc([c.accrualEndDate() for c in coupons]),
                            "fixingDates": [v.enc(fc.fixingDate()) if fc is not None else None for fc in floating],
                            "rates": [v.attempt(c.rate) for c in coupons],
                            "amounts": [v.attempt(f.amount) for f in flows]}})
    return out


def make_cases():
    return coupon_cases() + leg_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/rangeaccrual.hpp", make_cases)
