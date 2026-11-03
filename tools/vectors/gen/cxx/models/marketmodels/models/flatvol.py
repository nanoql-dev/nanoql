# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FlatVol and FlatVolFactory (ql/models/marketmodels/models/flatvol.hpp, .cpp) and the
MarketModel base's reads (ql/models/marketmodels/marketmodel.hpp, .cpp), which the wheel doesn't expose, each output
computed by the C++ harness.

FlatVol models on eight semi-annual and six irregular rates and one rate: on an exponential correlation (exponent 1)
with the default evolution times, every other rate time and times off the rate times, at full and reduced factors,
undisplaced, displaced 1% and on rising displacements; on an exponential one at exponent 0.5 (on the default and on
finer times) and a time-homogeneous one, with the default evolution times, at full factors and one: the model's reads (its counts, initial rates and displacements, each step's pseudo-root,
covariance and total covariance, and each rate's volatility over the steps). FlatVolFactory on flat forward curves at
3% and 5% (Actual/365 Fixed), volatilities linear in two to four times, displacements 0 and 1%: the model it creates
and its reads. flatVolCovariance on intervals before, across and after the fixings. Throws: each size check, factors
past the rates or zero, too few factors for the steps, reversed bounds.

Left out (nanoql refuses them, QuantLib reading past them; tests/NanoQL.Tests/Divergences/MarketModelUndefinedReadsTests.cs):
correlation times ending before the last evolution time, a correlation of fewer rates than the model.

The harness unit is tools/cxx/src/models/marketmodels/models/flatvol.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/FlatVolCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
GRIDS = [
    ("eight semi-annual rates", [0.5 * (i + 1) for i in range(9)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
    ("one rate", [1.0, 2.0]),
]


def evolutions(times):
    fix = times[:-1]
    yield "default evolution times", list(fix)
    if len(fix) > 2:
        yield "every other rate time", fix[1::2] if fix[-1] in fix[1::2] else fix[1::2] + [fix[-1]]
    yield "times off the rate times", [t * 0.93 for t in fix]


def correlations(H, times):
    fix = times[:-1]
    finer = sorted(set(fix) | {(a + b) / 2 for a, b in zip([0.0] + fix[:-1], fix)})
    if len(fix) == 1:
        # QuantLib's exponential correlation refuses one rate
        yield "time-homogeneous", {"type": "TimeHomogeneous", "rateTimes": times, "fwdCorrelation": [[1.0]]}
        return
    yield "exponential", {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0,
                          "times": []}
    yield "exponential at 0.5", {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.3, "beta": 0.4,
                                 "gamma": 0.5, "times": []}
    yield "exponential at 0.5 on finer times", {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.3,
                                                "beta": 0.4, "gamma": 0.5, "times": finer}
    yield "time-homogeneous", {"type": "TimeHomogeneous", "rateTimes": times,
                               "fwdCorrelation": H.call("exponentialCorrelations", times, 0.2, 0.1, 1.0, 0.0)}


def flat_vol(times, corr, evol, factors, d):
    n = len(times) - 1
    return {"type": "FlatVol", "vols": [0.18 + 0.01 * i for i in range(n)], "corr": corr, "rateTimes": times,
            "evolutionTimes": evol, "factors": factors,
            "initialRates": [0.03 + 0.002 * i for i in range(n)], "displacements": d}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            for elabel, evol in evolutions(times):
                for clabel, corr in correlations(H, times):
                    for factors in sorted({n, min(2, n), 1}):
                        if factors * len(evol) < n:
                            continue
                        if clabel != next(correlations(H, times))[0] and (
                                elabel != "default evolution times" or factors not in (n, 1)):
                            continue
                        for dlabel, d in (("undisplaced", [0.0] * n), ("displaced 1%", [0.01] * n),
                                          ("rising displacements", [0.002 * (i + 1) for i in range(n)])):
                            if dlabel != "undisplaced" and (clabel not in ("exponential", "time-homogeneous")
                                                            or factors != n):
                                continue
                            m = flat_vol(times, corr, evol, factors, d)
                            out.append({"name": f"{label}, {elabel}, {clabel}, {factors} factors, {dlabel}",
                                        "inputs": {"settings": st, "call": "model", "model": m},
                                        "expected": v.enc(H.call("FlatVol", m))})
            base = flat_vol(times, next(correlations(H, times))[1], list(times[:-1]), n, [0.0] * n)
            refusals = {"rates": {**base, "initialRates": base["initialRates"][:-1]} if n > 1 else None,
                        "displacements": {**base, "displacements": [0.0] * (n + 1)},
                        "vols": {**base, "vols": base["vols"] + [0.2]},
                        "factorsPastRates": {**base, "factors": n + 1},
                        "noFactors": {**base, "factors": 0},
                        "tooFewFactors": {**base, "evolutionTimes": [times[-2]], "factors": 1} if n > 1 else None}
            out.append({"name": f"{label}, refused models",
                        "inputs": {"settings": st, "call": "refused",
                                   "models": {k: m for k, m in refusals.items() if m is not None}},
                        "expected": {k: v.attempt(lambda m=m: H.call("FlatVol", m))
                                     for k, m in refusals.items() if m is not None}})
        for rate, d in ((0.03, 0.0), (0.05, 0.01)):
            for vtimes, vols in (([0.0, 10.0], [0.2, 0.15]), ([0.0, 1.0, 3.0, 10.0], [0.25, 0.2, 0.18, 0.16])):
                for times, evol, factors in (([0.5 * (i + 1) for i in range(9)], [], 3),
                                             ([0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6], [0.8, 1.75, 4.0], 4)):
                    args = [0.5, 0.2, vtimes, vols, R, rate, {"type": "Actual365Fixed"}, d, times,
                            evol, factors]
                    out.append({"name": f"factory at {rate}, displaced {d}, {len(vtimes)} vol times, "
                                        f"{len(times) - 1} rates",
                                "inputs": {"settings": st, "call": "factory", "longTermCorrelation": 0.5,
                                           "beta": 0.2, "times": vtimes, "vols": vols, "referenceDate": R,
                                           "rate": rate, "displacement": d, "rateTimes": times,
                                           "evolutionTimes": evol, "factors": factors},
                                "expected": v.enc(H.call("FlatVolFactory.create", *args))})
        covs = []
        args = []
        for t1, t2 in ((0.0, 0.5), (0.5, 1.0), (0.25, 2.0), (1.5, 2.5), (3.0, 3.0)):
            for T, S in ((1.0, 2.0), (2.0, 1.0), (0.5, 0.5), (3.0, 4.0)):
                args.append([t1, t2, T, S, 0.2, 0.3])
                covs.append(H.call("flatVolCovariance", t1, t2, T, S, 0.2, 0.3))
        out.append({"name": "flatVolCovariance", "inputs": {"settings": st, "call": "covariance", "args": args},
                    "expected": {"covariances": v.enc(covs),
                                 "reversed": v.attempt(lambda: H.call("flatVolCovariance", 1.0, 0.5, 2.0, 2.0, 0.2,
                                                                      0.2))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/flatvol.hpp", cases)
