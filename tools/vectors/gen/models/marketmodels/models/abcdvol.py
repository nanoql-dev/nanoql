# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AbcdVol (ql/models/marketmodels/models/abcdvol.hpp, .cpp) and the MarketModel base's reads:
the model's counts, initial rates and displacements, each step's pseudo-root, covariance and total covariance, and each
rate's volatility over the steps.

AbcdVol models on eight semi-annual and six irregular rates: abcd functions QuantLib's guess (-0.06, 0.17, 0.54, 0.17),
QuantLib's test's flat (0, 0, 1, 1, on each rate's own volatility as k) and its degenerate (1, 0, 1e-50, 0), each rate's
k rising; on an exponential correlation (exponent 1) with the default evolution times, every other rate time and times
off the rate times, at full and reduced factors and one, undisplaced, displaced 1% and on rising displacements; and on
an exponential correlation at exponent 0.5 on finer times. The specs are the C++ harness's
(tools/cxx/src/models/marketmodels/marketmodelspec.hpp), {"type": "AbcdVol", "abcd", "ks", "corr", "rateTimes",
"evolutionTimes", "factors", "initialRates", "displacements"}, the correlation's {"type": "Exponential", "rateTimes",
"longTermCorr", "beta", "gamma", "times"}. Throws: the sizes' checks, factors past the rates or zero.

Left out (nanoql refuses them, QuantLib reading past them;
tests/NanoQL.Tests/Divergences/MarketModelUndefinedReadsTests.cs): correlation times ending before the last evolution
time. The wheel's calls, by QuantLib-SWIG 1.44: ExponentialForwardCorrelation(rateTimes, longTermCorr, beta, gamma,
times), EvolutionDescription(rateTimes, evolutionTimes), AbcdVol(a, b, c, d, ks, corr, evolution, factors, initialRates,
displacements) and MarketModel's members. Read back by
tests/NanoQL.Tests/Vectors/Models/MarketModels/AbcdVolVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
GRIDS = [
    ("eight semi-annual rates", [0.5 * (i + 1) for i in range(9)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
]
ABCDS = [("QuantLib's guess", [-0.06, 0.17, 0.54, 0.17]), ("flat", [0.0, 0.0, 1.0, 1.0]),
         ("degenerate", [1.0, 0.0, 1e-50, 0.0])]


def rows(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def evolutions(times):
    fix = times[:-1]
    yield "default evolution times", list(fix)
    if len(fix) > 2:
        yield "every other rate time", fix[1::2] if fix[-1] in fix[1::2] else fix[1::2] + [fix[-1]]
    yield "times off the rate times", [t * 0.93 for t in fix]


def spec(abcd, times, corr, evol, factors, d):
    n = len(times) - 1
    ks = [0.18 + 0.01 * i for i in range(n)] if abcd == [0.0, 0.0, 1.0, 1.0] else [1.0 + 0.02 * i for i in range(n)]
    return {"type": "AbcdVol", "abcd": abcd, "ks": ks, "corr": corr, "rateTimes": times, "evolutionTimes": evol,
            "factors": factors, "initialRates": [0.03 + 0.002 * i for i in range(n)], "displacements": d}


def make(s):
    c = s["corr"]
    corr = ql.ExponentialForwardCorrelation(c["rateTimes"], c["longTermCorr"], c["beta"], c["gamma"], c["times"])
    evolution = ql.EvolutionDescription(s["rateTimes"], s["evolutionTimes"])
    a, b, cc, d = s["abcd"]
    return ql.AbcdVol(a, b, cc, d, s["ks"], corr, evolution, s["factors"], s["initialRates"], s["displacements"])


def reads(m):
    steps = m.numberOfSteps()
    return {"numberOfRates": m.numberOfRates(), "numberOfFactors": m.numberOfFactors(), "numberOfSteps": steps,
            "initialRates": list(m.initialRates()), "displacements": list(m.displacements()),
            "pseudoRoots": [rows(m.pseudoRoot(j)) for j in range(steps)],
            "covariances": [rows(m.covariance(j)) for j in range(steps)],
            "totalCovariances": [rows(m.totalCovariance(j)) for j in range(steps)],
            "timeDependentVolatilities": [list(m.timeDependentVolatility(i)) for i in range(m.numberOfRates())]}


def cases():
    st = v.settings(R)
    out = []
    for label, times in GRIDS:
        n = len(times) - 1
        fix = times[:-1]
        exp1 = {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0, "times": []}
        for alabel, abcd in ABCDS:
            for elabel, evol in evolutions(times):
                for factors in sorted({n, 2, 1}):
                    for dlabel, d in (("undisplaced", [0.0] * n), ("displaced 1%", [0.01] * n),
                                      ("rising displacements", [0.002 * (i + 1) for i in range(n)])):
                        if dlabel != "undisplaced" and (factors != n or elabel != "default evolution times"):
                            continue
                        s = spec(abcd, times, exp1, evol, factors, d)
                        out.append({"name": f"{label}, {alabel} abcd, {elabel}, {factors} factors, {dlabel}",
                                    "inputs": {"settings": st, "call": "model", "model": s},
                                    "expected": v.enc(reads(make(s)))})
        finer = sorted(set(fix) | {(a + b) / 2 for a, b in zip([0.0] + fix[:-1], fix)})
        exp05 = {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.3, "beta": 0.4, "gamma": 0.5,
                 "times": finer}
        s = spec(ABCDS[0][1], times, exp05, list(fix), n, [0.0] * n)
        out.append({"name": f"{label}, QuantLib's guess abcd, exponent 0.5 on finer times",
                    "inputs": {"settings": st, "call": "model", "model": s},
                    "expected": v.enc(reads(make(s)))})
        base = spec(ABCDS[0][1], times, exp1, list(fix), n, [0.0] * n)
        refusals = {"displacements": {**base, "displacements": [0.0] * (n + 1)},
                    "ks": {**base, "ks": base["ks"] + [1.0]},
                    "factorsPastRates": {**base, "factors": n + 1},
                    "noFactors": {**base, "factors": 0}}
        out.append({"name": f"{label}, refused models",
                    "inputs": {"settings": st, "call": "refused", "models": refusals},
                    "expected": {k: v.attempt(lambda m=m: make(m)) for k, m in refusals.items()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/abcdvol.hpp", cases)
