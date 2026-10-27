# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LogNormalFwdRateIpc (ql/models/marketmodels/evolvers/lognormalfwdrateipc.hpp, .cpp), each
output computed by the C++ harness: whole evolutions, step by step. The wheel binds the evolver but no market model of
the harness's FlatVol, so its own vectors come with AbcdVol.

lognormalfwdratepc.py's FlatVol models (tools/cxx/src/models/marketmodels/marketmodelspec.hpp) on six semi-annual and
five irregular rates, on an exponential correlation, with the default evolution times and every other rate time, at
full and reduced factors, undisplaced and displaced 1% (full factors); on the Mersenne-twister generator (seed 42) and
Sobol's (diagonal ordering, seed 1234); under the terminal measure, the one the evolver takes; from step 0, with the
model's initial state and a set one, and from step 1 (Mersenne twister, the model's state). Each of two paths: its
weight and each step's weight, current step, the curve state's forwards, discount ratios to the last bond and
coterminal swap rates. Throws: the money-market measure, refused; incompatible numeraires.

evolutions() is the evolver generators' shared loop (lognormalfwdrateballand.py, lognormalfwdrateiballand.py,
normalfwdratepc.py, lognormalcotswapratepc.py, lognormalcmswapratepc.py). The harness unit is
tools/cxx/src/models/marketmodels/evolvers/evolvers.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/LogNormalFwdRateIpcCxxVectorTests.cs."""
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
R = pc.R
PATHS = pc.PATHS
STATE = "set state"


def default_evolutions(fix):
    return [("default evolution times", list(fix)), ("every other rate time", fix[1::2])]


def evolutions(H, st, evolver, keep, model=pc.model, evolution_sets=default_evolutions, displacements=(0.0, 0.01),
               extra=None):
    """Every case of lognormalfwdratepc.py's loop that keep(evolution label, factors, d, generator label, measure label,
    initial step, state label) accepts, on the harness's evolver type `evolver` (a name, or the constant-maturity
    evolver's object), the model built by model(times, evolution times, factors, d); `extra` the inputs added to each
    case."""
    out = []
    for label, times in pc.GRIDS:
        n = len(times) - 1
        fix = times[:-1]
        for elabel, evol in evolution_sets(fix):
            for factors in sorted({n, 2}):
                if factors * len(evol) < n:
                    continue
                for d in displacements:
                    if d != 0.0 and factors != n:
                        continue
                    m = model(times, evol, factors, d)
                    for glabel, g in pc.GENERATORS:
                        for mlabel, numeraires in pc.measures(times, evol):
                            for initial in (0, 1):
                                if initial >= len(evol):
                                    continue
                                for slabel, state in (("model state", None),
                                                      (STATE, [0.04 + 0.001 * i for i in range(n)])):
                                    if initial == 1 and (slabel == STATE or glabel == "Sobol"):
                                        continue
                                    if not keep(elabel, factors, d, glabel, mlabel, initial, slabel):
                                        continue
                                    r = H.call("MarketModelEvolver.evolve", evolver, m, g, numeraires, initial, PATHS,
                                               state)
                                    out.append({"name": f"{label}, {elabel}, {factors} factors, displaced {d}, "
                                                        f"{glabel}, {mlabel}, from step {initial}, {slabel}",
                                                "inputs": {"settings": st, "call": "evolve", "model": m,
                                                           "generator": g, "numeraires": numeraires,
                                                           "initialStep": initial, "paths": PATHS,
                                                           "initialForwards": state, **(extra or {})},
                                                "expected": {
                                                    "numeraires": r["numeraires"],
                                                    "pathWeights": [p["weight"] for p in r["paths"]],
                                                    **{k: [[s[k] for s in p["steps"]] for p in r["paths"]]
                                                       for k in ("weight", "currentStep", "forwards",
                                                                 "discountRatios", "coterminalSwapRates")}}})
    return out


def refused(H, st, evolver, sets, label, model=pc.model, extra=None):
    """One case per grid: each named numeraire set (sets(times, evolution times)) refused or accepted, as the
    evolver's constructor answers on the model of default evolution times."""
    out = []
    for glabel, times in pc.GRIDS:
        n = len(times) - 1
        fix = times[:-1]
        m = model(times, list(fix), n, 0.0)
        bad = sets(times, list(fix))
        g = pc.GENERATORS[0][1]
        out.append({"name": f"{glabel}, {label}",
                    "inputs": {"settings": st, "call": "refused", "model": m, "generator": g,
                               "numeraires": [b for _, b in bad], **(extra or {})},
                    "expected": {"refused": [v.attempt(lambda b=b: H.call("MarketModelEvolver.evolve", evolver, m, g, b,
                                                                          0, 1, None))
                                             for _, b in bad]}})
    return out


def incompatible(times, evol):
    n = len(times) - 1
    return [("short numeraires", [n] * (len(evol) - 1)), ("an expired numeraire", [n] * (len(evol) - 2) + [0, n])]


def cases():
    with cxx.connect() as H:
        st = H.settings(R)
        out = evolutions(H, st, "LogNormalFwdRateIpc", lambda e, f, d, g, mlabel, i, s: mlabel == "terminal")
        out += refused(H, st, "LogNormalFwdRateIpc",
                       lambda times, evol: [("the money-market measure", pc.measures(times, evol)[1][1])]
                       + incompatible(times, evol), "refused numeraires")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolvers/lognormalfwdrateipc.hpp", cases)
