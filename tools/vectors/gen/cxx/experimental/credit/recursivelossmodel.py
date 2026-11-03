# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's RecursiveLossModel (ql/experimental/credit/recursivelossmodel.hpp), which the wheel
doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/basket.cpp and
recursivelossmodel.cpp) on tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs: Gaussian and Student t
models on equal weights, weights of 1 to 3 units and weights with gaps; the expected tranche loss at dates to ten years,
and the statistics the model refuses. Left out (Divergences/RecursiveLossModelTests.cs): the loss probabilities and the
loss distribution, the percentile and the shortfall on it, which integrate a vector over the factor, where QuantLib's
quadrature leaves its last node unweighted (fixed); and with them the percentile at 0 and 1, which QuantLib's returns as
a probability, and the distribution's labels on weights with gaps, which QuantLib's gives by rank. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/RecursiveLossModelCxxVectorTests.cs through tests/NanoQL.Tests/Vectors/LossModelStatistics.cs."""
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


def model(copula, recoveries, correlation=None, factor_weights=None, t_orders=None, n_buckets=None):
    m = pf.constant_loss(copula, recoveries, correlation=correlation, factor_weights=factor_weights, t_orders=t_orders)
    m["kind"] = "Recursive"
    if n_buckets is not None:
        m["nBuckets"] = n_buckets
    return m


POOL = pf.flat_pool([0.01, 0.03, 0.02, 0.015, 0.025])
FULL = {"refused": ["probAtLeastNEvents", "probsBeingNthEvent", "defaultCorrelation", "probOverLoss", "splitVaRLevel",
                    "recoveryRate"]}
ETL = FULL
CASES = [
    ("Gaussian, equal weights, mezzanine", pf.basket(TODAY, [10] * 5, POOL, 0.03, 0.3),
     model("Gaussian", [0.4] * 5, 0.3), FULL),
    ("Gaussian by weights, consecutive weights 1 to 3, equity", pf.basket(TODAY, [10, 20, 30, 10, 20], POOL, 0.0, 0.2),
     model("Gaussian", [0.4] * 5, factor_weights=[[0.5], [0.3], [0.6], [0.4], [0.2]]), FULL),
    ("Student t, equal weights, senior", pf.basket(TODAY, [10] * 5, POOL, 0.1, 1.0),
     model("T", [0.4] * 5, 0.3, t_orders=[5, 7]), FULL),
    ("Gaussian, weights with gaps", pf.basket(TODAY, [10, 30, 10, 30, 70], POOL, 0.03, 0.12),
     model("Gaussian", [0.4, 0.4, 0.4, 0.4, 0.4], 0.3), ETL),
    ("Gaussian, unequal recoveries, two buckets a unit", pf.basket(TODAY, [10, 20, 30, 15, 25], POOL, 0.0, 0.1),
     model("Gaussian", [0.4, 0.35, 0.5, 0.3, 0.45], 0.3, n_buckets=2), ETL),
]

def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, b, m, stats in CASES:
            with H.case_settings(TODAY.serialNumber()) as st:
                inputs, expected = pf.model_statistics(call, b, m, DATES, **stats)
                out.append({"name": name, "inputs": {"settings": st, **inputs}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/recursivelossmodel.hpp", cases)
