# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Harness vectors for QuantLib's AnalyticRoughHestonEngine members
(ql/pricingengines/vanilla/analyticroughhestonengine.hpp, .cpp), on the evaluation date 15 October 2026: the normalized
characteristic function, its logarithm, the fractional Riccati solution and the time form of the price, which the wheel
doesn't expose.

QuantLib's case's model (v₀ = θ 0.04, κ 0.3, σ 0.4, ρ -0.7) at Hurst exponents 0.1 and 0.3 on batesmodel.py's flat
curves, by the Adams, Padé and lifted routes at QuantLib's defaults and the Adams route at 64 steps: chF, lnChF and
riccatiSolution at five complex arguments (on the pricing contour u - 3i/2 the engine reads, and off it) at three
times, and the time form's price of three calls and three puts. The curves share their reference date and day
counter, so the time form reads them at QuantLib's times (the S-hes plan's HR7 (xii)).

A case holds the "process" (tools/vectors/gen/cxx/models/equity/batesmodel.py's harness spec), "hurst", "order",
"timeSteps", "approximation" (QuantLib's integer), "nFactors", "zs" ([real, imaginary] each), "ts" and "payoffs"
([optionType, strike, t] each). The harness's call: AnalyticRoughHestonEngine::members
(tools/cxx/src/pricingengines/vanilla/analyticroughhestonengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/AnalyticRoughHestonEngineCxxVectorTests.cs."""
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
CALL, PUT = 1, -1
ZS = [[0.5, -1.5], [3.0, -1.5], [12.0, -1.5], [2.0, -0.5], [1.0, 0.0]]
TS = [0.25, 1.0, 2.0]
PAYOFFS = [[CALL, 90.0, 0.5], [CALL, 100.0, 1.0], [CALL, 120.0, 2.0], [PUT, 80.0, 0.5], [PUT, 100.0, 1.0],
           [PUT, 110.0, 2.0]]
ROUTES = [("Adams", 128, 256, 0, 20), ("Pade", 128, 256, 1, 20), ("lifted", 128, 256, 2, 20),
          ("Adams at 64 steps", 128, 64, 0, 20)]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        process = bm.process_spec(("rough Heston case", 100.0, 0.04, 0.3, 0.04, 0.4, -0.7))
        for hurst in (0.1, 0.3):
            for rname, order, steps, approximation, n in ROUTES:
                r = H.call("AnalyticRoughHestonEngine::members", process, hurst, order, steps, approximation, n, ZS,
                           TS, PAYOFFS)
                out.append({"name": f"H {hurst}, {rname}",
                            "inputs": {"settings": st, "process": process, "hurst": hurst, "order": order,
                                       "timeSteps": steps, "approximation": approximation, "nFactors": n, "zs": ZS,
                                       "ts": TS, "payoffs": PAYOFFS},
                            "expected": {k: [v.enc(z) for z in r[k]]
                                         for k in ("chF", "lnChF", "riccatiSolution", "priceVanillaPayoff")}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticroughhestonengine.hpp", cases)
