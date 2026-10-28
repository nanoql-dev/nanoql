# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Basket (ql/experimental/credit/basket.hpp, .cpp), which the wheel doesn't expose, each output
computed by the C++ harness (tools/cxx/src/experimental/credit/basket.cpp) on
tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs, the model set on the basket and the basket calculated
before any read (its first statistic recomputes its evaluation-date state after the tranche's amounts; before it,
QuantLib's remaining amounts read 0, the S-xcr plan's XR8 (xliii), held in Divergences/BasketConstructionOrderTests.cs).

Baskets under 31 August 2006 on constant-loss models, Gaussian and Student t, by correlation and by factor weights:
pools added in name order (XR8 (i)), no name defaulted by the evaluation date (a realized default restates the basket,
XR8 (ii), held in Divergences/BasketRealizedDefaultsTests.cs), equity, mezzanine and senior tranches, unequal
notionals and hazard rates, and "programmed" events after the evaluation date on the pool's last name, the one name
whose default leaves QuantLib's live reads by position on the right names. For each: the inspectors; at a sample of
dates the date reads (settled loss, remaining notional and notionals, names, size, live list, probabilities and the
live names' probabilities), the restated attachment, detachment and tranche notional only at dates before an event
settles (QuantLib's min(D, A + max(0, L - A)) and unamortized detachment differ from the header's max(0, A - L) once a
loss settles); each name's exposure; and the statistics the constant-loss model gives (the probability of n events or
more for every n, each pair's default correlation past the evaluation date, where QuantLib's divides 0 by 0, and each
name's recovery), and those it refuses (expected tranche loss, the tail and shortfall statistics, the loss distribution,
the n-th event probabilities). A basket on QuantLib's DefaultLossModel with every default kept (the harness's subclass),
each statistic the basket routes to the model refused. And the refused baskets (notionals not matching the pool, empty, invalid tranches, a
forward inception). Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/BasketCxxVectorTests.cs."""
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


pf = _load("cxx_defaultevent", "cxx/experimental/credit/defaultevent.py")

TODAY = pf.TODAY
GAUSS, T, QUAD, TRAP = "Gaussian", "T", 0, 1
EVENT_DATE = TODAY + 500
PROGRAMMED = {3: [pf.event("BankruptcyEvent", EVENT_DATE, "EUR", pf.SEC_DOM, EVENT_DATE + 20, recovery=0.25)]}
UNSETTLED = {3: [pf.event("BankruptcyEvent", EVENT_DATE, "EUR", pf.SEC_DOM)]}
FW = [[0.3, 0.2], [0.5, 0.1], [0.2, 0.6], [0.4, 0.4]]
BASKETS = [
    ("equity tranche, Gaussian by correlation",
     pf.basket(TODAY, [10, 20, 30, 40], pf.flat_pool([0.01, 0.02, 0.015, 0.03]), 0.0, 0.1),
     pf.constant_loss(GAUSS, [0.4, 0.35, 0.5, 0.3], correlation=0.3), None),
    ("mezzanine, Gaussian by two factors",
     pf.basket(TODAY, [25, 25, 25, 25], pf.flat_pool([0.02, 0.02, 0.04, 0.01]), 0.03, 0.07),
     pf.constant_loss(GAUSS, [0.4] * 4, factor_weights=FW), None),
    ("senior, Student t on one factor",
     pf.basket(TODAY, [10, 20, 30, 40], pf.flat_pool([0.01, 0.02, 0.015, 0.03]), 0.15, 1.0),
     pf.constant_loss(T, [0.4, 0.35, 0.5, 0.3], correlation=0.5, t_orders=[5, 5]), None),
    ("whole pool, Student t by weights",
     pf.basket(TODAY - 100, [1, 2, 3, 4], pf.flat_pool([0.05, 0.01, 0.02, 0.03], reference=TODAY - 100)),
     pf.constant_loss(T, [0.2, 0.4, 0.6, 0.0], factor_weights=[[0.3], [0.5], [0.2], [0.7]], t_orders=[3, 7]), None),
    ("a programmed default on the last name",
     pf.basket(TODAY, [10, 20, 30, 40], pf.flat_pool([0.01, 0.02, 0.015, 0.03], events=PROGRAMMED), 0.0, 0.5),
     pf.constant_loss(GAUSS, [0.4, 0.35, 0.5, 0.3], correlation=0.2), EVENT_DATE + 20),
    ("an unsettled programmed default", pf.basket(TODAY, [10, 20, 30, 40],
                                                 pf.flat_pool([0.01, 0.02, 0.015, 0.03], events=UNSETTLED), 0.0, 0.5),
     pf.constant_loss(GAUSS, [0.4] * 4, correlation=0.2), None),
]
REFUSED = [
    pf.basket(TODAY, [10, 20, 30], pf.flat_pool([0.01] * 4)),
    pf.basket(TODAY, [], pf.flat_pool([])),
    pf.basket(TODAY, [10] * 4, pf.flat_pool([0.01] * 4), 0.5, 0.4),
    pf.basket(TODAY, [10] * 4, pf.flat_pool([0.01] * 4), -0.1, 0.4),
    pf.basket(TODAY, [10] * 4, pf.flat_pool([0.01] * 4), 0.1, 1.1),
    pf.basket(TODAY + 1, [10] * 4, pf.flat_pool([0.01] * 4)),
]
DATES = [TODAY, TODAY + 1, TODAY + 365, TODAY + 1826, EVENT_DATE + 20, TODAY + 3653]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, b, m, settles in BASKETS:
            with H.case_settings(TODAY.serialNumber()) as st:
                n = len(b["notionals"])
                dated = [H.call("Basket::dated", b, m, d.serialNumber()) for d in DATES]
                keep = [settles is None or d < settles for d in DATES]
                expected = {"inspect": call("Basket::inspect", b, m)}
                for i, k in enumerate(["settledLoss", "cumulatedLoss", "remainingNotional", "remainingNotionals",
                                       "remainingNames", "remainingSize", "liveList", "remainingAttachmentAmount",
                                       "remainingDetachmentAmount", "remainingTrancheNotional", "probabilities",
                                       "remainingProbabilities"]):
                    if k in ("remainingAttachmentAmount", "remainingDetachmentAmount", "remainingTrancheNotional"):
                        expected[k] = [x[i] for x, ok in zip(dated, keep) if ok]
                    else:
                        expected[k] = [x[i] for x in dated]
                expected["exposure"] = [call("Basket::exposure", b, m, f"Name{i}", d.serialNumber())
                                        for i in range(n) for d in DATES[:2]]
                expected["probAtLeastNEvents"] = [call("Basket::probAtLeastNEvents", b, m, k, d.serialNumber())
                                                  for k in range(n + 2) for d in DATES]
                expected["defaultCorrelation"] = [call("Basket::defaultCorrelation", b, m, d.serialNumber(), i, j)
                                                  for d in DATES[1:4] for i in range(n) for j in range(n)]
                expected["recoveryRate"] = [call("Basket::recoveryRate", b, m, d.serialNumber(), i)
                                            for d in DATES[:3] for i in range(n)]
                d1 = (TODAY + 1826).serialNumber()
                expected["refused"] = [call("Basket::probsBeingNthEvent", b, m, 1, d1),
                                       call("Basket::expectedTrancheLoss", b, m, d1),
                                       call("Basket::probOverLoss", b, m, d1, 0.5),
                                       call("Basket::percentile", b, m, d1, 0.9),
                                       call("Basket::expectedShortfall", b, m, d1, 0.9),
                                       call("Basket::splitVaRLevel", b, m, d1, 10.0),
                                       call("Basket::lossDistribution", b, m, d1),
                                       call("Basket::dated", b, m, (TODAY - 1000).serialNumber())]
                out.append({"name": name,
                            "inputs": {"settings": st, "basket": b, "model": m,
                                       "dates": [d.serialNumber() for d in DATES], "restated": keep},
                            "expected": expected})
        with H.case_settings(TODAY.serialNumber()) as st:
            b = pf.basket(TODAY, [10, 20, 30, 40], pf.flat_pool([0.01, 0.02, 0.015, 0.03]), 0.0, 0.1)
            m = {"kind": "DefaultLossModel"}
            d1 = (TODAY + 1826).serialNumber()
            out.append({"name": "the base model's defaults", "inputs": {"settings": st, "basket": b, "model": m},
                        "expected": {"inspect": call("Basket::inspect", b, m),
                                     "defaults": [call("Basket::expectedTrancheLoss", b, m, d1),
                                                  call("Basket::probOverLoss", b, m, d1, 0.5),
                                                  call("Basket::percentile", b, m, d1, 0.9),
                                                  call("Basket::expectedShortfall", b, m, d1, 0.9),
                                                  call("Basket::splitVaRLevel", b, m, d1, 10.0),
                                                  call("Basket::lossDistribution", b, m, d1),
                                                  call("Basket::probsBeingNthEvent", b, m, 1, d1),
                                                  call("Basket::defaultCorrelation", b, m, d1, 0, 1),
                                                  call("Basket::probAtLeastNEvents", b, m, 1, d1),
                                                  call("Basket::recoveryRate", b, m, d1, 0)]}})
        with H.case_settings(TODAY.serialNumber()) as st:
            m = pf.constant_loss(GAUSS, [0.4] * 4, correlation=0.3)
            out.append({"name": "refused baskets", "inputs": {"settings": st, "baskets": REFUSED, "model": m},
                        "expected": {"inspect": [call("Basket::inspect", b, m) for b in REFUSED]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/basket.hpp", cases)
