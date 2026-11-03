# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BaseCorrelationLossModel (ql/experimental/credit/basecorrelationlossmodel.hpp), which
the wheel doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/basket.cpp) on
tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs: the model on QuantLib's four bilinear base models
(the Gaussian LHP, the Gaussian and Student t binomial on a homogeneous pool, whose weight nanoql fixes elsewhere, and
the Gaussian inhomogeneous pool), tranches inside the surface's loss levels; the expected tranche loss at dates to ten
years, and the statistics the model refuses. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/BaseCorrelationLossModelCxxVectorTests.cs through tests/NanoQL.Tests/Vectors/LossModelStatistics.cs."""
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


SURFACE = pf.surface([(1, pf.YEARS), (3, pf.YEARS), (5, pf.YEARS), (7, pf.YEARS)], [0.03, 0.07, 0.1, 0.15, 0.3],
                     [[0.15, 0.17, 0.18, 0.2], [0.22, 0.24, 0.25, 0.27], [0.28, 0.3, 0.31, 0.33],
                      [0.35, 0.37, 0.38, 0.4], [0.5, 0.52, 0.55, 0.58]])


def model(base, recoveries, t_orders=None):
    m = {"kind": "BaseCorrelation", "base": base, "surface": SURFACE, "recoveries": list(recoveries)}
    if t_orders is not None:
        m["tOrders"] = list(t_orders)
    return m


INHOMOGENEOUS = pf.flat_pool([0.01, 0.03, 0.02, 0.015, 0.025, 0.02, 0.01, 0.03])
HOMOGENEOUS = pf.flat_pool([0.02] * 8)
REFUSED = {"refused": ["percentile", "expectedShortfall", "probOverLoss", "lossDistribution", "splitVaRLevel",
                       "probAtLeastNEvents", "probsBeingNthEvent", "defaultCorrelation"]}
CASES = [
    ("Gaussian LHP base, [3%, 7%]", pf.basket(TODAY, [10, 20, 30, 15, 25, 10, 20, 30], INHOMOGENEOUS, 0.03, 0.07),
     model("GaussianLHP", [0.4, 0.35, 0.5, 0.3, 0.45, 0.4, 0.4, 0.4]), REFUSED),
    ("Gaussian LHP base, [7%, 15%]", pf.basket(TODAY, [10] * 8, INHOMOGENEOUS, 0.07, 0.15),
     model("GaussianLHP", [0.4] * 8), REFUSED),
    ("Gaussian binomial base, [3%, 10%]", pf.basket(TODAY, [10] * 8, HOMOGENEOUS, 0.03, 0.1),
     model("GaussianBinomial", [0.4] * 8), REFUSED),
    ("Student t binomial base, [7%, 30%]", pf.basket(TODAY, [10] * 8, HOMOGENEOUS, 0.07, 0.3),
     model("TBinomial", [0.4] * 8, t_orders=[5, 7]), REFUSED),
    ("Gaussian inhomogeneous pool base, [3%, 7%]", pf.basket(TODAY, [10, 20, 30, 15, 25, 10, 20, 30], INHOMOGENEOUS,
                                                             0.03, 0.07),
     model("IHGaussPool", [0.4] * 8), REFUSED),
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
    v.main(__file__, "ql/experimental/credit/basecorrelationlossmodel.hpp", cases)
