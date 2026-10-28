# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InhomogeneousPoolLossModel (ql/experimental/credit/inhomogeneouspooldef.hpp), which the
wheel doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/basket.cpp) on
tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs: Gaussian and Student t models over one-factor
constant-loss latent models, by correlation and by weights, on baskets of unequal notionals, hazards and recoveries,
equity, mezzanine and senior tranches; the expected tranche loss and the percentile at dates to ten years, and the
statistics the model refuses. The shortfall reads Distribution's tranche and shortfall, whose defects nanoql fixes
(the S-xcr plan's XR8 (xx)), so it is left out (held in Divergences/DistributionMembersTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/InhomogeneousPoolLossModelCxxVectorTests.cs through
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
    m.update({"kind": "InhomogeneousPool", "nBuckets": 100, "max": 5.0, "min": -5.0, "nSteps": 15})
    return m


POOL = pf.flat_pool([0.01, 0.03, 0.02, 0.015, 0.025])
CASES = [
    ("Gaussian, equity tranche", pf.basket(TODAY, [10, 20, 30, 15, 25], POOL, 0.0, 0.1),
     model("Gaussian", [0.4, 0.35, 0.5, 0.3, 0.45], 0.3)),
    ("Gaussian by weights, mezzanine", pf.basket(TODAY, [10, 20, 30, 15, 25], POOL, 0.03, 0.12),
     model("Gaussian", [0.4] * 5, factor_weights=[[0.5], [0.3], [0.6], [0.4], [0.2]])),
    ("Student t, senior", pf.basket(TODAY, [10, 20, 30, 15, 25], POOL, 0.1, 1.0),
     model("T", [0.4, 0.35, 0.5, 0.3, 0.45], 0.3, t_orders=[5, 7])),
]
STATISTICS = {"levels": [0.5, 0.9, 0.99],
              "refused": ["probAtLeastNEvents", "probsBeingNthEvent", "defaultCorrelation", "lossDistribution",
                          "probOverLoss", "splitVaRLevel", "recoveryRate"]}


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, b, m in CASES:
            with H.case_settings(TODAY.serialNumber()) as st:
                inputs, expected = pf.model_statistics(call, b, m, DATES, **STATISTICS)
                out.append({"name": name, "inputs": {"settings": st, **inputs}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/inhomogeneouspooldef.hpp", cases)
