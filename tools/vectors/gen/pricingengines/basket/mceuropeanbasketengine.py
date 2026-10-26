# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCEuropeanBasketEngine (ql/pricingengines/basket/mceuropeanbasketengine.hpp, .cpp), on the
evaluation date 15 October 2026: the Monte Carlo European basket engine's NPV, and its error estimate under the
pseudo-random traits, for maximum, minimum, spread and average basket options on stochastic-process arrays of two and
three Black-Scholes processes, pseudo-random and low-discrepancy, on fixed seeds and sample counts, by time steps and by
steps per year, with antithetic paths, and a Brownian bridge, which QuantLib's multi-path generator refuses.

Every structure is on Act/365 (Fixed) from the evaluation date and every process shares one risk-free curve: on
structures dated or day-counted apart QuantLib evolves every process on the first's times
(tests/NanoQL.Tests/Divergences/McBasketMismatchedTimesTests.cs).

The specs are tools/vectors/gen/instruments/basketoption.py's options and an engine {"processes", "correlation",
"traits" ("pr" | "ld"), "timeSteps", "timeStepsPerYear", "brownianBridge", "antitheticVariate", "requiredSamples",
"seed"}. The wheel's calls, by QuantLib-SWIG 1.44's basketoptions.i and stochasticprocess.i:
StochasticProcessArray(processes, correlation), the C++ constructor; MCEuropeanBasketEngine(process, traits, timeSteps,
timeStepsPerYear, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed), the
Python-side function forwarding to MCPREuropeanBasketEngine or MCLDEuropeanBasketEngine, QuantLib's
MCEuropeanBasketEngine<PseudoRandom> or <LowDiscrepancy>, with Statistics; BasketOption's NPV and errorEstimate. Read
back by tests/NanoQL.Tests/Vectors/PricingEngines/Basket/MCEuropeanBasketEngineVectorTests.cs."""
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


nd = _load("fdndimblackscholesvanillaengine", "pricingengines/basket/fdndimblackscholesvanillaengine.py")
fd2, bo, bc, bsp = nd.fd2, nd.bo, nd.bc, nd.bsp
R = bo.R
CALL, PUT = bo.CALL, bo.PUT


def engine_spec(processes, correlation, traits, samples, seed, steps=None, per_year=None, bridge=False,
                antithetic=False):
    return {"processes": processes, "correlation": correlation, "traits": traits, "timeSteps": steps,
            "timeStepsPerYear": per_year, "brownianBridge": bridge, "antitheticVariate": antithetic,
            "requiredSamples": samples, "seed": seed}


def results(option_s, engine_s):
    option, keep = bo.option(option_s)
    processes = [bsp.process(p) for p in engine_s["processes"]]
    array = ql.StochasticProcessArray(processes, ql.Matrix(engine_s["correlation"]))
    option.setPricingEngine(ql.MCEuropeanBasketEngine(
        array, engine_s["traits"], timeSteps=engine_s["timeSteps"], timeStepsPerYear=engine_s["timeStepsPerYear"],
        brownianBridge=engine_s["brownianBridge"], antitheticVariate=engine_s["antitheticVariate"],
        requiredSamples=engine_s["requiredSamples"], seed=engine_s["seed"]))
    out = {"NPV": v.attempt(option.NPV)}
    if engine_s["traits"] == "pr":
        out["errorEstimate"] = v.attempt(option.errorEstimate)
    return out


def cases():
    st = v.settings(R)
    european = bo.exercise_spec("European", 365)
    rho2 = [[1.0, 0.5], [0.5, 1.0]]
    max_call = bo.payoff_spec("Max", bc.payoff_spec("PlainVanilla", CALL, 100.0))
    min_put = bo.payoff_spec("Min", bc.payoff_spec("PlainVanilla", PUT, 100.0))
    spread_call = bo.payoff_spec("Spread", bc.payoff_spec("PlainVanilla", CALL, 5.0))
    avg3 = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", CALL, 100.0), n=3)
    max3 = bo.payoff_spec("Max", bc.payoff_spec("PlainVanilla", PUT, 110.0))
    flat2, zero2, three = fd2.FLAT_PAIR, fd2.ZERO_PAIR, nd.THREE
    setups = [
        ("pseudo-random, maximum call, one step", max_call, engine_spec(flat2, rho2, "pr", 1023, 42, steps=1)),
        ("pseudo-random, minimum put, four steps, antithetic", min_put,
         engine_spec(flat2, rho2, "pr", 1023, 1234, steps=4, antithetic=True)),
        ("pseudo-random, spread call, steps per year", spread_call,
         engine_spec(flat2, [[1.0, -0.3], [-0.3, 1.0]], "pr", 2047, 7, per_year=12)),
        ("pseudo-random, zero curves, maximum call", max_call, engine_spec(zero2, rho2, "pr", 1023, 42, steps=3)),
        ("pseudo-random, three assets, average call", avg3, engine_spec(three, nd.RHO3, "pr", 1023, 99, steps=2)),
        ("low-discrepancy, maximum call", max_call, engine_spec(flat2, rho2, "ld", 1023, 42, steps=1)),
        ("low-discrepancy, spread call, Brownian bridge (refused)", spread_call,
         engine_spec(flat2, rho2, "ld", 1023, 42, steps=4, bridge=True)),
        ("low-discrepancy, spread call, four steps", spread_call, engine_spec(flat2, rho2, "ld", 1023, 42, steps=4)),
        ("low-discrepancy, three assets, maximum put", max3, engine_spec(three, nd.RHO3, "ld", 2047, 5, steps=2)),
    ]
    out = []
    for name, payoff, engine in setups:
        option = bo.option_spec(payoff, european)
        out.append({"name": name, "inputs": {"settings": st, "option": option, "engine": engine},
                    "expected": results(option, engine)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/mceuropeanbasketengine.hpp", cases)
