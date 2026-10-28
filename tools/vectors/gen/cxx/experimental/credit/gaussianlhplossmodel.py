# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GaussianLHPLossModel (ql/experimental/credit/gaussianlhplossmodel.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/basket.cpp) on
tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs: models at three correlations on baskets of equal and
unequal notionals and recoveries, equity, mezzanine and senior tranches, no programmed default (QuantLib's reads past
its vectors under one, held in Divergences/LhpProgrammedDefaultTests.cs); the expected tranche loss, the percentile,
the shortfall and the probability over loss fractions at dates to ten years, each name's recovery, and the statistics
the model refuses. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/GaussianLHPLossModelCxxVectorTests.cs through
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


def model(correlation, recoveries):
    return {"kind": "GaussianLHP", "correlation": correlation, "recoveries": list(recoveries)}


POOL = pf.flat_pool([0.02, 0.03, 0.01, 0.025])
CASES = [
    ("equity tranche, equal names", pf.basket(TODAY, [25] * 4, pf.flat_pool([0.02] * 4), 0.0, 0.1),
     model(0.3, [0.4] * 4)),
    ("mezzanine, unequal names", pf.basket(TODAY, [10, 20, 30, 40], POOL, 0.03, 0.12), model(0.5, [0.4, 0.3, 0.5, 0.35])),
    ("senior, low correlation", pf.basket(TODAY, [10, 20, 30, 40], POOL, 0.1, 1.0), model(0.1, [0.4, 0.3, 0.5, 0.35])),
]
STATISTICS = {"levels": [0.0, 0.5, 0.9, 0.99, 1.0], "shortfalls": [0.5, 0.9, 0.99], "fractions": [0.0, 0.25, 0.5, 1.0],
              "recoveries": True,
              "refused": ["probAtLeastNEvents", "probsBeingNthEvent", "defaultCorrelation", "lossDistribution",
                          "splitVaRLevel"]}


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
    v.main(__file__, "ql/experimental/credit/gaussianlhplossmodel.hpp", cases)
