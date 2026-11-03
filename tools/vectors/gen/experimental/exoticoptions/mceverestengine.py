# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EverestOption and MCEverestEngine (ql/experimental/exoticoptions/everestoption.hpp, .cpp,
mceverestengine.hpp, .cpp), on the evaluation date 15 October 2026: the Monte Carlo Everest engine's NPV, and its error
estimate under the pseudo-random traits, on stochastic-process arrays of two and three Black-Scholes processes,
pseudo-random and low-discrepancy, on fixed seeds and sample counts, by time steps and by steps per year, with
antithetic paths and with a Brownian bridge; then an expired option and a zero notional, which the option refuses.

The expired option's error estimate is left out: QuantLib's MultiAssetOption leaves it unset on expiry, so reading it
throws, where nanoql's multi-asset results carry the 0 of an instrument's expired results.

Every structure is on Act/365 (Fixed) from the evaluation date and every process shares one risk-free curve, so the
engine's clock is every structure's own: the processes are the MC European basket generator's
(tools/vectors/gen/pricingengines/basket/mceuropeanbasketengine.py). The specs: an option {"notional", "guarantee",
"exercise"} with tools/vectors/gen/instruments/basketoption.py's exercise, and an engine {"processes", "correlation",
"traits" ("pr" | "ld"), "timeSteps", "timeStepsPerYear", "brownianBridge", "antitheticVariate", "requiredSamples",
"seed"}, which tools/vectors/gen/experimental/exoticoptions/mchimalayaengine.py shares. The wheel's calls, by
QuantLib-SWIG 1.44's exoticoptions.i: StochasticProcessArray(processes, correlation); EverestOption(notional, guarantee,
exercise); MCPREverestEngine or MCLDEverestEngine(process, timeSteps, timeStepsPerYear, brownianBridge,
antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed), QuantLib's MCEverestEngine<PseudoRandom> or
<LowDiscrepancy> with Statistics; the option's NPV and errorEstimate. Read back by
tests/NanoQL.Tests/Vectors/Experimental/ExoticOptions/MCEverestEngineVectorTests.cs."""
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


meb = _load("mceuropeanbasketengine", "pricingengines/basket/mceuropeanbasketengine.py")
nd, bo, bsp = meb.nd, meb.bo, meb.bsp
R = bo.R
FLAT2 = meb.fd2.FLAT_PAIR
THREE = nd.THREE
RHO2 = [[1.0, 0.5], [0.5, 1.0]]
RHO3 = nd.RHO3


def engine_spec(processes, correlation, traits, samples, seed, steps=None, per_year=None, bridge=False,
                antithetic=False):
    return {"processes": processes, "correlation": correlation, "traits": traits, "timeSteps": steps,
            "timeStepsPerYear": per_year, "brownianBridge": bridge, "antitheticVariate": antithetic,
            "requiredSamples": samples, "seed": seed}


def array(engine_s):
    processes = [bsp.process(p) for p in engine_s["processes"]]
    return ql.StochasticProcessArray(processes, ql.Matrix(engine_s["correlation"]))


def option_spec(notional, guarantee, days):
    return {"notional": notional, "guarantee": guarantee, "exercise": bo.exercise_spec("European", days)}


def results(option_s, engine_s, expired=False):
    kind = ql.MCPREverestEngine if engine_s["traits"] == "pr" else ql.MCLDEverestEngine
    steps, per_year = engine_s["timeSteps"], engine_s["timeStepsPerYear"]
    kwargs = {"brownianBridge": engine_s["brownianBridge"], "antitheticVariate": engine_s["antitheticVariate"],
              "requiredSamples": engine_s["requiredSamples"], "seed": engine_s["seed"]}
    if steps is not None:
        kwargs["timeSteps"] = steps
    if per_year is not None:
        kwargs["timeStepsPerYear"] = per_year

    def price():
        option = ql.EverestOption(option_s["notional"], option_s["guarantee"], bo.exercise(option_s["exercise"]))
        option.setPricingEngine(kind(array(engine_s), **kwargs))
        return option

    out = {"NPV": v.attempt(lambda: price().NPV())}
    if engine_s["traits"] == "pr" and not expired:
        out["errorEstimate"] = v.attempt(lambda: price().errorEstimate())
    return out


def cases():
    st = v.settings(R)
    setups = [
        ("pseudo-random, two assets, guarantee 0.1, a year, one step a year", option_spec(1.0, 0.1, 365),
         engine_spec(FLAT2, RHO2, "pr", 1023, 42, per_year=1)),
        ("pseudo-random, three assets, two years, four steps, antithetic", option_spec(1.0, 0.0, 730),
         engine_spec(THREE, RHO3, "pr", 2047, 86421, steps=4, antithetic=True)),
        ("pseudo-random, three assets, notional 1000, twelve steps a year", option_spec(1000.0, 0.05, 365),
         engine_spec(THREE, RHO3, "pr", 1023, 7, per_year=12)),
        ("pseudo-random, two assets, Brownian bridge", option_spec(1.0, 0.1, 365),
         engine_spec(FLAT2, RHO2, "pr", 1023, 42, steps=2, bridge=True)),
        ("low-discrepancy, two assets, one step", option_spec(1.0, 0.1, 365),
         engine_spec(FLAT2, RHO2, "ld", 1023, 42, steps=1)),
        ("low-discrepancy, three assets, three steps", option_spec(1.0, 0.02, 548),
         engine_spec(THREE, RHO3, "ld", 2047, 5, steps=3)),
        ("expired", option_spec(1.0, 0.1, -1), engine_spec(FLAT2, RHO2, "pr", 1023, 42, steps=1)),
        ("a zero notional (refused)", option_spec(0.0, 0.1, 365), engine_spec(FLAT2, RHO2, "pr", 1023, 42, steps=1)),
    ]
    return [{"name": name, "inputs": {"settings": st, "option": o, "engine": e},
             "expected": results(o, e, expired=name == "expired")} for name, o, e in setups]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/exoticoptions/mceverestengine.hpp", cases)
