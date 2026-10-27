# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiStepSwap (ql/models/marketmodels/products/multistep/multistepswap.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: the swap stepped on LMM curve states the harness sets
from each step's forwards, read per step (whether the path is finished, the count, and each cash flow's time index and
amount), with the product's counts, cash-flow times, evolution times and suggested numeraires; and its values through
the accounting engine over a few paths of the predictor-corrector evolver, on the Mersenne twister and on Sobol.

Rate grids: six semi-annual rates and five irregular ones; payer and receiver; fixed rates at, above and below the
forwards; fixed accruals off the rate taus and payment times off the rate times.

This file holds the shared helpers of the products S-mmp's second commit ports, which their generators import.

Left out (nanoql refuses them, QuantLib reading past its inputs; tests/NanoQL.Tests/Conventions/
MarketModelProductTests.cs): accruals or payment times not one a rate.

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiStepSwapCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import vectors as v  # noqa: E402

R = 46322
PATHS = 32
GRIDS = mf.GRIDS
GENERATORS = [("MT", {"type": "MT", "seed": 42}), ("Sobol", {"type": "Sobol", "ordering": 2, "seed": 1234})]


def swap_spec(kind, times, fixed_rate, fixed_accruals=None, floating_accruals=None, payment_times=None, payer=None):
    s = {"type": kind, "rateTimes": times, "fixedAccruals": fixed_accruals or mf.accruals_of(times),
         "floatingAccruals": floating_accruals or mf.accruals_of(times), "paymentTimes": payment_times or times[1:],
         "fixedRate": fixed_rate}
    if payer is not None:
        s["payer"] = payer
    return s


def discounts(times):
    d = [0.95]
    for f, a in zip(mf.forwards_of(times), mf.accruals_of(times)):
        d.append(d[-1] / (1.0 + f * a))
    return d


def model(times, evolution_times):
    """FlatVol at 20% on an exponential correlation, on the grid's initial forwards, stepping at the product's times:
    3 factors, or every rate's where 3 a step can't span the rates (FlatVol's check, a one-step product's)."""
    n = len(times) - 1
    factors = min(3, n) if 3 * len(evolution_times) >= n else n
    corr = {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0, "times": []}
    return {"type": "FlatVol", "vols": [0.2] * n, "corr": corr, "rateTimes": times, "evolutionTimes": evolution_times,
            "factors": factors, "initialRates": mf.forwards_of(times), "displacements": [0.0] * n}


def engine_cases(H, st, label, spec, times, evolution_times):
    """The product's values through the accounting engine, in the terminal measure, on each generator."""
    out = []
    n = len(times) - 1
    m = model(times, evolution_times)
    nums = [n] * len(evolution_times)
    initial = discounts(times)[n]
    for glabel, g in GENERATORS:
        r = H.call("AccountingEngine.values", "LogNormalFwdRatePc", m, g, nums, spec, initial, PATHS)
        out.append({"name": f"{label}, through the engine on {glabel}",
                    "inputs": {"settings": st, "evolver": "LogNormalFwdRatePc", "model": m, "generator": g,
                               "numeraires": nums, "product": spec, "initialNumeraireValue": initial,
                               "paths": PATHS},
                    "expected": {"samples": r["samples"], "weightSum": v.enc(r["weightSum"]),
                                 "mean": v.enc(r["mean"]), "variance": v.enc(r["variance"]),
                                 "errorEstimate": v.enc(r["errorEstimate"])}})
    return out


def steps_and_engine(H, st, rnd, label, spec, times, engine=True):
    """A product stepped on states drawn around the grid's forwards, and, where asked, through the engine."""
    n = len(times) - 1
    out = [mf.product_case(H, st, label, spec, mf.states(rnd, times, n))]
    if engine:
        evolution_times = H.call("MarketModelMultiProduct.steps", spec, [])["evolutionTimes"]
        out += engine_cases(H, st, label, spec, times, evolution_times)
    return out


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            f0 = mf.forwards_of(times)
            mid = sum(f0) / len(f0)
            for payer in (True, False):
                side = "payer" if payer else "receiver"
                out += steps_and_engine(H, st, rnd, f"{label}: {side} at the forwards' mean",
                                        swap_spec("MultiStepSwap", times, mid, payer=payer), times)
            out += steps_and_engine(H, st, rnd, f"{label}: payer 1% above",
                                    swap_spec("MultiStepSwap", times, mid + 0.01, payer=True), times, engine=False)
            out += steps_and_engine(H, st, rnd, f"{label}: receiver 1% below",
                                    swap_spec("MultiStepSwap", times, mid - 0.01, payer=False), times, engine=False)
            fixed = [a * 365.0 / 360.0 for a in mf.accruals_of(times)]
            pays = [t - 0.1 for t in times[1:]]
            out += steps_and_engine(H, st, rnd, f"{label}: own fixed accruals and payment times",
                                    swap_spec("MultiStepSwap", times, mid, fixed_accruals=fixed, payment_times=pays,
                                              payer=True), times, engine=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multistepswap.hpp", cases)
