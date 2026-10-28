# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DefaultLatentModel and ConstantLossLatentmodel
(ql/experimental/credit/defaultprobabilitylatentmodel.hpp, constantlosslatentmodel.hpp), which the wheel doesn't
expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/defaultprobabilitylatentmodel.cpp)
on tools/vectors/gen/cxx/experimental/credit/defaultevent.py's basket specs.

Gaussian and Student t models, by correlation (QuantLib's quote constructor) and by factor weights, reset on baskets of
pools added in name order (the S-xcr plan's XR8 (i)) with no realized default, at integrations by Gauss-Hermite and,
on one factor, by trapezoids: each name's unconditional default probability integrated over the factors at dates from
the evaluation date, where it is 0, to ten years; each pair's default correlation past the evaluation date (at it,
QuantLib divides 0 by 0, XR8 (xlvi), held in Conventions/CreditPortfolioConventionTests.cs); the probability of n
defaults or more for every n; the conditional default probabilities, plain and from the inverse, at probabilities
below and above 1e-10 and factor samples; and the constant-loss model's recoveries. And a reset on a basket of another
size, refused. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/DefaultProbabilityLatentModelCxxVectorTests.cs."""
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
BASKET = pf.basket(TODAY, [10, 20, 30, 40], pf.flat_pool([0.01, 0.025, 0.015, 0.04]), 0.0, 0.2)


def model(kind, copula, integration=0, correlation=None, factor_weights=None, t_orders=None, recoveries=None):
    m = {"kind": kind, "copula": copula, "integration": integration}
    if correlation is not None:
        m["correlation"] = correlation
        m["nVariables"] = 4
    else:
        m["factorWeights"] = factor_weights
    if t_orders is not None:
        m["tOrders"] = t_orders
    if recoveries is not None:
        m["recoveries"] = recoveries
    return m


FW2 = [[0.3, 0.2], [0.5, 0.1], [0.2, 0.6], [0.4, 0.4]]
MODELS = [
    ("Gaussian by correlation", model("DefaultLatentModel", "Gaussian", correlation=0.3)),
    ("Gaussian by two factors", model("DefaultLatentModel", "Gaussian", factor_weights=FW2)),
    ("Gaussian by correlation, trapezoids", model("DefaultLatentModel", "Gaussian", 1, correlation=0.3)),
    ("Student t by correlation", model("DefaultLatentModel", "T", correlation=0.3, t_orders=[5, 5])),
    ("Student t by weights, trapezoids", model("DefaultLatentModel", "T", 1, factor_weights=[[0.3], [0.5], [0.2], [0.7]],
                                               t_orders=[3, 7])),
    ("constant loss, Gaussian", model("ConstantLossLatentModel", "Gaussian", correlation=0.45,
                                      recoveries=[0.4, 0.35, 0.5, 0.0])),
    ("constant loss, Student t", model("ConstantLossLatentModel", "T", factor_weights=[[0.3], [0.5], [0.2], [0.7]],
                                       t_orders=[5, 5], recoveries=[0.4, 0.4, 0.2, 0.6])),
    ("constant loss, Gaussian by two factors", model("ConstantLossLatentModel", "Gaussian", factor_weights=FW2,
                                                     recoveries=[0.3, 0.45, 0.5, 0.2])),
    ("constant loss, Student t by correlation", model("ConstantLossLatentModel", "T", correlation=0.2, t_orders=[3, 5],
                                                      recoveries=[0.4, 0.35, 0.5, 0.0])),
]
DATES = [TODAY, TODAY + 30, TODAY + 365, TODAY + 1826, TODAY + 3653]
PROBS = [5e-11, 1e-6, 0.01, 0.3, 0.75]
FACTORS = {1: [[-2.5], [0.0], [1.2]], 2: [[-2.5, 0.3], [0.0, 0.0], [1.2, -0.8]]}


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, m in MODELS:
            with H.case_settings(TODAY.serialNumber()) as st:
                k = len(m["factorWeights"][0]) if "factorWeights" in m else 1
                factors = FACTORS[k]
                dates = [d.serialNumber() for d in DATES]
                expected = {
                    "probOfDefault": [call("DefaultLatentModel::probOfDefault", m, BASKET, i, dates) for i in range(4)],
                    "defaultCorrelation": [call("DefaultLatentModel::defaultCorrelation", m, BASKET, d, i, j)
                                           for d in dates[1:] for i in range(4) for j in range(4)],
                    "probAtLeastNEvents": [call("DefaultLatentModel::probAtLeastNEvents", m, BASKET, n, d)
                                           for n in range(6) for d in dates],
                    "conditional": [call("DefaultLatentModel::conditional", m, BASKET, PROBS, i, factors)
                                    for i in range(4)]}
                if m["kind"] == "ConstantLossLatentModel":
                    expected["recoveries"] = [call("ConstantLossLatentModel::recoveries", m, BASKET, d) for d in dates[:2]]
                out.append({"name": name,
                            "inputs": {"settings": st, "model": m, "basket": BASKET, "dates": dates,
                                       "probabilities": PROBS, "factors": factors},
                            "expected": expected})
        with H.case_settings(TODAY.serialNumber()) as st:
            m = model("DefaultLatentModel", "Gaussian", correlation=0.3)
            small = pf.basket(TODAY, [10, 20], pf.flat_pool([0.01, 0.02]))
            out.append({"name": "another size", "inputs": {"settings": st, "model": m, "basket": small},
                        "expected": {"probOfDefault": call("DefaultLatentModel::probOfDefault", m, small, 0,
                                                           [(TODAY + 365).serialNumber()])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/defaultprobabilitylatentmodel.hpp", cases)
