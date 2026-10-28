# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GaussianRandomDefaultModel (ql/experimental/credit/randomdefaultmodel.hpp), which the
wheel doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/
randomdefaultmodel.cpp): the pool's default times after each of several sequences at QuantLib's pseudo-random
Gaussian sequences, by seed, correlation, accuracy and maximum time, with a reset; and the default maximum time,
past the curves' range, which QuantLib refuses. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/GaussianRandomDefaultModelCxxVectorTests.cs."""
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
dts = pf.dts
TODAY = pf.TODAY

POOL = pf.flat_pool([0.03, 0.05, 0.02, 0.04, 0.06])
HIGH = pf.flat_pool([0.2, 0.3, 0.25])
CASES = [
    ("correlation 0.3, seed 42, ten years", POOL, {"kind": "Gaussian", "correlation": 0.3}, 1e-6, 42, 10.0, 6, None),
    ("correlation 0.8, seed 2863311530, thirty years, reset after two", POOL,
     {"kind": "Gaussian", "correlation": 0.8}, 1e-8, 2863311530, 30.0, 5, 1),
    ("high hazards, correlation 0, coarse accuracy", HIGH, {"kind": "Gaussian", "correlation": 0.0}, 1e-3, 7, 5.0, 8,
     None),
    ("the default maximum time", POOL, {"kind": "Gaussian", "correlation": 0.3}, 1e-6, 42, None, 1, None),
]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, pool, copula, accuracy, seed, tmax, n, reset in CASES:
            with H.case_settings(TODAY.serialNumber()) as st:
                args = [pool, copula, accuracy, seed, tmax, n, reset]
                out.append({"name": name, "inputs": {"settings": st, "args": args},
                            "expected": {"sequences": call("GaussianRandomDefaultModel::sequences", *args)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/randomdefaultmodel.hpp", cases)
