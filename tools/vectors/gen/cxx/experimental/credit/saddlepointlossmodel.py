# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SaddlePointLossModel (ql/experimental/credit/saddlepointlossmodel.hpp), which the
wheel doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/basket.cpp and
saddlepointlossmodel.cpp) on tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs: Gaussian models on pools
of 40 names, inside the expansion's domain; the expected tranche loss and the probability over fractions of the tranche
up to 1 at dates to ten years, the whole portfolio's tail and density, and the statistics the model refuses. Left out
(Divergences/SaddlePointLossModelTests.cs): the percentile, the shortfall and the loss distribution, whose searches read
the tail at levels below QuantLib's saddle bracket, where QuantLib's returns the bracket's end and nanoql solves;
fractions above 1, the shortfall on a tranche, and the degenerate nodes and small pools where QuantLib's tail throws or
leaves [0, 1]; and the split of loss levels, which integrates a vector over the factor, where QuantLib's quadrature
leaves its last node unweighted (fixed). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/SaddlePointLossModelCxxVectorTests.cs through tests/NanoQL.Tests/Vectors/LossModelStatistics.cs, the tail and density directly."""
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


def model(recoveries, correlation):
    m = pf.constant_loss("Gaussian", recoveries, correlation=correlation)
    m["kind"] = "SaddlePoint"
    return m


N = 40
POOL = pf.flat_pool([0.01 + 0.0005 * i for i in range(N)])
NOTIONALS = [10 + (i % 4) for i in range(N)]
LOSSES = [5.0, 20.0, 40.0]
REFUSED = ["probAtLeastNEvents", "probsBeingNthEvent", "defaultCorrelation", "recoveryRate"]
CASES = [
    ("Gaussian, mezzanine", pf.basket(TODAY, NOTIONALS, POOL, 0.03, 0.07), model([0.4] * N, 0.3),
     {"fractions": [0.0, 0.3, 0.7, 1.0], "refused": REFUSED}),
    ("Gaussian, whole pool", pf.basket(TODAY, NOTIONALS, POOL, 0.0, 1.0),
     model([0.4 - 0.002 * i for i in range(N)], 0.2),
     {"fractions": [0.01, 0.05, 0.1], "refused": REFUSED}),
]

def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, b, m, stats in CASES:
            with H.case_settings(TODAY.serialNumber()) as st:
                inputs, expected = pf.model_statistics(call, b, m, DATES, **stats)
                expected["members"] = call("SaddlePointLossModel::members", b, m, inputs["dates"], LOSSES)
                inputs["losses"] = LOSSES
                out.append({"name": name, "inputs": {"settings": st, **inputs}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/saddlepointlossmodel.hpp", cases)
