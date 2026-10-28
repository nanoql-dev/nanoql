# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SpotRecoveryLatentModel (ql/experimental/credit/spotlosslatentmodel.hpp), which the wheel
doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/spotlosslatentmodel.cpp)
on tools/vectors/gen/cxx/experimental/credit/defaultevent.py's basket specs.

Gaussian and Student t models of one and two factors, the defaults' weights then the recoveries', at several model
parameters A, reset on baskets of curves dated at the evaluation date: each name's conditional default probability by
dates and from probabilities below and above 1e-10, its expected conditional recovery, its spot recovery on latent
values and its recovery variable on samples of every factor; and the refused models (odd rows, recoveries not
matching, a basket of another size). A model parameter of 0 divides by 0 in QuantLib's conditional recovery, so it is
refused and left out (the S-xcr plan's XR8 (xxxiv), held in Conventions/RandomModelConventionTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/SpotRecoveryLatentModelCxxVectorTests.cs."""
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
BASKET = pf.basket(TODAY, [10, 20, 30], pf.flat_pool([0.02, 0.04, 0.01]))


def model(copula, weights, recoveries, model_a, t_orders=None, integration=0):
    m = {"copula": copula, "integration": integration, "factorWeights": weights, "recoveries": list(recoveries),
         "modelA": model_a}
    if t_orders is not None:
        m["tOrders"] = list(t_orders)
    return m


ONE = [[0.5], [0.3], [0.6], [0.4], [0.5], [0.2]]
TWO = [[0.4, 0.2], [0.3, 0.3], [0.5, 0.1], [0.3, 0.4], [0.2, 0.2], [0.6, 0.1]]
MODELS = [
    ("Gaussian, one factor", model("Gaussian", ONE, [0.4, 0.35, 0.5], 0.8)),
    ("Gaussian, two factors", model("Gaussian", TWO, [0.4, 0.35, 0.5], 1.5)),
    ("Gaussian, a small parameter", model("Gaussian", ONE, [0.2, 0.6, 0.45], 0.1)),
    ("Student t, one factor", model("T", ONE, [0.4, 0.35, 0.5], 0.8, [5, 7])),
]
REFUSED = [model("Gaussian", ONE[:5], [0.4, 0.35, 0.5], 0.8), model("Gaussian", ONE, [0.4, 0.35], 0.8),
           model("Gaussian", ONE[:4], [0.4, 0.35], 0.8)]
DATES = [TODAY, TODAY + 365, TODAY + 3653]
PROBS = [5e-11, 1e-6, 0.05, 0.5]
LATENTS = [-3.0, -1.0, 0.0, 1.2]


def factors(k):
    return [[-2.0] * k, [0.0] * k, [0.7, -0.4][:k]]


def samples(k, n):
    base = [-1.5, 0.3, 0.8, -0.2, 2.1, 0.0, -0.7, 1.1, 0.4, -0.9]
    return [[base[(j + s) % len(base)] for j in range(k + 2 * n)] for s in range(3)]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, m in MODELS:
            with H.case_settings(TODAY.serialNumber()) as st:
                k = len(m["factorWeights"][0])
                f, s = factors(k), samples(k, 3)
                ds = [d.serialNumber() for d in DATES]
                out.append({"name": name,
                            "inputs": {"settings": st, "model": m, "basket": BASKET, "dates": ds, "factors": f,
                                       "probabilities": PROBS, "latents": LATENTS, "samples": s},
                            "expected": {"members": [call("SpotRecoveryLatentModel::members", m, BASKET, d, f, PROBS,
                                                          LATENTS, s) for d in ds]}})
        with H.case_settings(TODAY.serialNumber()) as st:
            out.append({"name": "refused models", "inputs": {"settings": st, "models": REFUSED, "basket": BASKET},
                        "expected": {"refused": [call("SpotRecoveryLatentModel::refused", m, BASKET) for m in REFUSED]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/spotlosslatentmodel.hpp", cases)
