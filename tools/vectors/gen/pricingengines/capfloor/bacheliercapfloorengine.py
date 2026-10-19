# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BachelierCapFloorEngine (ql/pricingengines/capfloor/bacheliercapfloorengine.hpp): 22
instruments of tools/vectors/gen/instruments/capfloor.py, whose functions build them, each priced by the wheel's
engine in two to four forms, on the evaluation date 15 January 2025.

The engines are the constructor on a volatility quote (0.8% and 1.2% normal volatilities), whose constant volatility
QuantLib builds as shifted lognormal, as its default, and reads as normal; and the constructor on an optionlet
volatility structure, a normal ConstantOptionletVolatility with a fixed reference date (a reference date other than the
evaluation date among them), or a shifted lognormal one, which QuantLib refuses ("BachelierCapFloorEngine should only be
used for vol surfaces stripped with normal model"). The wheel's quote constructor takes no day counter, so every quote
case is on C++'s default, Actual/365 (Fixed); the harness's cxx/pricingengines/capfloor/blackcapfloorengine prices the
others. The instruments are blackcapfloorengine.py's, the Bachelier formulas pricing negative rates and strikes
without a displacement, and a negative gearing, which makes the formulas' discount negative ("discount (...) must be
positive").

The outputs and inputs are blackcapfloorengine.py's: per engine, the NPV, the vega and the per-optionlet results, a
refused pricing or a result the engine didn't set {"throws": true}; an engine is {"form": "quote", "vol"} or
{"form": "structure", "volatility", "displacement": null}. The wheel's BachelierCapFloorEngine is the proxy of the C++
constructors on a quote handle and on a structure handle (QuantLib-SWIG 1.44, SWIG/capfloor.i). Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/BachelierCapFloorEngineVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_blackcapfloorengine", os.path.join(v.GEN_ROOT, "pricingengines", "capfloor", "blackcapfloorengine.py"))
bce = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bce)
SL, N = bce.SL, bce.N


def bachelier_engines(name):
    """The Bachelier engines an instrument is priced with."""
    out = [bce.bachelier_quote(0.008), bce.bachelier_quote(0.012)]
    if name in ("3Y cap at 3%", "3Y collar 3.5% / 2.5%", "seasoned cap, past fixings", "4Y cap at -0.2% on negative rates",
                "4Y floor at 0% on negative rates"):
        out += [bce.structure(0.009, N), bce.structure(0.009, N, 0.0, None, bce.LATER)]
    if name in ("3Y floor at 3%", "10Y cap at 3%"):
        out += [bce.structure(0.2, SL)]
    for e in out:
        if e["form"] == "structure":
            e["displacement"] = None
    return out


INSTRUMENTS = [
    "1Y cap at 3%", "3Y cap at 3%", "3Y floor at 3%", "3Y collar 3.5% / 2.5%", "10Y cap at 3%", "5Y cap in the money",
    "5Y floor far out of the money", "7Y quarterly cap on 3M", "4Y collar, several rates", "3Y collar, cap below floor",
    "3Y cap, gearing 2, spread 1%", "3Y cap, gearing -1, spread 6%", "5Y cap, amortizing",
    "3Y cap, indexed coupons by the switch", "seasoned cap, past fixings", "seasoned collar, past fixings",
    "cap fixing today, stored", "cap paid in full", "4Y cap at -0.2% on negative rates",
    "4Y floor at 0% on negative rates", "3Y cap on a spot-dated Actual/360 curve", "refused at pricing: a fixed-rate leg",
]


def cases():
    return bce.cases_for(INSTRUMENTS, bachelier_engines, bce.bachelier_engine, "Bachelier")


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/capfloor/bacheliercapfloorengine.hpp", cases)
