# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's McSimulation (ql/pricingengines/mcsimulation.hpp), the base of the Monte Carlo engines, with
the MonteCarloModel it builds (ql/methods/montecarlo/montecarlomodel.hpp) and the single-variate traits
(mctraits.hpp), through the wheel's MCEuropeanEngine, QuantLib's European engine on McSimulation<SingleVariate, RNG,
Statistics>: a one-year European call struck at 100 on a Black-Scholes-Merton process (spot 100, a flat 5% risk-free
and 2% dividend yield on Actual/365 Fixed, a 20% constant volatility, all dated 15 March 2026), pseudo-random and
Sobol, 1 and 10 time steps, 1,000 and 4,095 samples, seeds 42 and 1,234,567, with and without antithetic variates
(pseudo-random only) and a Brownian bridge; four tolerance-driven cases; and a tolerance the sample limit can't
reach, which McSimulation::value refuses. Each case records the NPV and, where the traits allow it, the error
estimate.

The C# side runs a test-only engine on the port's McSimulation with QuantLib's European path pricer and
MCVanillaEngine's time grid and path generator (the S-sr plan's SSD7: no S-sr engine lands before C2). The wheel's
MCEuropeanEngine(process, traits, timeSteps, timeStepsPerYear, brownianBridge, antitheticVariate, requiredSamples,
requiredTolerance, maxSamples, seed) is MakeMCEuropeanEngine's settings, each the C++ engine's. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/McSimulationVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.March, 2026)


def option_and_process():
    dc = ql.Actual365Fixed()
    process = ql.BlackScholesMertonProcess(
        ql.QuoteHandle(ql.SimpleQuote(100.0)), ql.YieldTermStructureHandle(ql.FlatForward(TODAY, 0.02, dc)),
        ql.YieldTermStructureHandle(ql.FlatForward(TODAY, 0.05, dc)),
        ql.BlackVolTermStructureHandle(ql.BlackConstantVol(TODAY, ql.TARGET(), 0.2, dc)))
    option = ql.VanillaOption(ql.PlainVanillaPayoff(ql.Option.Call, 100.0),
                              ql.EuropeanExercise(TODAY + ql.Period(1, ql.Years)))
    return option, process


def run(rng, steps, antithetic, bridge, seed, samples=None, tolerance=None, max_samples=None):
    option, process = option_and_process()
    kw = {"timeSteps": steps, "brownianBridge": bridge, "antitheticVariate": antithetic, "seed": seed}
    if samples is not None:
        kw["requiredSamples"] = samples
    if tolerance is not None:
        kw["requiredTolerance"] = tolerance
    if max_samples is not None:
        kw["maxSamples"] = max_samples
    option.setPricingEngine(ql.MCEuropeanEngine(process, rng, **kw))
    npv = v.attempt(lambda: option.NPV())
    if isinstance(npv, dict) or rng != "pseudorandom":
        return {"npv": npv}
    return {"npv": npv, "errorEstimate": option.errorEstimate()}


def cases():
    st = v.settings(TODAY)
    out = []
    for rng in ("pseudorandom", "lowdiscrepancy"):
        for steps in (1, 10):
            for samples in (1000, 4095):
                for seed in (42, 1234567):
                    for antithetic in (False, True):
                        if rng == "lowdiscrepancy" and antithetic:
                            continue
                        for bridge in (False, True):
                            out.append({"name": f"{rng}, {steps} steps, {samples} samples, seed {seed}, antithetic "
                                                f"{antithetic}, bridge {bridge}",
                                        "inputs": {"settings": st, "rng": rng, "steps": steps, "samples": samples,
                                                   "seed": seed, "antithetic": antithetic, "bridge": bridge},
                                        "expected": run(rng, steps, antithetic, bridge, seed, samples=samples)})
    for tolerance in (0.05, 0.02):
        for antithetic in (False, True):
            out.append({"name": f"tolerance {tolerance!r}, antithetic {antithetic}",
                        "inputs": {"settings": st, "rng": "pseudorandom", "steps": 1, "tolerance": tolerance,
                                   "seed": 42, "antithetic": antithetic, "bridge": False},
                        "expected": run("pseudorandom", 1, antithetic, False, 42, tolerance=tolerance)})
    out.append({"name": "tolerance beyond the sample limit",
                "inputs": {"settings": st, "rng": "pseudorandom", "steps": 1, "tolerance": 0.001, "maxSamples": 5000,
                           "seed": 42, "antithetic": False, "bridge": False},
                "expected": run("pseudorandom", 1, False, False, 42, tolerance=0.001, max_samples=5000)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/mcsimulation.hpp", cases)
