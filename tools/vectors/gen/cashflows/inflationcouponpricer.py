# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year inflation-coupon pricers (ql/cashflows/inflationcouponpricer.hpp): the Black,
unit-displaced Black and Bachelier pricers' swaplet, caplet and floorlet rates, reached through the wheel's
CappedFlooredYoYInflationCoupon, whose rate is the pricer's swaplet rate plus its floorlet rate less its caplet rate,
each at the coupon's effective strike, (strike - spread) / gearing.

- "pricer" cases, one per pricer, volatility and coupon: the coupon capped, floored and collared, each with the
  pricer set: underlyingRate (the swaplet rate), effectiveCap, effectiveFloor, cappedRate, flooredRate, collaredRate
  and collaredAmount. The coupons fix before the volatility's base date (the optionlets' intrinsic values on the
  fixing) or after it, on ratio and quoted indexes, with gearings and spreads.

A case's inputs hold its settings, the index spec and fixings (tools/vectors/gen/cashflows/indexedcashflow.py's), the
pricer's kind ("Black", "UnitDisplacedBlack" or "Bachelier"), its volatility (a ConstantYoYOptionletVolatility spec,
tools/vectors/gen/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.py's), its nominal curve
and the coupon's arguments with its cap and floor. Read back by
tests/NanoQL.Tests/Vectors/Coupons/InflationCouponPricerVectorTests.cs."""
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


icf = _load("wheel_indexedcashflow", "cashflows", "indexedcashflow.py")
vol = _load("wheel_yoyvol", "termstructures", "volatility", "inflation", "yoyinflationoptionletvolatilitystructure.py")
cf = icf.cf
ii = icf.ii

TODAY = icf.TODAY
UK = cf.UK_SETTLEMENT
A365 = cf.A365
M = int(ql.Months)
INDEXES = {"ratio": (icf.YYUKRPI_RATIO, icf.UK_FIXINGS), "quoted": (ii.preset("YYUKRPI", icf.YOY_UK), ii.YY_FIXINGS)}
PRICERS = {"Black": ql.BlackYoYInflationCouponPricer,
           "UnitDisplacedBlack": ql.UnitDisplacedBlackYoYInflationCouponPricer,
           "Bachelier": ql.BachelierYoYInflationCouponPricer}
VOLS = {
    "Black": [vol.surface_spec(0.15, 0, UK, ql.ModifiedFollowing, A365, [2, M], ql.Monthly, False)],
    "UnitDisplacedBlack": [vol.surface_spec(0.01, 0, UK, ql.ModifiedFollowing, A365, [2, M], ql.Monthly, False)],
    "Bachelier": [vol.surface_spec(0.006, 0, UK, ql.ModifiedFollowing, A365, [3, M], ql.Monthly, False),
                  vol.surface_spec(0.012, 2, UK, ql.Following, cf.THIRTY360_BB, [2, M], ql.Monthly, True)],
}

# (label, index, payment, nominal, start, end, fixing days, lag months, interpolation, gearing, spread, cap, floor)
COUPONS = [
    ("forecast, at the money", "ratio", ql.Date(17, ql.June, 2025), 1e6, ql.Date(15, ql.June, 2024),
     ql.Date(15, ql.June, 2025), 0, 2, ql.CPI.Flat, 1.0, 0.0, 0.031, 0.029),
    ("forecast, geared, spread", "ratio", ql.Date(16, ql.March, 2026), 1e6, ql.Date(15, ql.March, 2025),
     ql.Date(15, ql.March, 2026), 0, 2, ql.CPI.Linear, 1.4, 0.003, 0.05, 0.01),
    ("forecast, far", "quoted", ql.Date(15, ql.June, 2029), 2e6, ql.Date(15, ql.June, 2028),
     ql.Date(15, ql.June, 2029), 2, 2, ql.CPI.Flat, 1.0, -0.002, 0.032, 0.02),
    ("fixed, in the money", "ratio", ql.Date(3, ql.May, 2024), 1e6, ql.Date(1, ql.May, 2023),
     ql.Date(1, ql.May, 2024), 0, 2, ql.CPI.Flat, 1.0, 0.0, 0.01, 0.005),
    ("fixed, quoted", "quoted", ql.Date(15, ql.May, 2024), 1e6, ql.Date(15, ql.May, 2023),
     ql.Date(15, ql.May, 2024), 0, 2, ql.CPI.Flat, 1.2, 0.001, 0.03, 0.025),
]


def pricer(kind, vol_spec):
    surface = ql.YoYOptionletVolatilitySurfaceHandle(vol.make_surface(vol_spec))
    return PRICERS[kind](surface, icf.nominal_curve(icf.NOMINAL))


def capped(index, payment, nominal, start, end, days, lag, interp, gearing, spread, cap, floor):
    return ql.CappedFlooredYoYInflationCoupon(payment, nominal, start, end, days, index, ql.Period(lag, ql.Months),
                                              interp, ql.Actual365Fixed(), gearing, spread,
                                              cap if cap is not None else ql.nullDouble(),
                                              floor if floor is not None else ql.nullDouble())


def pricer_cases():
    out = []
    k = 0
    for kind, vol_specs in VOLS.items():
        for vol_spec in vol_specs:
            for label, which, payment, nominal, start, end, days, lag, interp, gearing, spread, cap, floor in COUPONS:
                spec, fixings = INDEXES[which]
                with v.case_settings(TODAY) as st:
                    index = icf.index_with_fixings(spec, fixings)
                    args = (index, payment, nominal, start, end, days, lag, interp, gearing, spread)
                    coupons = {"capped": capped(*args, cap, None), "floored": capped(*args, None, floor),
                               "collared": capped(*args, cap, floor)}
                    p = pricer(kind, vol_spec)
                    for c in coupons.values():
                        ql.setCouponPricer([c], p)
                    collar = coupons["collared"]
                    out.append({"name": f"pricer {kind} {label} / {k}",
                                "inputs": {"settings": st, "kind": "pricer", "index": spec, "fixings": fixings,
                                           "pricer": kind, "volatility": vol_spec, "nominalCurve": icf.NOMINAL,
                                           "paymentDate": v.enc(payment), "nominal": nominal,
                                           "startDate": v.enc(start), "endDate": v.enc(end), "fixingDays": days,
                                           "observationLag": [lag, M], "interpolation": int(interp),
                                           "dayCounter": A365, "gearing": gearing, "spread": spread, "cap": cap,
                                           "floor": floor},
                                "expected": {
                                    "underlyingRate": v.attempt(collar.underlyingRate),
                                    "effectiveCap": v.attempt(collar.effectiveCap),
                                    "effectiveFloor": v.attempt(collar.effectiveFloor),
                                    "cappedRate": v.attempt(coupons["capped"].rate),
                                    "flooredRate": v.attempt(coupons["floored"].rate),
                                    "collaredRate": v.attempt(collar.rate),
                                    "collaredAmount": v.attempt(collar.amount)}})
                k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/inflationcouponpricer.hpp", pricer_cases)
