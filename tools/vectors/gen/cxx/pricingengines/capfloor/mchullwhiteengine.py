# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCHullWhiteCapFloorEngine and MakeMCHullWhiteCapFloorEngine
(ql/pricingengines/capfloor/mchullwhiteengine.hpp, .cpp), which the wheel doesn't expose, each output computed by the
C++ harness on Statistics, on the evaluation date 15 January 2025: 13 instruments of
tools/vectors/gen/instruments/capfloor.py, whose functions build them, each priced on six engines; and 7 cases of the
builder on one cap, its refusals among them.

The instruments cover caps and floors from the spot date at, in and out of the money, a quarterly cap on Euribor 3M,
gearings and spreads, amortizing notionals, seasoned instruments with past fixings (their past caplets skipped, those
fixed on or before the evaluation date paying their known forwards), a caplet fixing today with its fixing stored, an
instrument paid in full, negative rates and a discount curve dated at the spot date. The model is a HullWhite (a = 0.05,
sigma = 0.01) on the case's discount curve, the engine's process a HullWhiteForwardProcess in the measure of the last
payment, its grid the fixings after the reference date and the last payment. Every seed is explicit: QuantLib's 0, a
seed from the clock and the builder's default, which nanoql refuses, is never given.

The engines: pseudo-random with 2,000 samples, plain and antithetic; with a Brownian bridge and 1,500; to an absolute
tolerance of 1e-4 times the leg's largest notional within 100,000 samples, antithetic; low-discrepancy with a Brownian
bridge and 1,023 samples, whose error estimate QuantLib doesn't provide ("error estimate not provided"); and
low-discrepancy without the bridge. Each output is [NPV, errorEstimate] per engine, amounts compared at the leg's
largest notional. The builder's cases call its setters in order, then price: the samples, a tolerance with a maximum,
the bridge and antithetic variates, and the refusals of samples after a tolerance ("tolerance already set"), a
tolerance after samples ("number of samples already set"), a tolerance on low-discrepancy traits ("chosen random
generator policy does not allow an error estimate"), and neither ("neither tolerance nor number of samples set", at
the calculation).

Generated and left out: the collars, which QuantLib's path pricer prices as their floors alone, long, since it reads
the cap rates only for a cap (mchullwhiteengine.cpp:54, :94-99), where nanoql's prices them as cap less floor
(tests/NanoQL.Tests/Divergences/McHullWhiteCollarTests.cs). Each case's inputs hold the instrument, the discount
curve, the model's parameters and the engines ({"traits", "brownianBridge", "antitheticVariate", "requiredSamples",
"requiredTolerance", "maxSamples", "seed"}, nulls QuantLib's) or the builder's traits and setters. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/MCHullWhiteCapFloorEngineCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


cfl = _load("wheel_capfloor", "instruments/capfloor.py")

TODAY = cfl.TODAY
HULL_WHITE = {"a": 0.05, "sigma": 0.01}
TAGS = ["NPV", "errorEstimate"]

INSTRUMENTS = ["1Y cap at 3%", "3Y cap at 3%", "3Y floor at 3%", "5Y cap in the money", "5Y floor far out of the money",
               "7Y quarterly cap on 3M", "3Y cap, gearing 2, spread 1%", "5Y cap, amortizing",
               "seasoned cap, past fixings", "seasoned floor, past fixings", "cap fixing today, stored",
               "cap paid in full", "4Y cap at -0.2% on negative rates", "3Y cap on a spot-dated Actual/360 curve"]
LEFT_OUT = ["3Y collar 3.5% / 2.5%", "3Y collar, cap below floor", "seasoned collar, past fixings"]


def engine(traits, samples=None, tolerance=None, max_samples=None, seed=42, bb=False, anti=False):
    return {"traits": traits, "brownianBridge": bb, "antitheticVariate": anti, "requiredSamples": samples,
            "requiredTolerance": tolerance, "maxSamples": max_samples, "seed": seed}


