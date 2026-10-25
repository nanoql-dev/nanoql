# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Harness vectors for QuantLib's AnalyticPDFHestonEngine members (ql/pricingengines/vanilla/analyticpdfhestonengine.hpp,
.cpp), on the evaluation date 15 October 2026: the density of the log spot, Pv, and the distribution of the spot, cdf,
which the wheel doesn't expose.

tools/vectors/gen/cxx/models/equity/batesmodel.py's two Heston sets at QuantLib's default accuracy and at 1e-8: Pv at
five log spots and cdf at five spots around the spot, at three times. The members read the model's density on its
clock, the curves sharing their reference date and day counter, so the risk-free curve's times (the S-hes plan's HR7
(xi)); every κt is far above HR7 (xxv)'s floor.

A case holds the "process" (batesmodel.py's harness spec), "eps", "maxIterations", "xs", "ss" and "ts". The harness's
call: AnalyticPDFHestonEngine::members (tools/cxx/src/pricingengines/vanilla/analyticpdfhestonengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/AnalyticPDFHestonEngineCxxVectorTests.cs."""
import importlib.util
import math
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
TS = [0.25, 1.0, 3.0]
MONEYNESS = [0.6, 0.85, 1.0, 1.15, 1.6]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for params in bm.HESTON:
            process = bm.process_spec(params)
            s0 = params[1]
            xs = [math.log(m * s0) for m in MONEYNESS]
            ss = [m * s0 for m in MONEYNESS]
            for eps in (1e-6, 1e-8):
                r = H.call("AnalyticPDFHestonEngine::members", process, eps, 10000, xs, ss, TS)
                out.append({"name": f"{params[0]}, accuracy {eps}",
                            "inputs": {"settings": st, "process": process, "eps": eps, "maxIterations": 10000, "xs": xs,
                                       "ss": ss, "ts": TS},
                            "expected": {"pv": [v.enc(x) for x in r["pv"]], "cdf": [v.enc(x) for x in r["cdf"]]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticpdfhestonengine.hpp", cases)
