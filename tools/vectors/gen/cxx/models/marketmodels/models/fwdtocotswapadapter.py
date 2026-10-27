# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FwdToCotSwapAdapter and FwdToCotSwapAdapterFactory
(ql/models/marketmodels/models/fwdtocotswapadapter.hpp, .cpp), which the wheel doesn't expose, each output computed by
the C++ harness.

The adapter on flatvol.py's FlatVol models and on AbcdVol models (marketmodelspec.hpp): eight semi-annual and six
irregular rates, an exponential correlation, the default evolution times and finer ones (each rate time among them),
full and reduced factors, undisplaced and displaced 1%: the adapter's reads (its counts, initial coterminal swap rates
and displacements, each step's pseudo-root, covariance and total covariance, and each rate's volatility over the steps).
The factory on flatvol.py's FlatVolFactory on flat forward curves at 3% and 5% (Actual/365 Fixed): the adapter it
creates and its reads. Throws: unequal displacements, an evolution skipping a rate time.

The harness unit is tools/cxx/src/models/marketmodels/models/adapters.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/FwdToCotSwapAdapterCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
NAME = "FwdToCotSwapAdapter"
GRIDS = [
    ("eight semi-annual rates", [0.5 * (i + 1) for i in range(9)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
]


def evolutions(times):
    fix = times[:-1]
    yield "default evolution times", list(fix)
    yield "finer times", sorted(set(fix) | {(a + b) / 2 for a, b in zip([0.0] + fix[:-1], fix)})


def model(kind, times, evol, factors, d):
    n = len(times) - 1
    corr = {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0, "times": []}
    m = {"type": kind, "corr": corr, "rateTimes": times, "evolutionTimes": evol, "factors": factors,
         "initialRates": [0.03 + 0.002 * i for i in range(n)], "displacements": d}
    if kind == "FlatVol":
        m["vols"] = [0.18 + 0.01 * i for i in range(n)]
    else:
        m["abcd"] = [-0.06, 0.17, 0.54, 0.17]
        m["ks"] = [1.0 + 0.02 * i for i in range(n)]
    return m


def cases(name=NAME):
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            for elabel, evol in evolutions(times):
                for kind in ("FlatVol", "AbcdVol"):
                    for factors in sorted({n, 2}):
                        for d in (0.0, 0.01):
                            if d != 0.0 and (factors != n or elabel != "default evolution times"):
                                continue
                            m = model(kind, times, evol, factors, [d] * n)
                            out.append({"name": f"{label}, {elabel}, {kind}, {factors} factors, displaced {d}",
                                        "inputs": {"settings": st, "call": "adapter", "model": m},
                                        "expected": v.enc(H.call(name, m))})
            base = model("FlatVol", times, list(times[:-1]), n, [0.0] * n)
            refusals = {"unequalDisplacements": {**base, "displacements": [0.01 * i for i in range(n)]},
                        "skippedRateTime": {**base, "evolutionTimes": times[:-1][1::2]}}
            out.append({"name": f"{label}, refused models",
                        "inputs": {"settings": st, "call": "refused", "models": refusals},
                        "expected": {k: v.attempt(lambda m=m: H.call(name, m)) for k, m in refusals.items()}})
        dc = {"type": "Actual365Fixed"}
        for rate, disp in ((0.03, 0.0), (0.05, 0.01)):
            for times, evol, factors in (([0.5 * (i + 1) for i in range(9)], [], 3),
                                         ([0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6], [], 6)):
                args = [0.5, 0.2, [0.0, 10.0], [0.2, 0.15], 46310, rate, dc, disp, times, evol, factors]
                out.append({"name": f"factory, flat {rate}, displacement {disp}, {len(times) - 1} rates",
                            "inputs": {"settings": st, "call": "factory", "longTermCorrelation": 0.5, "beta": 0.2,
                                       "times": [0.0, 10.0], "vols": [0.2, 0.15], "referenceDate": 46310,
                                       "rate": rate, "displacement": disp, "rateTimes": times,
                                       "evolutionTimes": evol, "factors": factors},
                            "expected": v.enc(H.call(name + "Factory.create", *args))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/fwdtocotswapadapter.hpp", cases)
