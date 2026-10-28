# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's RandomDefaultLM (ql/experimental/credit/randomdefaultlatentmodel.hpp), which the wheel
doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/basket.cpp) on
tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs: Gaussian and Student t models over constant-loss
latent models, 2,000 paths on QuantLib's Sobol sequence at its default seed, 2863311530, on baskets of unequal
notionals, hazards and recoveries, curves dated at the evaluation date (QuantLib counts the days from each curve's
date, the S-xcr plan's XR8 (x)), at dates within the horizon (XR8 (xliv)); the expected tranche loss, the percentile
and the shortfall at levels whose positions lie within the sorted losses (XR8 (xxix)), the event counts, the loss
splits and distributions, and each name's recovery, on both constructors; the n-th event's probabilities where no two names default on one
day (QuantLib's counts them once, held in Divergences/RandomModelStatisticsTests.cs: the whole pool's case has such days).
The default correlation divides the joint frequency by n - 1 (XR8 (xxiv)), so it is left out (held in
Divergences/RandomModelStatisticsTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/RandomDefaultLMCxxVectorTests.cs through
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


def model(copula, recoveries, correlation, t_orders=None):
    m = pf.constant_loss(copula, recoveries, correlation=correlation, t_orders=t_orders)
    m.update({"kind": "RandomDefault", "nSims": 2000, "accuracy": 1e-6, "seed": SEED})
    return m


def on_default(m):
    """The model on QuantLib's default latent model constructor, its recoveries given."""
    m["onDefaultModel"] = True
    return m


POOL = pf.flat_pool([0.03, 0.05, 0.02, 0.04, 0.06])
CASES = [
    ("Gaussian, equity tranche", pf.basket(TODAY, [10, 20, 30, 15, 25], POOL, 0.0, 0.2),
     model("Gaussian", [0.4, 0.35, 0.5, 0.3, 0.45], 0.3), True),
    ("Gaussian, whole pool", pf.basket(TODAY, [10, 20, 30, 15, 25], POOL), model("Gaussian", [0.4] * 5, 0.6), False),
    ("Student t, mezzanine", pf.basket(TODAY, [10, 20, 30, 15, 25], POOL, 0.05, 0.3),
     model("T", [0.4, 0.35, 0.5, 0.3, 0.45], 0.3, [5, 7]), True),
    ("Gaussian, the default model's constructor", pf.basket(TODAY, [10, 20, 30, 15, 25], POOL, 0.0, 0.2),
     on_default(model("Gaussian", [0.3, 0.5, 0.45, 0.4, 0.2], 0.3)), True),
    ("Student t, the default model's constructor", pf.basket(TODAY, [10, 20, 30, 15, 25], POOL, 0.05, 0.3),
     on_default(model("T", [0.3, 0.5, 0.45, 0.4, 0.2], 0.3, [5, 7])), True),
]
STATISTICS = {"levels": [0.0, 0.5, 0.9, 0.99], "shortfalls": [0.5, 0.9, 0.99], "events": True, "splits": [1.0, 5.0],
              "distribution": True, "recoveries": True}


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
    v.main(__file__, "ql/experimental/credit/randomdefaultlatentmodel.hpp", cases)
