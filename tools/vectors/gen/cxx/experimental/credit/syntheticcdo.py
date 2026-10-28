# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SyntheticCDO and its two engines (ql/experimental/credit/syntheticcdo.hpp, .cpp,
midpointcdoengine.hpp, .cpp, integralcdoengine.hpp, .cpp), which the wheel doesn't expose, each output computed by the
C++ harness (tools/cxx/src/experimental/credit/syntheticcdo.cpp) on
tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs.

CDOs on equity, mezzanine and senior tranches of baskets bound to the pool, LHP, constant-loss and random default
models, sellers and buyers, with and without an upfront, on a spot schedule from the day after the evaluation date,
curves dated at the evaluation date (QuantLib refuses curves dated after it and a seasoned CDO, the S-xcr plan's XR8
(x)), on the mid-point engine and the integral engine at QuantLib's three-month step and at a month: the value, the
legs, the fair premium, the instrument's and the engine's leg values, the remaining notional, the error count and the
expected tranche losses; the inspectors; and a CDO expired at the evaluation date, QuantLib's expired results. Unlevered (QuantLib's engines lever the legs unevenly, XR8 (ix), held in
Divergences/CdoLeverageTests.cs); the fair upfront and the engines' additional fair premium are left out (XR8 (ix)).
Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/SyntheticCDOCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


pf = _load("cxx_defaultevent", "cxx/experimental/credit/defaultevent.py")
nt = _load("cxx_integralntdengine", "cxx/experimental/credit/integralntdengine.py")

TODAY = pf.TODAY
D = ql.Date
A360 = {"type": "Actual360"}
SELLER, BUYER = int(ql.Protection.Seller), int(ql.Protection.Buyer)
DISCOUNT = nt.DISCOUNT
SCHEDULE = nt.SCHEDULE
SHORT = nt.SHORT
POOL = pf.flat_pool([0.01, 0.02, 0.015, 0.03, 0.025, 0.01, 0.02, 0.015])
NOTIONALS = [10.0] * 8


def pool_model(kind, correlation):
    m = pf.constant_loss("Gaussian", [0.4] * 8, correlation=correlation)
    m.update({"kind": kind, "nBuckets": 100, "max": 5.0, "min": -5.0, "nSteps": 15})
    return m


def random_model(correlation):
    m = pf.constant_loss("Gaussian", [0.4] * 8, correlation=correlation)
    m.update({"kind": "RandomDefault", "nSims": 1000, "accuracy": 1e-6, "seed": 2863311530})
    return m


MODELS = [
    ("inhomogeneous pool", pool_model("InhomogeneousPool", 0.3)),
    ("homogeneous pool", pool_model("HomogeneousPool", 0.3)),
    ("Gaussian LHP", {"kind": "GaussianLHP", "correlation": 0.3, "recoveries": [0.4] * 8}),
    ("constant loss", pf.constant_loss("Gaussian", [0.4] * 8, correlation=0.3)),
    ("random default", random_model(0.3)),
]
TRANCHES = [(0.0, 0.03), (0.03, 0.07), (0.07, 1.0)]


def cdo(side, sched, upfront, running, notional=None):
    return {"side": side, "schedule": sched, "upfrontRate": upfront, "runningRate": running, "dayCounter": A360,
            "paymentConvention": int(ql.Following), "notional": notional}


CDOS = [cdo(SELLER, SCHEDULE, 0.0, 0.02), cdo(BUYER, SCHEDULE, 0.05, 0.02), cdo(SELLER, SHORT, 0.1, 0.05)]
STEPS = [[3, pf.MONTHS], [1, pf.MONTHS]]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, m in MODELS:
            for a, d in TRANCHES:
                with H.case_settings(TODAY.serialNumber()) as st:
                    b = pf.basket(TODAY, NOTIONALS, POOL, a, d)
                    out.append({"name": f"{name}, [{a}, {d}]",
                                "inputs": {"settings": st, "basket": b, "model": m, "cdos": CDOS,
                                           "discount": DISCOUNT, "steps": STEPS},
                                "expected": {
                                    "inspect": [call("SyntheticCDO::inspect", b, m, c) for c in CDOS],
                                    "midPoint": [call("MidPointCDOEngine::calculate", b, m, c, DISCOUNT) for c in CDOS],
                                    "integral": [call("IntegralCDOEngine::calculate", b, m, c, DISCOUNT, s)
                                                 for c in CDOS for s in STEPS]}})
        with H.case_settings(TODAY.serialNumber()) as st:
            m = pool_model("InhomogeneousPool", 0.3)
            b = pf.basket(D(1, 1, 2004), NOTIONALS, POOL, 0.03, 0.07)
            c = cdo(SELLER, nt.schedule(D(1, 9, 2004), D(1, 9, 2005)), 0.0, 0.02)
            out.append({"name": "an expired CDO", "inputs": {"settings": st, "basket": b, "model": m, "cdo": c,
                                                             "discount": DISCOUNT},
                        "expected": {"expired": call("SyntheticCDO::expired", b, m, c, DISCOUNT)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/syntheticcdo.hpp", cases)
