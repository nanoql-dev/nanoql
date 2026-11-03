# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BinomialLossModel (ql/experimental/credit/binomiallossmodel.hpp), which the wheel
doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/basket.cpp) on
tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs: the Gaussian and Student t models on homogeneous
pools, by correlation and by weights, equity, mezzanine and senior tranches; the expected tranche loss at dates to ten
years, and the statistics the model refuses. Left out (Divergences/BinomialLossModelTests.cs): inhomogeneous pools,
whose adjusted binomial's weight is QuantLib's defect, fixed; and the loss distribution, the percentile and the
shortfall, which integrate a vector over the factor, where QuantLib's quadrature leaves its last node unweighted
(fixed). Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/BinomialLossModelCxxVectorTests.cs through
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


def model(copula, recoveries, correlation=None, factor_weights=None, t_orders=None):
    m = pf.constant_loss(copula, recoveries, correlation=correlation, factor_weights=factor_weights, t_orders=t_orders)
    m["kind"] = "Binomial"
    return m


POOL = pf.flat_pool([0.02] * 6)
STATS = {"refused": ["probAtLeastNEvents", "probsBeingNthEvent", "defaultCorrelation", "probOverLoss", "splitVaRLevel",
                     "recoveryRate"]}
CASES = [
    ("Gaussian, equity tranche", pf.basket(TODAY, [10] * 6, POOL, 0.0, 0.1), model("Gaussian", [0.4] * 6, 0.3), STATS),
    ("Gaussian by weights, mezzanine", pf.basket(TODAY, [10] * 6, POOL, 0.03, 0.12),
     model("Gaussian", [0.35] * 6, factor_weights=[[0.5]] * 6), STATS),
    ("Student t, senior", pf.basket(TODAY, [25] * 6, POOL, 0.1, 1.0), model("T", [0.4] * 6, 0.3, t_orders=[5, 7]),
     STATS),
    ("Gaussian, whole pool, high correlation", pf.basket(TODAY, [10] * 6, pf.flat_pool([0.05] * 6), 0.0, 1.0),
     model("Gaussian", [0.3] * 6, 0.8), STATS),
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
    v.main(__file__, "ql/experimental/credit/binomiallossmodel.hpp", cases)
