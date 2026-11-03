# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCHestonHullWhiteEngine, HestonHullWhitePathPricer and MakeMCHestonHullWhiteEngine
(ql/pricingengines/vanilla/mchestonhullwhiteengine.hpp, .cpp), which the wheel doesn't bind, each output computed by the
C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026.

- The path pricer: calls and puts at three strikes (one of them 0) on fixed three-asset paths (the spot, the variance
  and the short rate's state) on grids of one and two steps, the payoff read on the first asset's last value and
  discounted by the process's numeraire at the exercise time.
- The builder: the NPV and error estimate (null under low-discrepancy numbers, which give none) of a European call and
  put on two hybrid processes (tools/vectors/gen/cxx/processes/hybridhestonhullwhiteprocess.py's specs: the equity
  Heston process with the reversion 0.1 at the correlation -0.4 under the BSM-Hull-White discretization, and with the
  reversion 0.5 at 0.3 under Euler), under the setters in turn (steps or steps per year, samples or a tolerance with a
  maximum, antithetic variates, the control variate, pseudo-random and low-discrepancy numbers), each on a fixed seed;
  then the builder's refusals, their messages QuantLib's: steps after steps per year and the reverse, samples after a
  tolerance and the reverse, a tolerance under low-discrepancy numbers, and no steps.

Left out: steps whose a·dt falls below the hybrid's step cut, 0.1: a year in one step at the reversion 0.1, quarters at
0.5; structures off the process's reference date and day counter; a forward process on another curve; and a seed of 0,
QuantLib's seed from the clock, which nanoql refuses: every case sets its seed first.

A path case holds the "process", "exerciseTime", "optionType", "strikes", "times" and "paths" (one list of values per
asset, each of the grid's size); a builder case the "traits", the "process", the "optionType", "strike", "exerciseDate"
and the "setters" ([name, value]). The harness's calls: HestonHullWhitePathPricer::value and
MakeMCHestonHullWhiteEngine::results (tools/cxx/src/pricingengines/vanilla/mchestonhullwhiteengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/MCHestonHullWhiteEngineCxxVectorTests.cs."""
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


hhp = _load("cxx_hybridhestonhullwhiteprocess", "cxx/processes/hybridhestonhullwhiteprocess.py")
bm = hhp.bm
R = bm.R
CALL, PUT = 1, -1
HESTON = bm.process_spec(bm.HESTON[0])
PROCESSES = [
    ("reversion 0.1, BSM-Hull-White", hhp.process_spec(HESTON, (0.1, 0.01, 2.0), -0.4, hhp.BSM_HULL_WHITE), 1),
    ("reversion 0.5, Euler", hhp.process_spec(HESTON, (0.5, 0.02, 2.0), 0.3, hhp.EULER), 4),
]
PATHS = [
    ([0.0, 1.0], [[100.0, 112.5], [0.04, 0.051], [0.0, 0.012]]),
    ([0.0, 0.5, 1.0], [[100.0, 93.2, 87.75], [0.04, 0.062, 0.0], [0.0, -0.01, 0.02]]),
]


def settings(steps_per_year):
    """The setters in turn, each a year's steps at the process's step count."""
    return [
        ("PseudoRandom", "steps and samples", [["withSeed", 42], ["withStepsPerYear", steps_per_year],
                                                ["withSamples", 1000]]),
        ("PseudoRandom", "steps, antithetic, control variate", [["withSeed", 7], ["withSteps", steps_per_year],
                                                                 ["withAntitheticVariate", True],
                                                                 ["withControlVariate", True], ["withSamples", 500]]),
        ("PseudoRandom", "tolerance", [["withSeed", 11], ["withSteps", steps_per_year], ["withAntitheticVariate", True],
                                       ["withAbsoluteTolerance", 0.2], ["withMaxSamples", 50000]]),
        ("LowDiscrepancy", "steps and samples", [["withSeed", 3], ["withSteps", steps_per_year],
                                                 ["withSamples", 255]]),
    ]


REFUSED = [
    ("PseudoRandom", "steps after steps per year", [["withSeed", 1], ["withStepsPerYear", 1], ["withSteps", 1]]),
    ("PseudoRandom", "steps per year after steps", [["withSeed", 1], ["withSteps", 1], ["withStepsPerYear", 1]]),
    ("PseudoRandom", "samples after a tolerance", [["withSeed", 1], ["withAbsoluteTolerance", 0.1],
                                                   ["withSamples", 100]]),
    ("PseudoRandom", "a tolerance after samples", [["withSeed", 1], ["withSamples", 100],
                                                   ["withAbsoluteTolerance", 0.1]]),
    ("LowDiscrepancy", "a tolerance", [["withSeed", 1], ["withSteps", 1], ["withAbsoluteTolerance", 0.1]]),
    ("PseudoRandom", "no steps", [["withSeed", 1], ["withSamples", 100]]),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        process = PROCESSES[0][1]
        for times, paths in PATHS:
            for option_type in (CALL, PUT):
                strikes = [0.0, 95.0, 110.0]
                values = [v.attempt(lambda k=k: H.call("HestonHullWhitePathPricer::value", process, 1.0,
                                                       option_type, k, times, paths)) for k in strikes]
                out.append({"name": f"path pricer, {len(times) - 1} steps, {option_type}",
                            "inputs": {"settings": st, "kind": "path", "process": process, "exerciseTime": 1.0,
                                       "optionType": option_type, "strikes": strikes, "times": times,
                                       "paths": paths},
                            "expected": {"value": values}})

        def builder(name, traits, process, option_type, strike, setters, refused=False):
            try:
                r = H.call("MakeMCHestonHullWhiteEngine::results", traits, process, option_type, strike, R + 365,
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

        for pname, process, steps in PROCESSES:
            for traits, sname, setters in settings(steps):
                for option_type, strike in ((CALL, 105.0), (PUT, 95.0)):
                    builder(f"{pname}, {traits}, {sname}, {'call' if option_type == CALL else 'put'}", traits,
                            process, option_type, strike, setters)
        for traits, name, setters in REFUSED:
            builder(f"{traits}, refused: {name}", traits, PROCESSES[0][1], CALL, 100.0, setters, refused=True)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/mchestonhullwhiteengine.hpp", cases)
