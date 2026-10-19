# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackCapFloorEngine and BachelierCapFloorEngine (ql/pricingengines/capfloor/
blackcapfloorengine.hpp, bacheliercapfloorengine.hpp) in the forms the wheel doesn't bind or doesn't fully bind, each
output computed by the C++ harness, on the evaluation date 15 January 2025: 8 instruments of
tools/vectors/gen/instruments/capfloor.py, each priced by both engines on a Volatility and on an optionlet volatility
structure.

The engines on a Volatility are QuantLib's constructors taking the volatility as a value, which the wheel's quote
constructors equal, the Black one with a day counter and a displacement and the Bachelier one with a day counter, which
the wheel's lacks (Actual/365 (Fixed), Actual/360, 30/360). The engines on a structure are QuantLib's constructors on a
ConstantOptionletVolatility (tools/vectors/gen/termstructures/volatility/optionlet/constantoptionletvol.py's specs, a
fixed reference date or settlement days), with their checks: the Black engine refuses a normal structure and a
displacement other than the structure's, and takes the structure's for a null one (QuantLib's Null<Real>()); the
Bachelier engine refuses a shifted lognormal structure.

Each case's outputs are, per engine ([engine][tag]), the NPV and the additional results (vega, optionletsPrice,
optionletsVega, optionletsDelta, optionletsDiscountFactor, optionletsAtmForward, optionletsStdDev), each a value or a
list over the optionlets, the ATM forward of a coupon paid before the evaluation date null; a refused pricing, or a
result the engine didn't set (a collar's optionletsStdDev), is {"throws": true}. The NPV, the vega and the optionlets'
prices and vegas are amounts, compared at the leg's largest notional. Each case's inputs hold the instrument
("capFloor"), the discount curve, the engines ({"type", "form", "vol", "dayCounter", "displacement"} for a Volatility,
{"type", "form", "volatility", "displacement"} for a structure, no displacement for the Bachelier engine) and the
outputs' tags. Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/BlackCapFloorEngineCxxVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


cfl = _load("wheel_capfloor", "instruments/capfloor.py")
cov = _load("wheel_constantoptionletvol", "termstructures/volatility/optionlet/constantoptionletvol.py")

TODAY = cfl.TODAY
A360, A365, THIRTY360_BB = cfl.A360, cfl.A365, cfl.THIRTY360_BB
SL, N = cfl.SL, cfl.N
TARGET = cfl.TARGET
TAGS = ["NPV", "vega", "optionletsPrice", "optionletsVega", "optionletsDelta", "optionletsDiscountFactor",
        "optionletsAtmForward", "optionletsStdDev"]
AMOUNTS = {"NPV", "vega", "optionletsPrice", "optionletsVega"}
INSTRUMENTS = ["3Y cap at 3%", "3Y floor at 3%", "3Y collar 3.5% / 2.5%", "10Y cap at 3%", "7Y quarterly cap on 3M",
               "3Y cap, gearing 2, spread 1%", "4Y cap at -0.2% on negative rates",
               "5Y cap, amortizing"]


def black(vol, dc=A365, displacement=0.0):
    return {"type": "BlackCapFloorEngine", "form": "volatility", "vol": vol, "dayCounter": dc,
            "displacement": displacement}


def bachelier(vol, dc=A365):
    return {"type": "BachelierCapFloorEngine", "form": "volatility", "vol": vol, "dayCounter": dc}


def structure(engine, vol, vol_type=SL, shift=0.0, displacement=None, days=None):
    s = (cov.moving_spec(days, TARGET, ql.Following, vol, A365, vol_type, shift) if days is not None
         else cov.fixed_spec(TODAY, TARGET, ql.Following, vol, A365, vol_type, shift))
    out = {"type": engine, "form": "structure", "volatility": s}
    if engine == "BlackCapFloorEngine":
        out["displacement"] = displacement
    return out


def engines_of(name):
    if "negative rates" in name:
        return [black(0.2, A365, 0.03), black(0.15, THIRTY360_BB, 0.02), bachelier(0.009, A360),
                structure("BlackCapFloorEngine", 0.2, SL, 0.03), structure("BlackCapFloorEngine", 0.2, SL, 0.03, 0.03),
                structure("BlackCapFloorEngine", 0.2, SL, 0.03, 0.01), structure("BachelierCapFloorEngine", 0.009, N)]
    return [black(0.2), black(0.3, A360), black(0.25, THIRTY360_BB, 0.01), bachelier(0.008), bachelier(0.01, A360),
            bachelier(0.012, THIRTY360_BB), structure("BlackCapFloorEngine", 0.25),
            structure("BlackCapFloorEngine", 0.25, SL, 0.0, 0.0), structure("BlackCapFloorEngine", 0.25, SL, 0.0, 0.01),
            structure("BlackCapFloorEngine", 0.22, SL, 0.0, None, 2), structure("BlackCapFloorEngine", 0.01, N),
            structure("BachelierCapFloorEngine", 0.009, N), structure("BachelierCapFloorEngine", 0.2, SL)]


def priced(H, spec, curve, e, tag):
    if e["form"] == "volatility":
        if e["type"] == "BlackCapFloorEngine":
            return H.call("BlackCapFloorEngine::calculate(Volatility)", spec, curve, e["vol"], e["dayCounter"],
                          e["displacement"], tag)
        return H.call("BachelierCapFloorEngine::calculate(Volatility)", spec, curve, e["vol"], e["dayCounter"], tag)
    if e["type"] == "BlackCapFloorEngine":
        return H.call("BlackCapFloorEngine::calculate(OptionletVolatilityStructure)", spec, curve, e["volatility"],
                      e["displacement"], tag)
    return H.call("BachelierCapFloorEngine::calculate(OptionletVolatilityStructure)", spec, curve, e["volatility"], tag)


def encoded(x, tag, scale):
    if isinstance(x, dict) or tag not in AMOUNTS:
        return x
    if isinstance(x, list):
        return [v.amount(y, scale) for y in x]
    return v.amount(x, scale)


def case(H, name, spec, curve, settings):
    with H.case_settings(v.enc(TODAY), **settings) as st:
        scale = cfl.scale_of(spec)
        engines = engines_of(name)
        values = [[encoded(v.attempt(lambda e=e, t=t: priced(H, spec, curve, e, t)), t, scale) for t in TAGS]
                  for e in engines]
        return {"name": name, "inputs": {"settings": st, "capFloor": spec, "discountCurve": curve, "engines": engines,
                                         "tags": TAGS},
                "expected": {"values": values}}


def cases():
    rows = {row[0]: row for row in cfl.instruments()}
    out = []
    with cxx.connect() as H:
        for name in INSTRUMENTS:
            _, spec, curve, settings = rows[name]
            assert not settings, name
            out.append(case(H, name, spec, curve, {}))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/capfloor/blackcapfloorengine.hpp", cases)
