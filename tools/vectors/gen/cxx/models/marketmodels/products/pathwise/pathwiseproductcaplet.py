# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's pathwise caplets (ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.hpp, .cpp):
MarketModelPathwiseMultiCaplet, MarketModelPathwiseMultiDeflatedCaplet and MarketModelPathwiseMultiDeflatedCap,
which the wheel doesn't expose, each output computed by the C++ harness: the product stepped on LMM curve states the
harness sets from each step's forwards, read per step (whether the path is finished, each product's count, and each
cash flow's time index, amount and derivatives with respect to the forwards), with the product's counts, cash-flow
times, evolution times, suggested numeraires and whether its amounts are deflated.

Rate grids: six semi-annual rates and five irregular ones; strikes at, above and below the forwards; the deflated
caplet on its strikes and on its one strike; caps over the whole curve, inner spans and one caplet; the undeflated
caplet with accruals off the rate taus and payment times off the rate times.

This file holds the pathwise product vectors' shared helpers, which the other pathwise generators import.

Left out: the deflated caplets and caps with accruals other than the rate taus, where QuantLib's deflator derivative
reads the accrual (tests/NanoQL.Tests/Divergences/MarketModelPathwiseDeflatedCapletTests.cs); and, nanoql refusing
them, QuantLib reading past its inputs, accruals, payment times or strikes not one a rate
(tests/NanoQL.Tests/Conventions/MarketModelProductTests.cs).

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/PathwiseProductCapletCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "multistep"))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import vectors as v  # noqa: E402

R = 46322
GRIDS = mf.GRIDS


def pathwise_case(H, st, name, spec, forwards):
    """A pathwise product stepped on the forwards' states, its steps flattened as the multi-step products' are."""
    r = H.call("MarketModelPathwiseMultiProduct.steps", spec, forwards)
    steps = r["steps"]
    return {"name": name, "inputs": {"settings": st, "product": spec, "stepForwards": forwards},
            "expected": {"numberOfProducts": r["numberOfProducts"],
                         "maxNumberOfCashFlowsPerProductPerStep": r["maxNumberOfCashFlowsPerProductPerStep"],
                         "alreadyDeflated": r["alreadyDeflated"],
                         "possibleCashFlowTimes": r["possibleCashFlowTimes"], "evolutionTimes": r["evolutionTimes"],
                         "suggestedNumeraires": r["suggestedNumeraires"], "done": [s["done"] for s in steps],
                         "numberCashFlows": [s["numberCashFlows"] for s in steps],
                         "timeIndices": [[[f[0] for f in p] for p in s["cashFlows"]] for s in steps],
                         "amounts": [[[v.enc(f[1:]) for f in p] for p in s["cashFlows"]] for s in steps]}}


def stepped(H, st, rnd, label, spec, times):
    return [pathwise_case(H, st, label, spec, mf.states(rnd, times, len(times) - 1))]


def caplet_spec(kind, times, strikes, accruals=None, payment_times=None):
    s = {"type": kind, "rateTimes": times, "accruals": accruals or mf.accruals_of(times),
         "paymentTimes": payment_times or times[1:]}
    if isinstance(strikes, list):
        s["strikes"] = strikes
    else:
        s["strike"] = strikes
    return s


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            f0 = mf.forwards_of(times)
            mid = sum(f0) / n
            for kind in ("MarketModelPathwiseMultiCaplet", "MarketModelPathwiseMultiDeflatedCaplet"):
                short = "caplets" if kind.endswith("MultiCaplet") else "deflated caplets"
                out += stepped(H, st, rnd, f"{label}: {short} at the forwards", caplet_spec(kind, times, f0), times)
                out += stepped(H, st, rnd, f"{label}: {short} 50 bp below",
                               caplet_spec(kind, times, [f - 0.005 for f in f0]), times)
                out += stepped(H, st, rnd, f"{label}: {short} 1% above",
                               caplet_spec(kind, times, [f + 0.01 for f in f0]), times)
            out += stepped(H, st, rnd, f"{label}: deflated caplets on one strike",
                           caplet_spec("MarketModelPathwiseMultiDeflatedCaplet", times, mid), times)
            accruals = [a * 365.0 / 360.0 for a in mf.accruals_of(times)]
            pays = [t - 0.1 for t in times[1:]]
            out += stepped(H, st, rnd, f"{label}: caplets with own accruals and payment times",
                           caplet_spec("MarketModelPathwiseMultiCaplet", times, f0, accruals, pays), times)
            cap = caplet_spec("MarketModelPathwiseMultiDeflatedCap", times, mid)
            cap["startsAndEnds"] = [[0, n], [1, n - 1], [2, 3]]
            out += stepped(H, st, rnd, f"{label}: deflated caps over the curve, an inner span and one caplet", cap,
                           times)
            low = caplet_spec("MarketModelPathwiseMultiDeflatedCap", times, mid - 0.01)
            low["startsAndEnds"] = [[0, 2], [2, n]]
            out += stepped(H, st, rnd, f"{label}: deflated caps 1% in, two spans", low, times)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.hpp", cases)
