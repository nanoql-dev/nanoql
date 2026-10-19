# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackCapFloorEngine (ql/pricingengines/capfloor/blackcapfloorengine.hpp): 26 instruments of
tools/vectors/gen/instruments/capfloor.py, whose functions build them, each priced by the wheel's engine in two to six
forms, on the evaluation date 15 January 2025.

The engines are the constructor on a volatility quote (a volatility, a day counter and a displacement: 20%, 35% on
Actual/360 and 12% on 30/360) and the constructor on an optionlet volatility structure, a ConstantOptionletVolatility
with a fixed reference date (a reference date other than the evaluation date among them, which moves the engine's
"today"), its displacement null (the structure's) or given (equal to the structure's, or not, which QuantLib refuses),
or a normal structure, which QuantLib refuses ("BlackCapFloorEngine should only be used for vol surfaces stripped with
shifted log normal model"). The instruments cover caps, floors and collars at, in and out of the money, a collar whose
cap is below its floor, gearings and spreads, amortizing notionals, indexed coupons, legs with past fixings (their past
caplets skipped, or priced at their intrinsic value when fixed on or before the volatility's reference date), a caplet
fixing today with and without a stored fixing, an expired instrument (its results unset), an empty leg, negative rates
(refused without a displacement, priced with one) and a discount curve dated at the spot date.

Each case's outputs are, per engine ([engine][output] for the per-optionlet results), the NPV, the vega and the
optionlets' prices, vegas, deltas, discount factors, standard deviations (which QuantLib doesn't set for a collar) and
ATM forwards (null for a coupon paid before the evaluation date, QuantLib's Null<Rate>()); a refused pricing, or a
result the engine didn't set, is {"throws": true}. The NPV, the vega and the optionlets' prices and vegas are amounts,
compared at the leg's largest notional. Each case's inputs hold the instrument, the discount curve and the engines:
{"form": "quote", "vol", "dayCounter", "displacement"} or {"form": "structure", "volatility", "displacement"}, the
volatility a spec of cashflows/iborcoupon.py's vol_spec and a null displacement QuantLib's Null<Real>(). The wheel's
BlackCapFloorEngine is the proxy of the C++ constructors on a quote handle and on a structure handle (QuantLib-SWIG 1.44,
SWIG/capfloor.i); its constructor on a Volatility, which the quote form equals, is the harness's (cxx/pricingengines/
capfloor/blackcapfloorengine). The functions here build the cases the Bachelier engine's generator shares. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/BlackCapFloorEngineVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_capfloor", os.path.join(v.GEN_ROOT, "instruments", "capfloor.py"))
cfl = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cfl)
ibc, irx = cfl.ibc, cfl.irx

TODAY = cfl.TODAY
A360, A365, THIRTY360_BB = cfl.A360, cfl.A365, cfl.THIRTY360_BB
SL, N = cfl.SL, cfl.N

RESULTS = ["vega", "optionletsPrice", "optionletsVega", "optionletsDelta", "optionletsDiscountFactor",
           "optionletsStdDev", "optionletsAtmForward"]
AMOUNTS = {"NPV", "vega", "optionletsPrice", "optionletsVega"}

LATER = ql.Date(20, ql.March, 2025)


def quote(vol, dc=A365, displacement=0.0):
    return {"form": "quote", "vol": vol, "dayCounter": dc, "displacement": displacement}


def bachelier_quote(vol):
    return {"form": "quote", "vol": vol}


def structure(vol, vtype=SL, displacement=0.0, given=None, reference=TODAY, dc=A365):
    return {"form": "structure", "volatility": ibc.vol_spec(vol, vtype, displacement, reference, dc),
            "displacement": given}


def black_engine(curve_spec, spec):
    if spec["form"] == "quote":
        return cfl.black_engine(curve_spec, spec)
    vol = ibc.volatility(spec["volatility"])
    if spec["displacement"] is None:
        return ql.BlackCapFloorEngine(irx.handle(curve_spec), vol)
    return ql.BlackCapFloorEngine(irx.handle(curve_spec), vol, spec["displacement"])