def engines(scale):
    return [engine("PseudoRandom", 2000, seed=42), engine("PseudoRandom", 2000, seed=7, anti=True),
            engine("PseudoRandom", 1500, seed=11, bb=True),
            engine("PseudoRandom", tolerance=1e-4 * scale, max_samples=100000, seed=5, anti=True),
            engine("LowDiscrepancy", 1023, seed=3, bb=True), engine("LowDiscrepancy", 1023, seed=19)]


BUILDS = [
    ("samples", "PseudoRandom", [["withSeed", 42], ["withSamples", 1000]]),
    ("tolerance with a maximum, antithetic", "PseudoRandom",
     [["withSeed", 42], ["withAbsoluteTolerance", 50.0], ["withMaxSamples", 50000], ["withAntitheticVariate", True]]),
    ("bridge, low-discrepancy", "LowDiscrepancy", [["withSeed", 9], ["withBrownianBridge", True], ["withSamples", 511]]),
    ("refused: samples after a tolerance", "PseudoRandom",
     [["withSeed", 42], ["withAbsoluteTolerance", 50.0], ["withSamples", 1000]]),
    ("refused: a tolerance after samples", "PseudoRandom",
     [["withSeed", 42], ["withSamples", 1000], ["withAbsoluteTolerance", 50.0]]),
    ("refused: a tolerance on low-discrepancy traits", "LowDiscrepancy",
     [["withSeed", 1], ["withAbsoluteTolerance", 50.0]]),
    ("refused: neither samples nor a tolerance", "PseudoRandom", [["withSeed", 42]]),
]


def encoded(x, scale):
    return x if isinstance(x, dict) else v.amount(x, scale)


def priced(H, spec, curve, e, tag, scale):
    return encoded(v.attempt(lambda: H.call(
        "MCHullWhiteCapFloorEngine::calculate", spec, curve, HULL_WHITE["a"], HULL_WHITE["sigma"], e["traits"],
        e["brownianBridge"], e["antitheticVariate"], e["requiredSamples"], e["requiredTolerance"], e["maxSamples"],
        e["seed"], tag)), scale)


def harness_settings(spec, settings):
    """capfloor.py's case settings as the harness stores them: the fixings by the index's name."""
    kw = cfl.settings_of(spec, settings)
    if "fixings" in kw:
        kw["fixings"] = {index.name(): [[v.enc(d), x] for d, x in pairs] for index, pairs in kw["fixings"]}
    return kw


def cases():
    rows = {row[0]: row for row in cfl.instruments()}
    out = []
    with cxx.connect() as H:
        for name in INSTRUMENTS + LEFT_OUT:
            _, spec, curve, settings = rows[name]
            with H.case_settings(v.enc(TODAY), **harness_settings(spec, settings)) as st:
                scale = cfl.scale_of(spec)
                es = engines(scale)
                values = [[priced(H, spec, curve, e, t, scale) for t in TAGS] for e in es]
                if name in LEFT_OUT:
                    continue
                out.append({"name": name,
                            "inputs": {"settings": st, "capFloor": spec, "discountCurve": curve,
                                       "hullWhite": HULL_WHITE, "engines": es, "tags": TAGS},
                            "expected": {"values": values}})
        _, spec, curve, settings = rows["3Y cap at 3%"]
        for name, traits, setters in BUILDS:
            with H.case_settings(v.enc(TODAY), **harness_settings(spec, settings)) as st:
                scale = cfl.scale_of(spec)
                npv = encoded(v.attempt(lambda: H.call(
                    "MakeMCHullWhiteCapFloorEngine::calculate", spec, curve, HULL_WHITE["a"], HULL_WHITE["sigma"],
                    traits, setters, "NPV")), scale)
                out.append({"name": "builder / " + name,
                            "inputs": {"settings": st, "capFloor": spec, "discountCurve": curve,
                                       "hullWhite": HULL_WHITE, "traits": traits, "setters": setters},
                            "expected": {"NPV": npv}})
    for c in out[-4:]:
        assert c["expected"]["NPV"] == v.THROWS, c["name"]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/capfloor/mchullwhiteengine.hpp", cases)
