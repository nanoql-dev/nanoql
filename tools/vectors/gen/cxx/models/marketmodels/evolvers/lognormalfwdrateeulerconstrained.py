# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LogNormalFwdRateEulerConstrained
(ql/models/marketmodels/evolvers/lognormalfwdrateeulerconstrained.hpp, .cpp), which the wheel doesn't expose, each
output computed by the C++ harness: whole evolutions with constraints, step by step.

QuantLib's setConstraintType builds every step's covariances from the pseudo-root of the step it is called at, and its
setThisConstraint displaces each step's constraint by the displacement at the step's index. The harness calls
setConstraintType again before each step, so each step's covariances are its own pseudo-root's, nanoql's fix, and
every model's displacements are equal, so the step's displacement is the constrained rate's. On
lognormalfwdratepc.py's grids and FlatVol models (six semi-annual and five irregular rates, an exponential
correlation, the default evolution times and every other rate time, full and reduced factors, undisplaced and
displaced 1% (full factors, terminal measure)), the Mersenne-twister generator (seed 42) and Sobol's (diagonal
ordering, seed 1234), the terminal and money-market measures, from step 0 with the model's initial state and a set one
and from step 1 (Mersenne twister, the model's state): each step's first alive rate constrained to its initial value
plus 0.2%, at every step; and on the terminal measure and the Mersenne twister, the last rate constrained to its
initial value less 0.3%, at even steps only. Each of two paths: its weight and each step's weight, current step, the
curve state's forwards, discount ratios to the last bond and coterminal swap rates.

The harness call is tools/cxx/src/models/marketmodels/evolvers/evolvers.cpp's
"LogNormalFwdRateEulerConstrained.evolve". Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/LogNormalFwdRateEulerConstrainedCxxVectorTests.cs."""
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


def alive(times, evol):
    out = []
    for t in evol:
        j = 0
        while times[j] < t:
            j += 1
        out.append(j)
    return out


def constraints(times, evol, m, variant):
    n = len(times) - 1
    rates = m["initialRates"]
    if variant == "first alive rate, every step":
        start = alive(times, evol)
        return start, [rates[k] + 0.002 for k in start], [True] * len(evol)
    start = [n - 1] * len(evol)
    return start, [rates[n - 1] - 0.003] * len(evol), [s % 2 == 0 for s in range(len(evol))]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(pc.R)
        for label, times in pc.GRIDS:
            n = len(times) - 1
            fix = times[:-1]
            for elabel, evol in (("default evolution times", list(fix)), ("every other rate time", fix[1::2])):
                for factors in sorted({n, 2}):
                    if factors * len(evol) < n:
                        continue
                    for d in (0.0, 0.01):
                        m = pc.model(times, evol, factors, d)
                        for glabel, g in pc.GENERATORS:
                            for mlabel, numeraires in pc.measures(times, evol):
                                for initial in (0, 1):
                                    for slabel, state in (("model state", None), ("set state", STATE[:n])):
                                        if initial == 1 and (slabel == "set state" or glabel == "Sobol"):
                                            continue
                                        if d != 0.0 and (factors != n or mlabel != "terminal"):
                                            continue
                                        for variant in ("first alive rate, every step", "last rate, even steps"):
                                            if variant != "first alive rate, every step" and (
                                                    mlabel != "terminal" or glabel != "MT"):
                                                continue
                                            start, rc, active = constraints(times, evol, m, variant)
                                            r = H.call("LogNormalFwdRateEulerConstrained.evolve", m, g, numeraires,
                                                       initial, pc.PATHS, state, start, rc, active)
                                            out.append({
                                                "name": f"{label}, {elabel}, {factors} factors, displaced {d}, "
                                                        f"{glabel}, {mlabel}, from step {initial}, {slabel}, "
                                                        f"{variant}",
                                                "inputs": {"settings": st, "call": "evolve", "model": m,
                                                           "generator": g, "numeraires": numeraires,
                                                           "initialStep": initial,
                                                           "paths": pc.PATHS, "initialForwards": state,
                                                           "constrainedRates": start, "rateConstraints": rc,
                                                           "isConstraintActive": active},
                                                "expected": {
                                                    "numeraires": r["numeraires"],
                                                    "pathWeights": [p["weight"] for p in r["paths"]],
                                                    **{k: [[s[k] for s in p["steps"]] for p in r["paths"]]
                                                       for k in ("weight", "currentStep", "forwards",
                                                                 "discountRatios", "coterminalSwapRates")}}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolvers/lognormalfwdrateeulerconstrained.hpp", cases)
