# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HimalayaOption and MCHimalayaEngine (ql/experimental/exoticoptions/himalayaoption.hpp, .cpp,
mchimalayaengine.hpp, .cpp), on the evaluation date 15 October 2026: the Monte Carlo Himalaya engine's NPV, and its
error estimate under the pseudo-random traits, on stochastic-process arrays of two and three Black-Scholes processes,
pseudo-random and low-discrepancy, on fixed seeds and sample counts, with antithetic paths and with a Brownian bridge,
on fewer fixings than assets, as many and more; then an expired option, a fixing before the evaluation date and
unsorted fixings, which the engine refuses.

The expired option's error estimate is left out: QuantLib's MultiAssetOption leaves it unset on expiry, so reading it
throws, where nanoql's multi-asset results carry the 0 of an instrument's expired results.

Every structure is on Act/365 (Fixed) from the evaluation date and every process shares one risk-free curve, so the
engine's clock is every structure's own. The specs: an option {"fixingDates" (serial numbers), "strike"}, and
tools/vectors/gen/experimental/exoticoptions/mceverestengine.py's engine, its steps unused (the engine's grid is the
fixings'). The wheel's calls, by QuantLib-SWIG 1.44's exoticoptions.i: StochasticProcessArray(processes, correlation);
HimalayaOption(fixingDates, strike); MCPRHimalayaEngine or MCLDHimalayaEngine(process, brownianBridge,
antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed), QuantLib's MCHimalayaEngine<PseudoRandom> or
<LowDiscrepancy> with Statistics; the option's NPV and errorEstimate. Read back by
tests/NanoQL.Tests/Vectors/Experimental/ExoticOptions/MCHimalayaEngineVectorTests.cs."""
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


ev = _load("mceverestengine", "experimental/exoticoptions/mceverestengine.py")
R = ev.R
engine_spec = ev.engine_spec


def option_spec(days, strike):
    return {"fixingDates": [R.serialNumber() + d for d in days], "strike": strike}


def results(option_s, engine_s, expired=False):
    kind = ql.MCPRHimalayaEngine if engine_s["traits"] == "pr" else ql.MCLDHimalayaEngine

    def price():
        dates = [ql.Date(d) for d in option_s["fixingDates"]]
        option = ql.HimalayaOption(dates, option_s["strike"])
        option.setPricingEngine(kind(ev.array(engine_s), brownianBridge=engine_s["brownianBridge"],
                                     antitheticVariate=engine_s["antitheticVariate"],
                                     requiredSamples=engine_s["requiredSamples"], seed=engine_s["seed"]))
        return option

    out = {"NPV": v.attempt(lambda: price().NPV())}
    if engine_s["traits"] == "pr" and not expired:
        out["errorEstimate"] = v.attempt(lambda: price().errorEstimate())
    return out


def cases():
    st = v.settings(R)
    flat2, three, rho2, rho3 = ev.FLAT2, ev.THREE, ev.RHO2, ev.RHO3
    quarters = [91, 182, 273, 365]
    setups = [
        ("pseudo-random, three assets, four quarterly fixings", option_spec(quarters, 100.0),
         engine_spec(three, rho3, "pr", 1023, 86421)),
        ("pseudo-random, two assets, two fixings, antithetic", option_spec([182, 365], 95.0),
         engine_spec(flat2, rho2, "pr", 2047, 42, antithetic=True)),
        ("pseudo-random, two assets, three fixings", option_spec([120, 240, 365], 98.0),
         engine_spec(flat2, rho2, "pr", 1023, 7)),
        ("pseudo-random, three assets, one fixing", option_spec([365], 105.0),
         engine_spec(three, rho3, "pr", 1023, 99)),
        ("pseudo-random, two assets, Brownian bridge", option_spec([182, 365], 95.0),
         engine_spec(flat2, rho2, "pr", 1023, 42, bridge=True)),
        ("low-discrepancy, three assets, four quarterly fixings", option_spec(quarters, 100.0),
         engine_spec(three, rho3, "ld", 1023, 42)),
        ("expired", option_spec([-30, -1], 100.0), engine_spec(flat2, rho2, "pr", 1023, 42)),
        ("a fixing before the evaluation date (refused)", option_spec([-1, 182], 100.0),
         engine_spec(flat2, rho2, "pr", 1023, 42)),
        ("unsorted fixings (refused)", option_spec([365, 182], 100.0), engine_spec(flat2, rho2, "pr", 1023, 42)),
    ]
    return [{"name": name, "inputs": {"settings": st, "option": o, "engine": e},
             "expected": results(o, e, expired=name == "expired")} for name, o, e in setups]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/exoticoptions/mchimalayaengine.hpp", cases)
