# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PathwiseAccountingEngine, PathwiseVegasAccountingEngine and PathwiseVegasOuterAccountingEngine
(ql/models/marketmodels/pathwiseaccountingengine.hpp, .cpp), which the wheel doesn't expose, each output computed by
the C++ harness: the products' prices over a few paths of a log-normal Euler evolver in the money-market measure.

Products: caplets struck at the forwards, deflated caplets, two deflated caps (fewer products than rates) and the
deflated coterminal swaptions, on six semi-annual rates and five irregular ones, FlatVol 20%, displacements 0 and 2%;
the Mersenne twister and Sobol; the vega engines with one bump a step and with two.

Left out (nanoql's recursion, Jacobian, discount factors and delta layout;
tests/NanoQL.Tests/Divergences/PathwiseDeltaTests.cs, PathwiseVegaTests.cs): the deltas and vegas.

The harness unit is tools/cxx/src/models/marketmodels/pathwisegreeks/pathwisegreeks.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/PathwiseAccountingEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "products", "multistep"))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "products", "pathwise"))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import pathwiseproductcaplet as pc  # noqa: E402
import vectors as v  # noqa: E402

R = 46322
PATHS = 32
GENERATORS = [("MT", {"type": "MT", "seed": 42}), ("Sobol", {"type": "Sobol", "ordering": 2, "seed": 1234})]


def model(times, displacement=0.0, factors=3, vol=0.2, forwards=None):
    """FlatVol on an exponential correlation, stepping at each rate time but the last."""
    n = len(times) - 1
    corr = {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0, "times": []}
    return {"type": "FlatVol", "vols": [vol] * n, "corr": corr, "rateTimes": times, "evolutionTimes": times[:-1],
            "factors": factors, "initialRates": forwards or mf.forwards_of(times), "displacements": [displacement] * n}


def money_market(times):
    return list(range(len(times) - 1))


def products(times):
    f0 = mf.forwards_of(times)
    n = len(times) - 1
    return [("caplets", pc.caplet_spec("MarketModelPathwiseMultiCaplet", times, f0)),
            ("deflated caplets", pc.caplet_spec("MarketModelPathwiseMultiDeflatedCaplet", times, f0)),
            ("two caps", dict(pc.caplet_spec("MarketModelPathwiseMultiDeflatedCap", times, sum(f0) / n),
                              startsAndEnds=[[0, 2], [2, n]])),
            ("coterminal swaptions", {"type": "MarketModelPathwiseCoterminalSwaptionsDeflated", "rateTimes": times,
                                      "strikes": f0})]


def bumps(times, per_step):
    """per_step bumps a step: factor f of every alive rate, 1."""
    n = len(times) - 1
    out = []
    for step in range(n):
        out.append([[[1.0 if (k >= step and f == b) else 0.0 for f in range(3)] for k in range(n)]
                    for b in range(per_step)])
    return out


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for glabel, times in mf.GRIDS:
            for d in (0.0, 0.02):
                m = model(times, d)
                nums = money_market(times)
                for gl, g in GENERATORS:
                    for plabel, p in products(times):
                        name = f"{glabel}, displacement {d}, {gl}: {plabel}"
                        r = H.call("PathwiseAccountingEngine.values", m, g, nums, p, 0.95, PATHS)
                        out.append({"name": f"{name}, the delta engine",
                                    "inputs": {"settings": st, "engine": "PathwiseAccountingEngine", "model": m,
                                               "generator": g, "numeraires": nums, "product": p,
                                               "initialNumeraireValue": 0.95, "paths": PATHS},
                                    "expected": {"prices": v.enc(r["prices"]), "priceErrors": v.enc(r["priceErrors"])}})
                        for per_step in (1, 2):
                            b = bumps(times, per_step)
                            for engine, outer in (("PathwiseVegasAccountingEngine", False),
                                                  ("PathwiseVegasOuterAccountingEngine", True)):
                                r = H.call("PathwiseVegasAccountingEngine.values", m, g, nums, p, b, 0.95, PATHS, outer)
                                out.append({"name": f"{name}, {engine}, {per_step} bumps a step",
                                            "inputs": {"settings": st, "engine": engine, "model": m, "generator": g,
                                                       "numeraires": nums, "product": p, "bumps": b,
                                                       "initialNumeraireValue": 0.95, "paths": PATHS},
                                            "expected": {"prices": v.enc(r["prices"]),
                                                         "priceErrors": v.enc(r["priceErrors"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/pathwiseaccountingengine.hpp", cases)
