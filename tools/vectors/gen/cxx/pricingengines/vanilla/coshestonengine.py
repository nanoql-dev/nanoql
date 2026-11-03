# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Harness vectors for QuantLib's COSHestonEngine members (ql/pricingengines/vanilla/coshestonengine.hpp, .cpp), on the
evaluation date 15 October 2026: the normalized characteristic function, the cumulants C1 to C4 and the mean,
variance, skew and kurtosis, which the wheel doesn't expose.

tools/vectors/gen/cxx/models/equity/batesmodel.py's two Heston sets and a third with κ 4 and σ 1.8 (QuantLib's
testCosHestonEngine's), at two truncations and orders: the characteristic function at six u at three times, the members
at those times. Every κt is between 1.2 and 20, at or above every cumulant's cut and below nanoql's large-κt cut, so the
cumulants are QuantLib's double arithmetic; every σ is far above the small-σ branch; the κt below the cuts, where
QuantLib's rounding passes 1e-12, and the members at t = 0 are left out, both divergence facts.

A case holds the "process" (batesmodel.py's harness spec), "L", "N", "us" and "ts". The harness's call:
COSHestonEngine::members (tools/cxx/src/pricingengines/vanilla/coshestonengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/COSHestonEngineCxxVectorTests.cs."""
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


bm = _load("cxx_batesmodel", "cxx/models/equity/batesmodel.py")
R = bm.R
SETS = bm.HESTON + [("COS case", 100.0, 0.1, 4.0, 0.22, 1.8, -0.75)]
US = [0.0, 0.45, 1.0, 3.0, 10.0, 40.0]
TS = [1.5, 2.5, 5.0]
KEYS = ["c1", "c2", "c3", "c4", "mu", "var", "skew", "kurtosis"]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for params in SETS:
            process = bm.process_spec(params)
            for l, n in ((16.0, 200), (25.0, 600)):
                r = H.call("COSHestonEngine::members", process, l, n, US, TS)
                out.append({"name": f"{params[0]}, L {l}, N {n}",
                            "inputs": {"settings": st, "process": process, "L": l, "N": n, "us": US, "ts": TS},
                            "expected": {"chF": [v.enc(z) for z in r["chF"]], **{k: [v.enc(x) for x in r[k]] for k in KEYS}}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/coshestonengine.hpp", cases)
