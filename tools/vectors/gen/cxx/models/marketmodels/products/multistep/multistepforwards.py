# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiStepForwards (ql/models/marketmodels/products/multistep/multistepforwards.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the forwards stepped on LMM curve states the
harness sets from each step's forwards, read per step (whether the path is finished, each product's count, and each
cash flow's time index and amount), with the product's counts, cash-flow times, evolution times and suggested
numeraires.

Rate grids: six semi-annual rates and five irregular ones; strikes at, above and below the forwards, fewer strikes
than rates, accruals off the rate taus and payment times off the rate times; each grid's states drawn around its
initial forwards.

This file holds the product vectors' shared helpers, which the other product generators import (multistepoptionlets,
multistepnothing, cashrebate, multiproductcomposite, singleproductcomposite, discounter, accountingengine).

Left out (nanoql refuses them, QuantLib reading past its inputs; tests/NanoQL.Tests/Conventions/
MarketModelProductTests.cs): no strike, more strikes than rates, fewer accruals or payment times than strikes.

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiStepForwardsCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46322
GRIDS = [
    ("six semi-annual rates", [0.5 * (i + 1) for i in range(7)]),
    ("five irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 3.1]),
]


def forwards_of(times):
    return [0.03 + 0.002 * i for i in range(len(times) - 1)]


def accruals_of(times):
    return [times[i + 1] - times[i] for i in range(len(times) - 1)]


def states(rnd, times, steps):
    """Each step's forwards, drawn around the grid's initial forwards."""
    f0 = forwards_of(times)
    return [[f * (1.0 + rnd.gauss(0.0, 0.15)) for f in f0] for _ in range(steps)]


def forwards_spec(times, strikes, accruals=None, payment_times=None):
    return {"type": "MultiStepForwards", "rateTimes": times, "accruals": accruals or accruals_of(times),
            "paymentTimes": payment_times or times[1:], "strikes": strikes}


def optionlets_spec(times, payoffs, accruals=None, payment_times=None):
    return {"type": "MultiStepOptionlets", "rateTimes": times, "accruals": accruals or accruals_of(times),
            "paymentTimes": payment_times or times[1:], "payoffs": payoffs}


def step_outputs(r):
    """A product's steps flattened: per step its done flag, counts, time indices and amounts."""
    steps = r["steps"]
    return {"numberOfProducts": r["numberOfProducts"],
            "maxNumberOfCashFlowsPerProductPerStep": r["maxNumberOfCashFlowsPerProductPerStep"],
            "possibleCashFlowTimes": r["possibleCashFlowTimes"], "evolutionTimes": r["evolutionTimes"],
            "suggestedNumeraires": r["suggestedNumeraires"], "done": [s["done"] for s in steps],
            "numberCashFlows": [s["numberCashFlows"] for s in steps],
            "timeIndices": [[[f[0] for f in p] for p in s["cashFlows"]] for s in steps],
            "amounts": [[[v.enc(f[1]) for f in p] for p in s["cashFlows"]] for s in steps]}


def product_case(H, st, name, spec, forwards):
    r = H.call("MarketModelMultiProduct.steps", spec, forwards)
    return {"name": name, "inputs": {"settings": st, "product": spec, "stepForwards": forwards},
            "expected": step_outputs(r)}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            f0 = forwards_of(times)
            fs = states(rnd, times, n)
            out.append(product_case(H, st, f"{label}: at the forwards", forwards_spec(times, f0), fs))
            out.append(product_case(H, st, f"{label}: 50 bp above", forwards_spec(times, [f + 0.005 for f in f0]), fs))
            out.append(product_case(H, st, f"{label}: 1% below", forwards_spec(times, [f - 0.01 for f in f0]), fs))
            out.append(product_case(H, st, f"{label}: two strikes", forwards_spec(times, f0[:2]), fs))
            accruals = [a * 365.0 / 360.0 for a in accruals_of(times)]
            pays = [t - 0.1 for t in times[1:]]
            out.append(product_case(H, st, f"{label}: own accruals and payment times",
                                    forwards_spec(times, f0, accruals, pays), fs))
        out.append(product_case(H, st, "one rate", forwards_spec([0.5, 1.0], [0.03]), [[0.031]]))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multistepforwards.hpp", cases)
