# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCBarrierEngine (ql/pricingengines/barrier/mcbarrierengine.hpp, .cpp), on the evaluation date
15 October 2026: European barrier options by Monte Carlo on fixed seeds, priced by the Brownian-bridge pricer
(BarrierPathPricer, its crossing probabilities from a Mersenne Twister of its own) and by the biased pricer
(BiasedBarrierPathPricer, the barrier read at the grid's points only).

Each of the four barrier types at a barrier on each side of the spot (90% and 110% of it), with and without a rebate
(3% of the spot), over a year, on two processes of tools/vectors/gen/processes/blackscholesprocess.py (flat curves and
zero curves, each under a constant volatility), on 10 steps and 4,095 samples: pseudo-random paths at seeds 42 and 7,
the second with antithetic variates, and low-discrepancy paths at seed 5 on a Brownian bridge, each by both pricers, a
call and a put at the spot: the NPV and the error estimate (null on low-discrepancy paths, which give none). Then an
expired option and the refusals: a spot beyond the barrier ("barrier touched") and a cash-or-nothing payoff.

The outputs nanoql fixes are left out: every process here has one day counter and reference date, so the grid is
QuantLib's (tests/NanoQL.Tests/Divergences/McBarrierMismatchedTimesTests.cs); and no pseudo-random run is at seed 5,
where nanoql's bridge reads MT(6) (McBarrierSeed5Tests.cs).

A case holds one process, exercise and barrier (tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's
specs) and a Monte Carlo spec {"traits" ("pseudorandom" or "lowdiscrepancy"), "timeSteps", "brownianBridge",
"antitheticVariate", "requiredSamples", "isBiased", "seed"}, its outputs one per payoff. The wheel's calls, by
QuantLib-SWIG 1.44's options.i: BarrierOption(barrierType, barrier, rebate, payoff, exercise), MCBarrierEngine(process,
traits, timeSteps, brownianBridge, antitheticVariate, requiredSamples, isBiased, seed) and the option's NPV and error
estimate. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/Barrier/MCBarrierEngineVectorTests.cs."""
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


abe = _load("analyticbarrierengine", "pricingengines/barrier/analyticbarrierengine.py")
bsp, bc = abe.bsp, abe.bc
R = abe.R
CALL, PUT = abe.CALL, abe.PUT
PROCESSES = [bsp.PROCESSES[0], bsp.PROCESSES[1]]
RUNS = [("pseudo-random, seed 42", "pseudorandom", False, False, 42),
        ("pseudo-random, seed 7, antithetic", "pseudorandom", False, True, 7),
        ("low-discrepancy, seed 5, Brownian bridge", "lowdiscrepancy", True, False, 5)]


def mc_spec(traits, bridge, antithetic, seed, biased, steps=10, samples=4095):
    return {"traits": traits, "timeSteps": steps, "brownianBridge": bridge, "antitheticVariate": antithetic,
            "requiredSamples": samples, "isBiased": biased, "seed": seed}


def engine(process, mc):
    return ql.MCBarrierEngine(process, mc["traits"], timeSteps=mc["timeSteps"], brownianBridge=mc["brownianBridge"],
                              antitheticVariate=mc["antitheticVariate"], requiredSamples=mc["requiredSamples"],
                              isBiased=mc["isBiased"], seed=mc["seed"])


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, mc, specs):
        e = engine(bsp.process(pspec), mc)
        options = []
        for spec in specs:
            o = abe.option(barrier, spec, ex)
            o.setPricingEngine(e)
            options.append(o)
        expected = {"NPV": [v.attempt(o.NPV) for o in options]}
        if all(isinstance(x, float) for x in expected["NPV"]):
            expected["errorEstimate"] = [v.result(o.errorEstimate) for o in options]
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier, "mc": mc,
                               "payoffs": specs},
                    "expected": expected})

    for pname, pspec in PROCESSES:
        x0 = pspec["x0"]
        ex = abe.exercise_spec("European", 365)
        specs = [bc.payoff_spec("PlainVanilla", t, x0) for t in (CALL, PUT)]
        for tname, t in abe.TYPES:
            level = x0 * (0.9 if t in (abe.DOWN_IN, abe.DOWN_OUT) else 1.1)
            for rebate in (0.0, 0.03 * x0):
                for rname, traits, bridge, antithetic, seed in RUNS:
                    for biased in (False, True):
                        pricer = "biased" if biased else "bridge"
                        add(f"{pname}, {tname} at {level:g}, rebate {rebate:g}, {rname}, {pricer} pricer", pspec, ex,
                            abe.barrier_spec(t, level, rebate), mc_spec(traits, bridge, antithetic, seed, biased), specs)
    pspec = bsp.PROCESSES[0][1]
    ex = abe.exercise_spec("European", 365)
    mc = mc_spec("pseudorandom", False, False, 42, False)
    add("expired", pspec, abe.exercise_spec("European", -1), abe.barrier_spec(abe.DOWN_OUT, 90.0, 3.0), mc,
        abe.payoffs(100.0))
    for tname, t in abe.TYPES:
        level = 105.0 if t in (abe.DOWN_IN, abe.DOWN_OUT) else 95.0
        add(f"{tname}, the spot beyond the barrier", pspec, ex, abe.barrier_spec(t, level, 3.0), mc,
            abe.payoffs(100.0)[:1])
    add("a cash-or-nothing payoff", pspec, ex, abe.barrier_spec(abe.DOWN_OUT, 90.0, 0.0), mc,
        [bc.payoff_spec("CashOrNothing", CALL, 100.0)])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/mcbarrierengine.hpp", cases)
