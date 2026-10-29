# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BinomialDoubleBarrierEngine (ql/experimental/barrieroption/binomialdoublebarrierengine.hpp)
on the Derman-Kani asset (discretizeddoublebarrieroption.hpp, .cpp), on the evaluation date 15 October 2026, through
QuantLib-SWIG's seven trees.

Four of tools/vectors/gen/processes/blackscholesprocess.py's processes, two corridors around the spot (80-120% and
70-140% of it), each knock-in and knock-out a call and a put at 90, 100 and 110% of the spot, European in a year and
American over it, on the Cox-Ross-Rubinstein, Jarrow-Rudd, additive EQP, Trigeorgis, Tian, Leisen-Reimer and Joshi
trees at 51 and 101 steps: the NPV, delta, gamma and theta. Then an expired option and a KIKO, which the asset refuses
("unsupported barrier type").

The outputs nanoql fixes are left out (the S-xeq plan's §2.8): every process shares one day counter and reference date,
where nanoql flattens a structure timed apart onto the tree's clock (XER5 (xi)); every count is odd, where nanoql's grid
and lattice take an even count's tree its own odd steps on the Leisen-Reimer and Joshi trees (XER5 (x), SEQR7 (x));
and an American is kept only where the grid's last time, maturity/steps × steps, isn't below the maturity, where
QuantLib 1.44 skips the exercise at expiry and nanoql takes #2878 (XER5 (xii)).

A case holds one process, exercise, double barrier and tree, its outputs one per payoff; the specs are the analytic
double-barrier generator's, and the tree is "crr", "jr", "eqp", "trigeorgis", "tian", "lr" or "j4" with its steps. The
wheel's calls, by QuantLib-SWIG 1.44's barrieroptions.i: DoubleBarrierOption(barrierType, barrierLo, barrierHi,
rebate, payoff, exercise), Binomial<tree>DoubleBarrierEngine(process, steps), each
BinomialDoubleBarrierEngine<T, DiscretizedDermanKaniDoubleBarrierOption>, and the option's NPV, delta, gamma and theta.
Read back by tests/NanoQL.Tests/Vectors/Experimental/BarrierOption/BinomialDoubleBarrierEngineVectorTests.cs."""
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


adbe = _load("analyticdoublebarrierengine", "pricingengines/barrier/analyticdoublebarrierengine.py")
abe, bsp = adbe.abe, adbe.bsp
R = adbe.R
PUT = adbe.PUT
KNOCK_OUT, KIKO = adbe.KNOCK_OUT, adbe.KIKO
TREES = [("crr", ql.BinomialCRRDoubleBarrierEngine), ("jr", ql.BinomialJRDoubleBarrierEngine),
         ("eqp", ql.BinomialEQPDoubleBarrierEngine), ("trigeorgis", ql.BinomialTrigeorgisDoubleBarrierEngine),
         ("tian", ql.BinomialTianDoubleBarrierEngine), ("lr", ql.BinomialLRDoubleBarrierEngine),
         ("j4", ql.BinomialJ4DoubleBarrierEngine)]
STEPS = [51, 101]
CORRIDORS = [(0.8, 1.2), (0.7, 1.4)]
OUTPUTS = ["NPV", "delta", "gamma", "theta"]


def results(barrier, payoff, ex, engine):
    o = adbe.option(barrier, payoff, ex)
    o.setPricingEngine(engine)
    return {k: v.attempt(getattr(o, k)) for k in OUTPUTS}


def grid_reaches_maturity(pspec, days, steps):
    # QuantLib's TimeGrid(maturity, steps): its last time dt × steps, dt = maturity/steps
    process = bsp.process(pspec)
    dc = process.riskFreeRate().dayCounter()
    reference = process.riskFreeRate().referenceDate()
    maturity = dc.yearFraction(reference, R + days)
    return (maturity / steps) * steps >= maturity


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, tree, cls, steps, specs):
        engine = cls(bsp.process(pspec), steps)
        values = [results(barrier, p, ex, engine) for p in specs]
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier, "tree": tree,
                               "steps": steps, "payoffs": specs},
                    "expected": {k: [r[k] for r in values] for k in OUTPUTS}})

    for pname, pspec in bsp.PROCESSES[:4]:
        x0 = pspec["x0"]
        for lo, hi in CORRIDORS:
            for tname, t in adbe.TYPES:
                barrier = adbe.barrier_spec(t, x0 * lo, x0 * hi)
                for kind in ("European", "American"):
                    ex = abe.exercise_spec(kind, 365)
                    for tree, cls in TREES:
                        for steps in STEPS:
                            if kind == "American" and not grid_reaches_maturity(pspec, 365, steps):
                                continue
                            add(f"{pname}, {tname} on [{lo:g}, {hi:g}] x spot, {kind}, {tree} {steps}", pspec, ex,
                                barrier, tree, cls, steps, abe.payoffs(x0))
    pspec = bsp.PROCESSES[0][1]
    add("expired", pspec, abe.exercise_spec("European", -1), adbe.barrier_spec(KNOCK_OUT, 80.0, 120.0), "crr",
        ql.BinomialCRRDoubleBarrierEngine, 51, abe.payoffs(100.0))
    add("a KIKO", pspec, abe.exercise_spec("European", 365), adbe.barrier_spec(KIKO, 80.0, 120.0), "crr",
        ql.BinomialCRRDoubleBarrierEngine, 51, abe.payoffs(100.0)[:1])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/barrieroption/binomialdoublebarrierengine.hpp", cases)
