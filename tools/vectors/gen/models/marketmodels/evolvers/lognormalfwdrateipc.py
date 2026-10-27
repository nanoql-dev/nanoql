# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LogNormalFwdRateIpc (ql/models/marketmodels/evolvers/lognormalfwdrateipc.hpp, .cpp) on the
wheel's AbcdVol: whole evolutions, step by step (the C++ harness's on FlatVol are
tools/vectors/gen/cxx/models/marketmodels/evolvers/lognormalfwdrateipc.py).

AbcdVol models (abcdvol.py's specs) on six semi-annual and five irregular rates, QuantLib's guess for the abcd, an
exponential correlation, the default evolution times and every other rate time, at full and reduced factors, undisplaced
and displaced 1% (full factors); on the Mersenne-twister factory (seed 42) and Sobol's (diagonal ordering, seed 1234,
Jaeckel's direction integers); under the terminal measure; from step 0 with the model's state and a set one, and from
step 1 (Mersenne twister, the model's state). Each of two paths: its weight and each step's weight, current step, the
curve state's forwards, discount ratios to the last bond and coterminal swap rates. The wheel's calls, by QuantLib-SWIG
1.44: AbcdVol, MTBrownianGeneratorFactory(seed), SobolBrownianGeneratorFactory(ordering, seed, directionIntegers),
terminalMeasure, LogNormalFwdRateIpc(model, factory, numeraires, initialStep),
LMMCurveState(rateTimes).setOnForwardRates, and the evolver's setInitialState, startNewPath, advanceStep, currentStep
and currentState. Read back by tests/NanoQL.Tests/Vectors/Models/MarketModels/LogNormalFwdRateIpcVectorTests.cs."""
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


abcd = _load("abcdvol", "models/marketmodels/models/abcdvol.py")
R = abcd.R
GRIDS = [
    ("six semi-annual rates", [0.5 * (i + 1) for i in range(7)]),
    ("five irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 3.1]),
]
GENERATORS = [("MT", {"type": "MT", "seed": 42}), ("Sobol", {"type": "Sobol", "ordering": 2, "seed": 1234})]
PATHS = 2


def factory(g):
    if g["type"] == "MT":
        return ql.MTBrownianGeneratorFactory(g["seed"])
    return ql.SobolBrownianGeneratorFactory(g["ordering"], g["seed"], ql.SobolRsg.Jaeckel)


def evolve(m, g, initial, state):
    model = abcd.make(m)
    numeraires = list(ql.terminalMeasure(model.evolution()))
    e = ql.LogNormalFwdRateIpc(model, factory(g), numeraires, initial)
    if state is not None:
        cs = ql.LMMCurveState(m["rateTimes"])
        cs.setOnForwardRates(state)
        e.setInitialState(cs)
    steps = model.numberOfSteps() - initial
    path_weights = []
    reads = {k: [] for k in ("weight", "currentStep", "forwards", "discountRatios", "coterminalSwapRates")}
    for _ in range(PATHS):
        path_weights.append(e.startNewPath())
        path = {k: [] for k in reads}
        for _ in range(steps):
            w = e.advanceStep()
            cs = e.currentState()
            n = cs.numberOfRates()
            path["weight"].append(w)
            path["currentStep"].append(e.currentStep())
            path["forwards"].append(list(cs.forwardRates()))
            path["discountRatios"].append([cs.discountRatio(i, n) for i in range(n + 1)])
            path["coterminalSwapRates"].append(list(cs.coterminalSwapRates()))
        for k in reads:
            reads[k].append(path[k])
    return numeraires, path_weights, reads


def cases():
    st = v.settings(R)
    out = []
    for label, times in GRIDS:
        n = len(times) - 1
        fix = times[:-1]
        corr = {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0, "times": []}
        for elabel, evol in (("default evolution times", list(fix)), ("every other rate time", fix[1::2])):
            for factors in sorted({n, 2}):
                for d in (0.0, 0.01):
                    if d != 0.0 and factors != n:
                        continue
                    m = abcd.spec(abcd.ABCDS[0][1], times, corr, evol, factors, [d] * n)
                    for glabel, g in GENERATORS:
                        for initial in (0, 1):
                            for slabel, state in (("model state", None), ("set state", [0.04 + 0.001 * i for i in range(n)])):
                                if initial == 1 and (slabel == "set state" or glabel == "Sobol"):
                                    continue
                                numeraires, weights, reads = evolve(m, g, initial, state)
                                out.append({"name": f"{label}, {elabel}, {factors} factors, displaced {d}, {glabel}, "
                                                    f"from step {initial}, {slabel}",
                                            "inputs": {"settings": st, "call": "evolve", "model": m, "generator": g,
                                                       "numeraires": numeraires, "initialStep": initial,
                                                       "paths": PATHS, "initialForwards": state},
                                            "expected": {"numeraires": numeraires, "pathWeights": v.enc(weights),
                                                         **{k: v.enc(x) for k, x in reads.items()}}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolvers/lognormalfwdrateipc.hpp", cases)