def bachelier_engine(curve_spec, spec):
    if spec["form"] == "quote":
        return cfl.bachelier_engine(curve_spec, spec)
    return ql.BachelierCapFloorEngine(irx.handle(curve_spec), ibc.volatility(spec["volatility"]))


def black_engines(name):
    """The Black engines an instrument is priced with."""
    if "negative rates" in name:
        return [quote(0.2), quote(0.2, A365, 0.03), structure(0.15, SL, 0.02), structure(0.15, SL, 0.02, 0.02)]
    out = [quote(0.2), quote(0.35, A360)]
    if name in ("3Y cap at 3%", "3Y collar 3.5% / 2.5%", "seasoned cap, past fixings", "cap fixing today, stored"):
        out += [structure(0.25), structure(0.25, SL, 0.0, None, LATER), structure(0.22, SL, 0.01, 0.01),
                structure(0.22, SL, 0.01, 0.0)]
    if name in ("3Y floor at 3%", "10Y cap at 3%"):
        out += [quote(0.12, THIRTY360_BB), structure(0.01, N)]
    return out


INSTRUMENTS = [
    "1Y cap at 3%", "3Y cap at 3%", "3Y floor at 3%", "3Y collar 3.5% / 2.5%", "10Y cap at 3%", "5Y cap in the money",
    "5Y cap far out of the money", "5Y floor far out of the money", "7Y quarterly cap on 3M", "4Y cap, rates extended",
    "2Y floor, more rates than coupons", "3Y collar, cap below floor", "3Y cap, gearing 2, spread 1%",
    "3Y floor, gearing 0.5, spread 0.2%", "5Y cap, amortizing", "3Y cap, indexed coupons by the switch",
    "seasoned cap, past fixings", "seasoned floor, past fixings", "seasoned collar, past fixings",
    "cap fixing today, none stored", "cap fixing today, stored", "cap paid in full", "cap on an empty leg",
    "4Y cap at -0.2% on negative rates", "3Y cap on a spot-dated Actual/360 curve",
    "refused at pricing: a fixed-rate leg",
]


def priced(instrument, scale):
    """The NPV and the results, with the engine set; a refused pricing is each output's throw."""
    out = {"NPV": cfl.attempt_scaled(instrument.NPV, scale)}
    for name in RESULTS:
        f = getattr(instrument, name)
        if name in AMOUNTS:
            if name == "vega":
                out[name] = cfl.attempt_scaled(f, scale)
            else:
                try:
                    out[name] = [v.amount(x, scale) for x in f()]
                except RuntimeError:
                    out[name] = dict(v.THROWS)
        else:
            out[name] = v.attempt(lambda f=f: list(f()))
    return out


def cases_for(instruments, engines_of, engine_of, label):
    rows = {row[0]: row for row in cfl.instruments()}
    out = []
    for name in instruments:
        _, spec, curve_spec, settings = rows[name]
        engines = engines_of(name)
        with v.case_settings(TODAY, **cfl.settings_of(spec, settings)) as st:
            c = cfl.make_instrument(spec)
            scale = cfl.scale_of(spec)
            per_engine = []
            for e in engines:
                try:
                    c.setPricingEngine(engine_of(curve_spec, e))
                except RuntimeError:
                    per_engine.append({k: dict(v.THROWS) for k in ["NPV"] + RESULTS})
                    continue
                per_engine.append(priced(c, scale))
            expected = {k: [p[k] for p in per_engine] for k in ["NPV"] + RESULTS}
            out.append({"name": f"{label}: {name}",
                        "inputs": {"settings": st, "capFloor": spec, "discountCurve": curve_spec, "engines": engines},
                        "expected": expected})
    return out


def cases():
    return cases_for(INSTRUMENTS, black_engines, black_engine, "Black")


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/capfloor/blackcapfloorengine.hpp", cases)
