# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AccountingEngine (ql/models/marketmodels/accountingengine.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness: the engine's values over a few paths of an evolver, added to a
SequenceStatisticsInc, read through its samples, weight sum, means, variances and error estimates.

Forwards and optionlets in a multi-product composite (QuantLib's testStochVolForwardsAndOptionlets' product), a
single-product composite of them, forwards struck off the money alone and a cash rebate alone (its path finished at the
first step), on FlatVol over six
semi-annual rates (three factors) with the log-normal Euler and predictor-corrector evolvers and the normal one, on the
Mersenne twister and Sobol, in the terminal measure and in the next bond at each step (QuantLib's money-market
plus one).

Left out (nanoql's composites and discounter differ there; multiproductcomposite.py and discounter.py say where):
products paid strictly inside a period but the last, and composites whose parts finish before the last step.

The harness unit is tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/AccountingEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "products", "multistep"))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import vectors as v  # noqa: E402

R = 46322
PATHS = 32
TIMES = mf.GRIDS[0][1]
GENERATORS = [("MT", {"type": "MT", "seed": 42}), ("Sobol", {"type": "Sobol", "ordering": 2, "seed": 1234})]
EVOLVERS = ["LogNormalFwdRateEuler", "LogNormalFwdRatePc", "NormalFwdRatePc"]


def model(evolver):
    n = len(TIMES) - 1
    corr = {"type": "Exponential", "rateTimes": TIMES, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0, "times": []}
    vols = [0.2] * n if evolver != "NormalFwdRatePc" else [0.006] * n
    return {"type": "FlatVol", "vols": vols, "corr": corr, "rateTimes": TIMES, "evolutionTimes": TIMES[:-1],
            "factors": 3, "initialRates": mf.forwards_of(TIMES), "displacements": [0.0] * n}


def discounts():
    d = [0.95]
    for f, a in zip(mf.forwards_of(TIMES), mf.accruals_of(TIMES)):
        d.append(d[-1] / (1.0 + f * a))
    return d


def products():
    f0 = mf.forwards_of(TIMES)
    fwd = mf.forwards_spec(TIMES, [f + 0.01 for f in f0])
    opt = mf.optionlets_spec(TIMES, [[1, f] for f in f0])
    rebate = {"type": "MarketModelCashRebate", "rateTimes": TIMES, "evolutionTimes": TIMES[:1],
              "paymentTimes": [TIMES[2]], "amounts": [[0.01], [0.02]], "numberOfProducts": 2}
    return [("forwards and optionlets", {"type": "MultiProductComposite",
                                         "parts": [{"product": fwd, "multiplier": 1.0},
                                                   {"product": opt, "multiplier": 1.0}]}),
            ("one product of both", {"type": "SingleProductComposite",
                                     "parts": [{"product": fwd, "multiplier": 1.0},
                                               {"product": opt, "multiplier": -2.0}]}),
            ("forwards 50 bp below", mf.forwards_spec(TIMES, [f - 0.005 for f in f0])),
            ("a rebate", rebate)]


def measures(n_steps):
    n = len(TIMES) - 1
    return [("terminal", [n] * n_steps), ("the next bond", list(range(1, n_steps + 1)))]


def cases():
    out = []
    d = discounts()
    with cxx.connect() as H:
        st = H.settings(R)
        for evolver in EVOLVERS:
            m = model(evolver)
            for glabel, g in GENERATORS:
                for mlabel, numeraires in measures(len(TIMES) - 1):
                    for plabel, p in products():
                        m2, nums = m, numeraires
                        initial = d[nums[0]]
                        r = H.call("AccountingEngine.values", evolver, m2, g, nums, p, initial, PATHS)
                        out.append({"name": f"{evolver}, {glabel}, {mlabel}: {plabel}",
                                    "inputs": {"settings": st, "evolver": evolver, "model": m2, "generator": g,
                                               "numeraires": nums, "product": p, "initialNumeraireValue": initial,
                                               "paths": PATHS},
                                    "expected": {"samples": r["samples"], "weightSum": v.enc(r["weightSum"]),
                                                 "mean": v.enc(r["mean"]), "variance": v.enc(r["variance"]),
                                                 "errorEstimate": v.enc(r["errorEstimate"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/accountingengine.hpp", cases)
