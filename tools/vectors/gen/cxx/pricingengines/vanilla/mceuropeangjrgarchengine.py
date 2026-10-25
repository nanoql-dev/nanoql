# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EuropeanGJRGARCHPathPricer and MakeMCEuropeanGJRGARCHEngine
(ql/pricingengines/vanilla/mceuropeangjrgarchengine.hpp), which the wheel doesn't bind, each output computed by the C++
harness (docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026.

- The path pricer: calls and puts at three strikes (one of them 0) and two discounts on fixed two-asset paths (the
  spot and the daily variance) on grids of one, two and four steps, the payoff read on the first asset's last value;
  and the refusal of a negative strike.
- The builder: the NPV and error estimate (null under low-discrepancy numbers, which give none) of a European call and
  put on tools/vectors/gen/processes/gjrgarchprocess.py's two parameter sets on flat curves, under the setters in turn
  (steps or steps per year, samples or a tolerance with a maximum, antithetic variates, pseudo-random and
  low-discrepancy numbers), each on a fixed seed; then the builder's refusals, their messages QuantLib's: steps after
  steps per year and the reverse, samples after a tolerance and the reverse, a tolerance under low-discrepancy
  numbers, and no steps.

Left out (the S-hes plan's §2.10): structures off the process's reference date and day counter (HR7 (xviii)); a seed
of 0, QuantLib's seed from the clock, which nanoql refuses (the master plan's S2R4): every case sets its seed first.

A path case holds the "optionType", "strikes", "discount", "times" and "paths" (one list of values per asset, each of
the grid's size); a builder case the "traits", the "process" ({"referenceDate", "dayCounter", "riskFree", "dividend",
"s0", "v0", "omega", "alpha", "beta", "gamma", "lambda", "daysPerYear"}, flat forwards at the two rates), the
"optionType", "strike", "exerciseDate" and the "setters" ([name, value]). The harness's calls:
EuropeanGJRGARCHPathPricer::value and MakeMCEuropeanGJRGARCHEngine::results
(tools/cxx/src/pricingengines/vanilla/mceuropeangjrgarchengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/MCEuropeanGJRGARCHEngineCxxVectorTests.cs."""
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


mch = _load("cxx_mceuropeanhestonengine", "cxx/pricingengines/vanilla/mceuropeanhestonengine.py")
gp = _load("gjrgarchprocess", "processes/gjrgarchprocess.py")
R = mch.R
A365 = {"type": "Actual365Fixed"}
CALL, PUT = 1, -1
PATHS = [
    ([0.0, 1.0], [[50.0, 56.25], [2.0e-4, 2.6e-4]]),
    ([0.0, 0.5, 1.0], [[50.0, 46.6, 43.875], [2.0e-4, 3.1e-4, 0.0]]),
    ([0.0, 0.25, 0.5, 0.75, 1.0], [[50.0, 52.0, 49.75, 50.625, 50.0], [2.0e-4, 1.75e-4, 2.05e-4, 2.2e-4, 1.95e-4]]),
]


def process_spec(params):
    _, s0, v0, omega, alpha, beta, gamma, lam, days = params
    return {"referenceDate": R, "dayCounter": A365, "riskFree": 0.04, "dividend": 0.02, "s0": s0, "v0": v0,
            "omega": omega, "alpha": alpha, "beta": beta, "gamma": gamma, "lambda": lam, "daysPerYear": days}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for times, paths in PATHS:
            for option_type in (CALL, PUT):
                for discount in (1.0, 0.96):
                    strikes = [0.0, 47.5, 55.0]
                    values = [v.attempt(lambda k=k: H.call("EuropeanGJRGARCHPathPricer::value", option_type, k,
                                                           discount, times, paths)) for k in strikes]
                    out.append({"name": f"path pricer, {len(times) - 1} steps, {option_type}, discount {discount}",
                                "inputs": {"settings": st, "kind": "path", "optionType": option_type,
                                           "strikes": strikes, "discount": discount, "times": times,
                                           "paths": paths},
                                "expected": {"value": values}})
        out.append({"name": "path pricer, negative strike",
                    "inputs": {"settings": st, "kind": "path", "optionType": CALL, "strikes": [-1.0],
                               "discount": 1.0, "times": PATHS[0][0], "paths": PATHS[0][1]},
                    "expected": {"value": [v.attempt(lambda: H.call("EuropeanGJRGARCHPathPricer::value", CALL, -1.0,
                                                                    1.0, PATHS[0][0], PATHS[0][1]))]}})

        def builder(name, traits, process, option_type, strike, setters, refused=False):
            try:
                r = H.call("MakeMCEuropeanGJRGARCHEngine::results", traits, process, option_type, strike, R + 365,
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

        processes = [(params[0], process_spec(params)) for params in gp.PARAMETERS]
        for traits, sname, setters in mch.SETTINGS:
            for pname, process in processes:
                for option_type, strike in ((CALL, 52.5), (PUT, 47.5)):
                    builder(f"{traits}, {sname}, {pname}, {'call' if option_type == CALL else 'put'}", traits,
                            process, option_type, strike, setters)
        for traits, name, setters in mch.REFUSED:
            builder(f"{traits}, refused: {name}", traits, processes[0][1], CALL, 50.0, setters, refused=True)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/mceuropeangjrgarchengine.hpp", cases)
