# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EuropeanHestonPathPricer and MakeMCEuropeanHestonEngine
(ql/pricingengines/vanilla/mceuropeanhestonengine.hpp), which the wheel doesn't bind, each output computed by the C++
harness (docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026.

- The path pricer: calls and puts at three strikes (one of them 0) and two discounts on fixed two-asset paths (the
  spot and the variance) on grids of one, two and four steps, the payoff read on the first asset's last value; and the
  refusal of a negative strike.
- The builder: the NPV and error estimate (null under low-discrepancy numbers, which give none) of a European call and
  put on tools/vectors/gen/cxx/models/equity/batesmodel.py's first Heston process and on a Bates process, under the
  setters in turn (steps or steps per year, samples or a tolerance with a maximum, antithetic variates, pseudo-random
  and low-discrepancy numbers), each on a fixed seed; then the builder's refusals, their messages QuantLib's: steps
  after steps per year and the reverse, samples after a tolerance and the reverse, a tolerance under low-discrepancy
  numbers, and no steps.

Left out (the S-hes plan's §2.10): structures off the process's reference date and day counter (HR7 (vi),
tests/NanoQL.Tests/Divergences/McHestonMismatchedTimesTests.cs); a seed of 0, QuantLib's seed from the clock, which
nanoql refuses (the master plan's S2R4): every case sets its seed first.

A path case holds the "optionType", "strike", "discount", "times" and "paths" (one list of values per asset, each of
the grid's size); a builder case the "traits", the "process" (batesmodel.py's harness spec), the "optionType",
"strike", "exerciseDate" and the "setters" ([name, value]). The harness's calls: EuropeanHestonPathPricer::value and
MakeMCEuropeanHestonEngine::results (tools/cxx/src/pricingengines/vanilla/mceuropeanhestonengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/MCEuropeanHestonEngineCxxVectorTests.cs."""
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


bm = _load("cxx_batesmodel", "cxx/models/equity/batesmodel.py")
R = bm.R
CALL, PUT = 1, -1
PATHS = [
    ([0.0, 1.0], [[100.0, 112.5], [0.04, 0.051]]),
    ([0.0, 0.5, 1.0], [[100.0, 93.2, 87.75], [0.04, 0.062, 0.0]]),
    ([0.0, 0.25, 0.5, 0.75, 1.0], [[100.0, 104.0, 99.5, 101.25, 100.0], [0.04, 0.035, 0.041, 0.044, 0.039]]),
]
SETTINGS = [
    ("PseudoRandom", "steps and samples", [["withSeed", 42], ["withSteps", 10], ["withSamples", 1000]]),
    ("PseudoRandom", "steps per year, antithetic", [["withSeed", 7], ["withStepsPerYear", 12],
                                                    ["withAntitheticVariate", True], ["withSamples", 500]]),
    ("PseudoRandom", "tolerance", [["withSeed", 11], ["withSteps", 5], ["withAntitheticVariate", True],
                                   ["withAbsoluteTolerance", 0.2], ["withMaxSamples", 50000]]),
    ("LowDiscrepancy", "steps and samples", [["withSeed", 3], ["withSteps", 8], ["withSamples", 255]]),
]
REFUSED = [
    ("PseudoRandom", "steps after steps per year", [["withSeed", 1], ["withStepsPerYear", 12], ["withSteps", 10]]),
    ("PseudoRandom", "steps per year after steps", [["withSeed", 1], ["withSteps", 10], ["withStepsPerYear", 12]]),
    ("PseudoRandom", "samples after a tolerance", [["withSeed", 1], ["withAbsoluteTolerance", 0.1],
                                                   ["withSamples", 100]]),
    ("PseudoRandom", "a tolerance after samples", [["withSeed", 1], ["withSamples", 100],
                                                   ["withAbsoluteTolerance", 0.1]]),
    ("LowDiscrepancy", "a tolerance", [["withSeed", 1], ["withSteps", 4], ["withAbsoluteTolerance", 0.1]]),
    ("PseudoRandom", "no steps", [["withSeed", 1], ["withSamples", 100]]),
]


def cases():
    out = []
    heston = bm.process_spec(bm.HESTON[0])
    bates = bm.BATES[0][1]
    with cxx.connect() as H:
        st = H.settings(R)
        for times, paths in PATHS:
            for option_type in (CALL, PUT):
                for discount in (1.0, 0.96):
                    strikes = [0.0, 95.0, 110.0]
                    values = [v.attempt(lambda k=k: H.call("EuropeanHestonPathPricer::value", option_type, k,
                                                           discount, times, paths)) for k in strikes]
                    out.append({"name": f"path pricer, {len(times) - 1} steps, {option_type}, discount {discount}",
                                "inputs": {"settings": st, "kind": "path", "optionType": option_type,
                                           "strikes": strikes, "discount": discount, "times": times,
                                           "paths": paths},
                                "expected": {"value": values}})
        out.append({"name": "path pricer, negative strike",
                    "inputs": {"settings": st, "kind": "path", "optionType": CALL, "strikes": [-1.0],
                               "discount": 1.0, "times": PATHS[0][0], "paths": PATHS[0][1]},
                    "expected": {"value": [v.attempt(lambda: H.call("EuropeanHestonPathPricer::value", CALL, -1.0,
                                                                    1.0, PATHS[0][0], PATHS[0][1]))]}})

        def builder(name, traits, process, option_type, strike, setters, refused=False):
            try:
                r = H.call("MakeMCEuropeanHestonEngine::results", traits, process, option_type, strike, R + 365,
                           setters)
                expected = {"NPV": v.enc(r["NPV"]), "errorEstimate": v.enc(r["errorEstimate"])}
                assert not refused, name
            except RuntimeError as e:
                assert refused, (name, e)
                expected = {"error": str(e)}
            out.append({"name": f"builder, {name}",
                        "inputs": {"settings": st, "kind": "builder", "traits": traits, "process": process,
                                   "optionType": option_type, "strike": strike, "exerciseDate": R + 365,
                                   "setters": setters},
                        "expected": expected})

        for traits, sname, setters in SETTINGS:
            for pname, process in (("Heston", heston), ("Bates", bates)):
                for option_type, strike in ((CALL, 105.0), (PUT, 95.0)):
                    builder(f"{traits}, {sname}, {pname}, {'call' if option_type == CALL else 'put'}", traits,
                            process, option_type, strike, setters)
        for traits, name, setters in REFUSED:
            builder(f"{traits}, refused: {name}", traits, heston, CALL, 100.0, setters, refused=True)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/mceuropeanhestonengine.hpp", cases)
