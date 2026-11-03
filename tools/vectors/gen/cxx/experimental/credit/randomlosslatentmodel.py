# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's RandomLossLM (ql/experimental/credit/randomlosslatentmodel.hpp), which the wheel doesn't
expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/basket.cpp) on
tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs: Gaussian and Student t models over spot-recovery
latent models (the defaults' weights, then the recoveries'), 2,000 paths on QuantLib's Sobol sequence at its default
seed, 2863311530, on baskets of unequal notionals and hazards, curves dated at the evaluation date, at dates within the
horizon; model parameters A of 3 and 4, which keep every simulated recovery below 255.5/256, where QuantLib's 8 bits
wrap 256/256 to 0 (held in Divergences/RandomModelStatisticsTests.cs); the expected tranche loss, the percentile and
the shortfall, the event counts, the loss splits and distributions, and the recovery, which the model refuses. The
default correlation is left out, as the random default model's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/RandomLossLMCxxVectorTests.cs through
tests/NanoQL.Tests/Vectors/LossModelStatistics.cs."""
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
DATES = [TODAY + 365, TODAY + 1826, TODAY + 3653]

SEED = 2863311530


def model(copula, recoveries, default_weights, recovery_weights, model_a, t_orders=None):
    m = {"kind": "RandomLoss", "copula": copula, "integration": 0, "recoveries": list(recoveries),
         "factorWeights": [[w] for w in default_weights] + [[w] for w in recovery_weights], "modelA": model_a,
         "nSims": 2000, "accuracy": 1e-6, "seed": SEED}
    if t_orders is not None:
        m["tOrders"] = list(t_orders)
    return m


POOL = pf.flat_pool([0.03, 0.05, 0.02, 0.04])
CASES = [
    ("Gaussian, equity tranche", pf.basket(TODAY, [10, 20, 30, 40], POOL, 0.0, 0.2),
     model("Gaussian", [0.4, 0.35, 0.5, 0.3], [0.5, 0.4, 0.6, 0.3], [0.4, 0.5, 0.3, 0.6], 4.0), True),
    ("Gaussian, whole pool", pf.basket(TODAY, [10, 20, 30, 40], POOL),
     model("Gaussian", [0.4] * 4, [0.6] * 4, [0.6] * 4, 3.0), True),
    ("Student t, mezzanine", pf.basket(TODAY, [10, 20, 30, 40], POOL, 0.05, 0.3),
     model("T", [0.4, 0.35, 0.5, 0.3], [0.5, 0.4, 0.6, 0.3], [0.4, 0.5, 0.3, 0.6], 4.0, [5, 7]), True),
]
STATISTICS = {"levels": [0.0, 0.5, 0.9, 0.99], "shortfalls": [0.5, 0.9, 0.99], "events": True, "splits": [1.0, 5.0],
              "distribution": True, "refused": ["recoveryRate"]}


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, b, m, nth in CASES:
            with H.case_settings(TODAY.serialNumber()) as st:
                inputs, expected = pf.model_statistics(call, b, m, DATES, nth=nth, **STATISTICS)
                out.append({"name": name, "inputs": {"settings": st, **inputs}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/randomlosslatentmodel.hpp", cases)
