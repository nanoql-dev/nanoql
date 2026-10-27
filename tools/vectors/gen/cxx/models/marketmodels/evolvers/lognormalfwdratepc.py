# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LogNormalFwdRatePc (ql/models/marketmodels/evolvers/lognormalfwdratepc.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: whole evolutions, step by step.

FlatVol models (tools/cxx/src/models/marketmodels/marketmodelspec.hpp) on six semi-annual and five irregular rates, on
an exponential correlation, with the default evolution times and every other rate time, at full and reduced factors,
undisplaced and displaced 1% (full factors, terminal measure); on the Mersenne-twister generator (seed 42) and
Sobol's (diagonal ordering, seed 1234); under the terminal and money-market measures; from step 0, with the model's
initial state and a set one, and from step 1 (Mersenne twister, the model's state). Each of two paths: its weight and each step's weight, current step, the curve state's forwards, discount ratios
to the last bond and coterminal swap rates. Throws: incompatible numeraires.

The harness unit is tools/cxx/src/models/marketmodels/evolvers/evolvers.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/LogNormalFwdRatePcCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TYPE = "LogNormalFwdRatePc"
R = 46310
GRIDS = [
    ("six semi-annual rates", [0.5 * (i + 1) for i in range(7)]),
    ("five irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 3.1]),
]
GENERATORS = [("MT", {"type": "MT", "seed": 42}), ("Sobol", {"type": "Sobol", "ordering": 2, "seed": 1234})]
PATHS = 2


def model(times, evol, factors, d):
    n = len(times) - 1
    corr = {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0, "times": []}
    return {"type": "FlatVol", "vols": [0.22 - 0.01 * i for i in range(n)], "corr": corr, "rateTimes": times,
            "evolutionTimes": evol, "factors": factors, "initialRates": [0.035 + 0.003 * i for i in range(n)],
            "displacements": [d] * n}


def measures(times, evol):
    n = len(times) - 1
    terminal = [n] * len(evol)
    mm = []
    for t in evol:
        j = 0
        while times[j] < t:
            j += 1
        mm.append(j)
    return [("terminal", terminal), ("money market", mm)]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            fix = times[:-1]
            for elabel, evol in (("default evolution times", list(fix)), ("every other rate time", fix[1::2])):
                for factors in sorted({n, 2}):
                    if factors * len(evol) < n:
                        continue
                    for d in (0.0, 0.01):
                        m = model(times, evol, factors, d)
                        for glabel, g in GENERATORS:
                            for mlabel, numeraires in measures(times, evol):
                                for initial in (0, 1):
                                    for slabel, state in (("model state", None),
                                                          ("set state", [0.04 + 0.001 * i for i in range(n)])):
                                        if initial == 1 and (slabel == "set state" or glabel == "Sobol"):
                                            continue
                                        if d != 0.0 and (factors != n or mlabel != "terminal"):
                                            continue
                                        r = H.call("MarketModelEvolver.evolve", TYPE, m, g, numeraires, initial, PATHS,
                                                   state)
                                        out.append({"name": f"{label}, {elabel}, {factors} factors, displaced {d}, "
                                                            f"{glabel}, {mlabel}, from step {initial}, {slabel}",
                                                    "inputs": {"settings": st, "call": "evolve", "model": m,
                                                               "generator": g, "numeraires": numeraires,
                                                               "initialStep": initial, "paths": PATHS,
                                                               "initialForwards": state},
                                                    "expected": {
                                                        "numeraires": r["numeraires"],
                                                        "pathWeights": [p["weight"] for p in r["paths"]],
                                                        **{k: [[s[k] for s in p["steps"]] for p in r["paths"]]
                                                           for k in ("weight", "currentStep", "forwards",
                                                                     "discountRatios", "coterminalSwapRates")}}})
            m = model(times, list(fix), n, 0.0)
            bad = [("short numeraires", [n] * (len(fix) - 1)), ("an expired numeraire", [n] * (len(fix) - 2) + [0, n])]
            out.append({"name": f"{label}, refused numeraires",
                        "inputs": {"settings": st, "call": "refused", "model": m, "generator": GENERATORS[0][1],
                                   "numeraires": [b for _, b in bad]},
                        "expected": {"refused": [v.attempt(lambda b=b: H.call("MarketModelEvolver.evolve", TYPE, m,
                                                                              GENERATORS[0][1], b, 0, 1, None))
                                                 for _, b in bad]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolvers/lognormalfwdratepc.hpp", cases)
