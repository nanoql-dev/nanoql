# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BinomialVanillaEngine (ql/pricingengines/vanilla/binomialengine.hpp), on the evaluation date
15 October 2026: plain-vanilla options rolled back on each of the seven binomial trees.

European and American calls and puts at the money and away from it, on four processes (flat and linear zero curves,
constant and surface volatilities; the Merton and Black-Scholes processes, each on Act/365 throughout), on each tree
at two step counts: the NPV, delta, gamma and theta the engine reports; and Bermudan calls and puts, quarterly to a
year, on two of the processes and each tree at its larger count. The Leisen-Reimer and Joshi trees take odd
counts only: on an even count QuantLib rolls back a tree built for one more step, which nanoql fixes
(tests/NanoQL.Tests/Divergences/BinomialEvenStepsTests.cs); every structure shares the risk-free curve's day counter
and reference date, where nanoql's flattening by date departs from QuantLib's
(tests/NanoQL.Tests/Divergences/BinomialMismatchedTimesTests.cs). Then an expired option, whose results are zero, and
the refusals: one time step ("at least 2 time steps required, 1 provided") and a cash-or-nothing payoff
("non-plain payoff given").

A case holds one process, tree, step count, exercise and type, its outputs one per strike. The process specs are
tools/vectors/gen/processes/blackscholesprocess.py's; an exercise is {"type": "European" | "American", "date"} or
{"type": "Bermudan", "date" (the last), "dates"}. The
wheel's calls, by QuantLib-SWIG 1.44's options.i: BinomialVanillaEngine(process, type, timeSteps), the C++ engine's
template by its tree's name, on a VanillaOption; NPV, delta, gamma and theta the option's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/BinomialVanillaEngineVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
RESULTS = ["NPV", "delta", "gamma", "theta"]
TREES = [("jr", (50, 101)), ("crr", (50, 101)), ("eqp", (50, 101)), ("trigeorgis", (50, 101)), ("tian", (50, 101)),
         ("lr", (51, 101)), ("joshi4", (51, 101))]
STRIKES = [90.0, 100.0, 115.0]


def exercise(spec):
    if spec["type"] == "European":
        return ql.EuropeanExercise(ql.Date(spec["date"]))
    if spec["type"] == "Bermudan":
        return ql.BermudanExercise([ql.Date(d) for d in spec["dates"]])
    return ql.AmericanExercise(R, ql.Date(spec["date"]))


def results(process, tree, steps, payoff_spec, ex):
    option = ql.VanillaOption(bc.payoff(payoff_spec), exercise(ex))
    try:
        # QuantLib refuses a step count at construction
        option.setPricingEngine(ql.BinomialVanillaEngine(process, tree, steps))
        option.NPV()
    except RuntimeError:
        return {r: dict(v.THROWS) for r in RESULTS}
    return {r: v.result(lambda r=r: getattr(option, r)()) for r in RESULTS}


def cases():
    st = v.settings(R)
    out = []
    for i in (0, 1, 3, 5):
        pname, pspec = bsp.PROCESSES[i]
        process = bsp.process(pspec)
        for tree, counts in TREES:
            for steps in counts:
                for kind, days in (("European", 365), ("American", 365)):
                    ex = {"type": kind, "date": (R + days).serialNumber()}
                    for option_type in (CALL, PUT):
                        payoffs = [bc.payoff_spec("PlainVanilla", option_type, k) for k in STRIKES]
                        per = [results(process, tree, steps, p, ex) for p in payoffs]
                        out.append({
                            "name": f"{pname}, {tree} {steps}, {kind} {'call' if option_type == CALL else 'put'}",
                            "inputs": {"settings": st, "process": pspec, "tree": tree, "timeSteps": steps,
                                       "payoffs": payoffs, "exercise": ex},
                            "expected": {r: [x[r] for x in per] for r in RESULTS}})
    # Bermudan exercises, quarterly to a year, on each tree at its larger count
    dates = [(R + d).serialNumber() for d in (91, 182, 273, 365)]
    ex = {"type": "Bermudan", "date": dates[-1], "dates": dates}
    for i in (0, 3):
        pname, pspec = bsp.PROCESSES[i]
        process = bsp.process(pspec)
        for tree, counts in TREES:
            steps = counts[-1]
            for option_type in (CALL, PUT):
                payoffs = [bc.payoff_spec("PlainVanilla", option_type, k) for k in STRIKES]
                per = [results(process, tree, steps, p, ex) for p in payoffs]
                out.append({
                    "name": f"{pname}, {tree} {steps}, Bermudan {'call' if option_type == CALL else 'put'}",
                    "inputs": {"settings": st, "process": pspec, "tree": tree, "timeSteps": steps,
                               "payoffs": payoffs, "exercise": ex},
                    "expected": {r: [x[r] for x in per] for r in RESULTS}})
    pspec = bsp.PROCESSES[0][1]
    process = bsp.process(pspec)
    put = [bc.payoff_spec("PlainVanilla", PUT, 100.0)]
    for name, tree, steps, payoffs, ex in (
            ("expired", "crr", 51, put, {"type": "European", "date": (R - 1).serialNumber()}),
            ("one time step", "crr", 1, put, {"type": "European", "date": (R + 365).serialNumber()}),
            ("cash-or-nothing payoff", "crr", 51, [bc.payoff_spec("CashOrNothing", PUT, 100.0)],
             {"type": "European", "date": (R + 365).serialNumber()})):
        per = [results(process, tree, steps, p, ex) for p in payoffs]
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "tree": tree, "timeSteps": steps, "payoffs": payoffs,
                               "exercise": ex},
                    "expected": {r: [x[r] for x in per] for r in RESULTS}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/binomialengine.hpp", cases)
