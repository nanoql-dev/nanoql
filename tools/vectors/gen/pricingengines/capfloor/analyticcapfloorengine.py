# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticCapFloorEngine (ql/pricingengines/capfloor/analyticcapfloorengine.hpp, .cpp): 24
instruments of tools/vectors/gen/instruments/capfloor.py, whose functions build them, each priced on four affine
models, and the first 14 again with no fixing days on the HullWhite alone, on the evaluation date 15 January 2025.

The instruments cover caps, floors and collars at, in and out of the money, a collar whose cap is below its floor,
gearings and spreads, amortizing notionals, legs with past fixings (their past caplets skipped, those fixed on or
before the reference date paying their known amounts, discounted on the model), a caplet fixing today with and
without a stored fixing, an instrument paid in full (its results unset), an empty leg, negative rates, a discount
curve dated at the spot date (a caplet paid between the evaluation date and the curve's reference date skipped), and
the refusals of a cap with no cap rates and of a fixed-rate leg. The 14 instruments from the spot date are priced again
on the HullWhite with their legs' fixing days 0 (", fixed on its start"), each caplet fixed on its start, where
QuantLib's option and nanoql's are the same.

Each case's output is the NPV on each model, an amount compared at the leg's largest notional: a HullWhite (a = 0.05,
sigma = 0.01) on the case's discount curve, its own; a Vasicek (r0 = 0.03, a = 0.08, b = 0.035, sigma = 0.009) with the
discount curve given to the engine, whose reference date and day counter the times read; a CoxIngersollRoss (r0 = 0.03,
theta = 0.035, k = 0.3, sigma = 0.05) likewise; and an ExtendedCoxIngersollRoss (theta = 0.035, k = 0.3, sigma = 0.05,
x0 = 0.03) on the discount curve. A refused pricing is {"throws": true}. A Cox-Ingersoll-Ross engine whose bond option
on one of the instrument's caplets reaches a non-central chi-squared series outside the range of doubles, where
QuantLib's series runs on subnormals and zeros and nanoql's distribution refuses
(tests/NanoQL.Tests/Divergences/ChiSquaredSeriesRefusalTests.cs), is generated and left out of the case's engines, by
models/shortrate/onefactormodels/coxingersollross.py's and extendedcoxingersollross.py's tests on the caplet's option (a
caplet just after its fixing, of short maturity, on the seasoned instruments). The HullWhite's price of an instrument
with a caplet still to be fixed before its start, which QuantLib's option expires at its start and nanoql's at its
fixing (tests/NanoQL.Tests/Divergences/CapletFixingExpiryTests.cs), is generated and left out of the case's engines
likewise. Each case's inputs hold the instrument, the discount curve and the engines ({"model", and the model's
parameters}). The wheel's AnalyticCapFloorEngine is the proxy of the C++ constructor on a one-factor affine model and an
optional term structure (QuantLib-SWIG 1.44, SWIG/capfloor.i). Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticCapFloorEngineVectorTests.cs."""
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
ecir = _load("wheel_extendedcoxingersollross", "models/shortrate/onefactormodels/extendedcoxingersollross.py")
cir = ecir.cir
irx = cfl.irx
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)

TODAY = cfl.TODAY

ENGINES = [
    {"model": "HullWhite", "a": 0.05, "sigma": 0.01},
    {"model": "Vasicek", "r0": 0.03, "a": 0.08, "b": 0.035, "sigma": 0.009, "lambda": 0.0},
    {"model": "CoxIngersollRoss", "r0": 0.03, "theta": 0.035, "k": 0.3, "sigma": 0.05},
    {"model": "ExtendedCoxIngersollRoss", "theta": 0.035, "k": 0.3, "sigma": 0.05, "x0": 0.03},
]

INSTRUMENTS = [
    "1Y cap at 3%", "3Y cap at 3%", "3Y floor at 3%", "3Y collar 3.5% / 2.5%", "10Y cap at 3%", "5Y cap in the money",
    "5Y cap far out of the money", "5Y floor far out of the money", "7Y quarterly cap on 3M", "4Y cap, rates extended",
    "3Y collar, cap below floor", "3Y cap, gearing 2, spread 1%", "3Y floor, gearing 0.5, spread 0.2%",
    "5Y cap, amortizing", "seasoned cap, past fixings", "seasoned floor, past fixings", "seasoned collar, past fixings",
    "cap fixing today, none stored", "cap fixing today, stored", "cap paid in full", "cap on an empty leg",
    "3Y cap on a spot-dated Actual/360 curve", "refused: a cap with no cap rates",
    "refused at pricing: a fixed-rate leg",
]


def engine_of(curve_spec, e):
    curve = irx.handle(curve_spec)
    if e["model"] == "HullWhite":
        return ql.AnalyticCapFloorEngine(ql.HullWhite(curve, e["a"], e["sigma"]))
    if e["model"] == "Vasicek":
        return ql.AnalyticCapFloorEngine(ql.Vasicek(e["r0"], e["a"], e["b"], e["sigma"], e["lambda"]), curve)
    if e["model"] == "CoxIngersollRoss":
        return ql.AnalyticCapFloorEngine(ql.CoxIngersollRoss(e["r0"], e["theta"], e["k"], e["sigma"]), curve)
    return ql.AnalyticCapFloorEngine(ql.ExtendedCoxIngersollRoss(curve, e["theta"], e["k"], e["sigma"], e["x0"]))


def caplet_options(c, curve_spec):
    """The bond options analyticcapfloorengine.cpp prices for an instrument's caplets still to be fixed, (type, strike,
    maturity, bond maturity), on the curve's reference date and day counter."""
    curve = irx.handle(curve_spec)
    reference, dc = curve.referenceDate(), curve.dayCounter()
    cap_rates, floor_rates = list(c.capRates()), list(c.floorRates())
    options = []
    for i, cf in enumerate(c.floatingLeg()):
        coupon = ql.as_floating_rate_coupon(cf)
        if coupon is None:
            # a fixed-rate leg, which the engine refuses
            continue
        payment = dc.yearFraction(reference, coupon.date())
        fixing = dc.yearFraction(reference, coupon.fixingDate())
        if payment < 0.0 or fixing <= 0.0:
            continue
        start = dc.yearFraction(reference, coupon.accrualStartDate())
        tau = coupon.accrualPeriod()
        for rates, kind in ((cap_rates, PUT), (floor_rates, CALL)):
            if rates:
                options.append((kind, 1.0 / (1.0 + rates[min(i, len(rates) - 1)] * tau), start, payment))
    return options


