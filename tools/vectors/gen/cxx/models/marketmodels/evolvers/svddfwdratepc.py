# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SVDDFwdRatePc (ql/models/marketmodels/evolvers/svddfwdratepc.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness: whole evolutions, step by step.

lognormalfwdratepc.py's FlatVol models (six semi-annual and five irregular rates, an exponential correlation, the
default evolution times, full and reduced factors, undisplaced and displaced 1% at full factors) on a SquareRootAndersen
of squarerootandersen.py's kind: mean level 1, reversion speed 4000, its evolution times 1 then 2e-4 more a step, one
per model step, so its first step's e^(-k dt) underflows to 0, QuantLib's (the S-mm plan's MR3 (xvii)), and its later
steps' k dt are 0.8 over the sub-steps; vol of variance 25, v0 1.44, weights 0.5 and 0.5, cut point 1.5; one and two
sub-steps. The process draws from the first variate (0), the second (1) and past the forwards' factors (clamped to
them); the variate step is 1, which QuantLib doesn't read. On the Mersenne-twister generator (seed 42) and Sobol's
(diagonal ordering, seed 1234), the terminal and money-market measures, from step 0 (the one QuantLib supports) with
the model's initial state and a set one. Each of two paths: its weight and each step's weight, current step, the curve
state's forwards, discount ratios to the last bond and coterminal swap rates. Throws: incompatible numeraires.

The harness call is tools/cxx/src/models/marketmodels/evolvers/evolvers.cpp's "SVDDFwdRatePc.evolve". Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/SVDDFwdRatePcCxxVectorTests.cs."""
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


pc = _load("cxx_lognormalfwdratepc", "cxx/models/marketmodels/evolvers/lognormalfwdratepc.py")
STATE = [0.04 + 0.001 * i for i in range(6)]


def process(steps, sub):
    return {"meanLevel": 1.0, "reversionSpeed": 4000.0, "volVar": 25.0, "v0": 1.44,
            "evolutionTimes": [1.0 + 0.0002 * k for k in range(steps)], "numberSubSteps": sub, "w1": 0.5, "w2": 0.5,
            "cutPoint": 1.5}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(pc.R)
        for label, times in pc.GRIDS:
            n = len(times) - 1
            evol = times[:-1]
            for factors in sorted({n, 2}):
                for d in (0.0, 0.01):
                    if d != 0.0 and factors != n:
                        continue
                    m = pc.model(times, evol, factors, d)
                    for sub in (1, 2):
                        vp = process(len(evol), sub)
                        for first in (0, 1, factors + 3):
                            for glabel, g in pc.GENERATORS:
                                for mlabel, numeraires in pc.measures(times, evol):
                                    for slabel, state in (("model state", None), ("set state", STATE[:n])):
                                        if (first != 0 or d != 0.0) and (glabel != "MT" or slabel != "model state"):
                                            continue
                                        r = H.call("SVDDFwdRatePc.evolve", m, g, vp, first, 1, numeraires, pc.PATHS,
                                                   state)
                                        out.append({
                                            "name": f"{label}, {factors} factors, displaced {d}, {sub} sub-steps, "
                                                    f"first vol variate {first}, {glabel}, {mlabel}, {slabel}",
                                            "inputs": {"settings": st, "call": "evolve", "model": m, "generator": g,
                                                       "volProcess": vp, "firstVolatilityFactor": first,
                                                       "volatilityFactorStep": 1, "numeraires": numeraires,
                                                       "paths": pc.PATHS, "initialForwards": state},
                                            "expected": {
                                                "numeraires": r["numeraires"],
                                                "pathWeights": [p["weight"] for p in r["paths"]],
                                                **{k: [[s[k] for s in p["steps"]] for p in r["paths"]]
                                                   for k in ("weight", "currentStep", "forwards", "discountRatios",
                                                             "coterminalSwapRates")}}})
            m = pc.model(times, evol, n, 0.0)
            vp = process(len(evol), 1)
            g = pc.GENERATORS[0][1]
            bad = [("short numeraires", [n] * (len(evol) - 1)),
                   ("an expired numeraire", [n] * (len(evol) - 2) + [0, n])]
            out.append({"name": f"{label}, refused numeraires",
                        "inputs": {"settings": st, "call": "refused", "model": m, "generator": g, "volProcess": vp,
                                   "firstVolatilityFactor": 0, "volatilityFactorStep": 1,
                                   "numeraires": [b for _, b in bad]},
                        "expected": {"refused": [v.attempt(lambda b=b: H.call("SVDDFwdRatePc.evolve", m, g, vp, 0, 1, b,
                                                                              1, None))
                                                 for _, b in bad]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolvers/svddfwdratepc.hpp", cases)
