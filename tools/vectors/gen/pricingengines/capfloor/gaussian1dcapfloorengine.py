# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Gaussian1dCapFloorEngine (ql/pricingengines/capfloor/gaussian1dcapfloorengine.hpp, .cpp):
caps, floors and collars on Euribor (instruments/capfloor.py's legs, forecast on a flat 3.1%), on the evaluation date 15
January 2025, each priced on pricingengines/swaption/gaussian1dswaptionengine.py's three GSR models (on the flat 2.7%
curve), with that generator's first two engine settings on the first model (32 points over 5 standard deviations; no
payoff extrapolation). The legs start three months after the spot date with two fixing days, so every caplet fixes after
the evaluation date: caps, floors and collars at, in and out of the money, a collar whose cap is below its floor, a
quarterly cap on Euribor 3M, cap rates extended over the leg, amortizing notionals on Euribor 1Y (forecast on a flat
3.4% of the model's clock), a 10-year cap; each case's outputs are its NPV on each model and setting, and on the first
model the optionlets' prices; amounts at the leg's largest notional.

Left out, each a documented divergence (the S-g1d plan's G1R6): a discount curve given to the engine, whose strike leg
QuantLib discounts on the model curve ((ii), Gaussian1dCapFloorDiscountTests); the optionlets' at-the-money forwards,
which QuantLib leaves 0 ((iii), Gaussian1dCapFloorResultsTests); caplets fixed before the evaluation date, which
QuantLib leaves undiscounted ((iv), Gaussian1dCapFloorFixedCapletTests); gearings and spreads, which the engine refuses;
CMS and overnight coupons ((xix), (xx)); flat payoff extrapolation ((xxi)); and curves on another clock ((xvi)). The
wheel's Gaussian1dCapFloorEngine(model, integrationPoints, stddevs, extrapolatePayoff, flatPayoffExtrapolation,
discountCurve) is the C++ constructor's proxy. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Gaussian1dCapFloorEngineVectorTests.cs."""
import importlib.util
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


cfl = _load("wheel_capfloor", "instruments/capfloor.py")
g1d = _load("wheel_gaussian1dswaptionengine", "pricingengines/swaption/gaussian1dswaptionengine.py")
TODAY = cfl.TODAY
M, Y = ql.Months, ql.Years
CAP, FLOOR, COLLAR = cfl.CAP, cfl.FLOOR, cfl.COLLAR
FORWARD = cfl.CAL.advance(cfl.SPOT, 3, M)
MODELS = g1d.MODELS
SETTINGS = g1d.SETTINGS[:2]
# Euribor 1Y forecast on a curve of the model's clock, not instruments/capfloor.py's Actual/360 one (G1R6 (xvi))
E1Y = cfl.ibc.euribor((1, Y), cfl.irx.curve_spec(TODAY, 0.034))


def forward_leg(length, index=cfl.E6M, tenor=ql.Period(6, M), notionals=(1e6,)):
    return cfl.ibor_leg(FORWARD, ql.Period(length, Y), index, tenor, notionals)


def instruments():
    spec = cfl.instrument_spec
    return [
        ("1Y cap at 3%", spec(CAP, forward_leg(1), [0.03])),
        ("3Y cap at 3%", spec(CAP, forward_leg(3), [0.03])),
        ("3Y floor at 3%", spec(FLOOR, forward_leg(3), (), [0.03])),
        ("3Y collar 3.5% / 2.5%", spec(COLLAR, forward_leg(3), [0.035], [0.025])),
        ("5Y cap in the money", spec(CAP, forward_leg(5), [0.015])),
        ("5Y cap far out of the money", spec(CAP, forward_leg(5), [0.09])),
        ("5Y floor far out of the money", spec(FLOOR, forward_leg(5), (), [0.004])),
        ("7Y quarterly cap on 3M", spec(CAP, forward_leg(7, cfl.E3M, ql.Period(3, M), (2.5e6,)), [0.032])),
        ("4Y cap, rates extended", spec(CAP, forward_leg(4), [0.025, 0.03])),
        ("3Y collar, cap below floor", spec(COLLAR, forward_leg(3), [0.02], [0.03])),
        ("5Y cap, amortizing", spec(CAP, forward_leg(5, E1Y, ql.Period(1, Y), (5e6, 4e6, 3e6, 2e6, 1e6)), [0.035])),
        ("10Y cap at 3%", spec(CAP, forward_leg(10), [0.03])),
    ]


def engine(model, settings=None):
    s = settings or {"integrationPoints": 64, "stddevs": 7.0, "extrapolatePayoff": True}
    return ql.Gaussian1dCapFloorEngine(model, s["integrationPoints"], s["stddevs"], s["extrapolatePayoff"], False)


def case(name, spec):
    with v.case_settings(TODAY) as st:
        scale = cfl.scale_of(spec)
        cap = cfl.make_instrument(spec)
        expected = {"gsr": [], "settings": []}
        for m in MODELS:
            cap.setPricingEngine(engine(g1d.make_gsr(m)))
            expected["gsr"].append(v.amount(v.result(cap.NPV), scale))
        model = g1d.make_gsr(MODELS[0])
        for s in SETTINGS:
            cap.setPricingEngine(engine(model, s))
            expected["settings"].append(v.amount(v.result(cap.NPV), scale))
        cap.setPricingEngine(engine(model))
        cap.NPV()
        expected["optionletsPrice"] = [v.amount(x, scale) for x in cap.optionletsPrice()]
        return {"name": name, "inputs": {"settings": st, "capFloor": spec, "models": MODELS, "engineSettings": SETTINGS},
                "expected": expected}


def cases():
    return [case(name, spec) for name, spec in instruments()]


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/capfloor/gaussian1dcapfloorengine.hpp", cases)