def fixed_before_start(c, curve_spec):
    """Whether a caplet still to be fixed, not yet paid, is fixed before its start, on the curve's reference date and day
    counter: the HullWhite's option then expires at its fixing in nanoql."""
    curve = irx.handle(curve_spec)
    reference, dc = curve.referenceDate(), curve.dayCounter()
    for cf in c.floatingLeg():
        coupon = ql.as_floating_rate_coupon(cf)
        if coupon is None:
            continue
        fixing = dc.yearFraction(reference, coupon.fixingDate())
        if dc.yearFraction(reference, coupon.date()) >= 0.0 and 0.0 < fixing < dc.yearFraction(
                reference, coupon.accrualStartDate()):
            return True
    return False


def refused(e, c, curve_spec):
    """Whether a Cox-Ingersoll-Ross engine's option on one of the caplets reaches a refused chi-squared series."""
    if e["model"] == "CoxIngersollRoss":
        spec = {k: e[k] for k in ("r0", "theta", "k", "sigma")}
        return any(cir.option_refused(spec, o) for o in caplet_options(c, curve_spec))
    if e["model"] == "ExtendedCoxIngersollRoss":
        m = ql.ExtendedCoxIngersollRoss(irx.handle(curve_spec), e["theta"], e["k"], e["sigma"], e["x0"])
        return any(ecir.option_refused(m, e, o) for o in caplet_options(c, curve_spec))
    return False


def left_out(e, c, curve_spec):
    return refused(e, c, curve_spec) or (e["model"] == "HullWhite" and fixed_before_start(c, curve_spec))


def on_start(spec):
    """The instrument with its leg's fixing days 0, each caplet fixed on its start."""
    return dict(spec, leg=dict(spec["leg"], fixingDays=[0]))


def rows():
    """(name, spec, curve spec, settings, engines) for the 24 instruments and the 14 fixed on their starts."""
    by_name = {row[0]: row for row in cfl.instruments()}
    out = [(name, by_name[name][1], by_name[name][2], by_name[name][3], ENGINES) for name in INSTRUMENTS]
    for name in INSTRUMENTS[:14]:
        _, spec, curve_spec, settings = by_name[name]
        assert not settings and spec["leg"]["fixingDays"] == [2], name
        out.append((f"{name}, fixed on its start", on_start(spec), curve_spec, settings, ENGINES[:1]))
    return out


def cases():
    out = []
    for name, spec, curve_spec, settings, all_engines in rows():
        with v.case_settings(TODAY, **cfl.settings_of(spec, settings)) as st:
            try:
                engines = [e for e in all_engines if not left_out(e, cfl.make_instrument(spec), curve_spec)]
            except RuntimeError:
                engines = all_engines
            npvs = []
            for e in engines:
                try:
                    c = cfl.make_instrument(spec)
                except RuntimeError:
                    npvs.append(dict(v.THROWS))
                    continue
                c.setPricingEngine(engine_of(curve_spec, e))
                npvs.append(cfl.attempt_scaled(c.NPV, cfl.scale_of(spec)))
            out.append({"name": name,
                        "inputs": {"settings": st, "capFloor": spec, "discountCurve": curve_spec, "engines": engines},
                        "expected": {"NPV": npvs}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/capfloor/analyticcapfloorengine.hpp", cases)
