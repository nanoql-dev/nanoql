# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BinomialBarrierEngine (ql/pricingengines/barrier/binomialbarrierengine.hpp), on the
evaluation date 15 October 2026: European barrier options on the seven binomial trees, each with Derman and Kani's
barrier adjustment (DiscretizedDermanKaniBarrierOption, the asset QuantLib-SWIG 1.44 binds), Cox-Ross-Rubinstein's
steps raised to Boyle and Lau's optimum.

Each of the four barrier types at a barrier on each side of the spot (90% and 110% of it), with and without a rebate
(3% of the spot), over a year, on four processes of tools/vectors/gen/processes/blackscholesprocess.py (flat curves
under a constant volatility, zero curves under a constant volatility, a variance curve and a surface), on the
Cox-Ross-Rubinstein, Jarrow-Rudd, additive EQP, Trigeorgis, Tian, Leisen-Reimer and Joshi trees at 51 steps (101 for
two of them), each a call and a put at three strikes (90, 100 and 110% of the spot): the NPV, delta, gamma and theta.
Then an expired option and the refusals: a spot beyond the barrier ("barrier touched") and a non-positive strike.

The outputs nanoql fixes are left out: every process here has one day counter and reference date, so the tree is
QuantLib's (tests/NanoQL.Tests/Divergences/BinomialBarrierMismatchedTimesTests.cs); every step count is odd, so the
Leisen-Reimer and Joshi trees, which take odd steps, build the grid QuantLib builds; and every exercise is European,
clear of #2878's American window (BarrierUpstreamFixesTests.cs).

A case holds one process, exercise, barrier, tree and step count, its outputs one per payoff, the specs
tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's, the tree its name. The wheel's calls, by
QuantLib-SWIG 1.44's options.i: BarrierOption(barrierType, barrier, rebate, payoff, exercise),
BinomialBarrierEngine(process, tree, steps) and the option's NPV, delta, gamma and theta. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Barrier/BinomialBarrierEngineVectorTests.cs."""
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
TREES = [("crr", 51), ("jr", 51), ("eqp", 51), ("trigeorgis", 101), ("tian", 51), ("lr", 101), ("j4", 51)]
PROCESSES = [bsp.PROCESSES[i] for i in (0, 1, 2, 3)]
OUTPUTS = ["NPV", "delta", "gamma", "theta"]


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, tree, steps, specs):
        engine = ql.BinomialBarrierEngine(bsp.process(pspec), tree, steps)
        options = []
        for spec in specs:
            o = abe.option(barrier, spec, ex)
            o.setPricingEngine(engine)
            options.append(o)
        expected = {"NPV": [v.attempt(o.NPV) for o in options]}
        if all(isinstance(x, float) for x in expected["NPV"]):
            for g in OUTPUTS[1:]:
                expected[g] = [v.result(getattr(o, g)) for o in options]
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier, "tree": tree,
                               "steps": steps, "payoffs": specs},
                    "expected": expected})

    for pname, pspec in PROCESSES:
        x0 = pspec["x0"]
        ex = abe.exercise_spec("European", 365)
        for tname, t in abe.TYPES:
            level = x0 * (0.9 if t in (abe.DOWN_IN, abe.DOWN_OUT) else 1.1)
            for rebate in (0.0, 0.03 * x0):
                for tree, steps in TREES:
                    add(f"{pname}, {tname} at {level:g}, rebate {rebate:g}, {tree} at {steps} steps", pspec, ex,
                        abe.barrier_spec(t, level, rebate), tree, steps, abe.payoffs(x0))
    pspec = bsp.PROCESSES[0][1]
    ex = abe.exercise_spec("European", 365)
    add("expired", pspec, abe.exercise_spec("European", -1), abe.barrier_spec(abe.DOWN_OUT, 90.0, 3.0), "crr", 51,
        abe.payoffs(100.0))
    for tname, t in abe.TYPES:
        level = 105.0 if t in (abe.DOWN_IN, abe.DOWN_OUT) else 95.0
        add(f"{tname}, the spot beyond the barrier", pspec, ex, abe.barrier_spec(t, level, 3.0), "jr", 51,
            abe.payoffs(100.0)[:1])
    add("a strike of 0", pspec, ex, abe.barrier_spec(abe.DOWN_OUT, 90.0, 0.0), "jr", 51,
        [bc.payoff_spec("PlainVanilla", CALL, 0.0)])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/binomialbarrierengine.hpp", cases)
