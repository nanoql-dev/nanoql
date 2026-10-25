# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Harness vectors for QuantLib's AnalyticPTDHestonEngine members (ql/pricingengines/vanilla/analyticptdhestonengine.hpp,
.cpp), on the evaluation date 15 October 2026: the normalized characteristic function and its logarithm, which the wheel
doesn't expose.

tools/vectors/gen/pricingengines/vanilla/analyticptdhestonengine.py's three models on batesmodel.py's flat curves: chF
and lnChF at six complex arguments (on the Gatheral contour u - i/2, and off it) at four times inside each grid.

A case holds the "process" (tools/vectors/gen/cxx/models/equity/batesmodel.py's harness spec, whose curves, spot and
v0 the model takes), "grid", "pTimes", "theta", "kappa", "sigma", "rho" (each a list, one value a constant), "zs"
([real, imaginary] each) and "ts". The harness's call: AnalyticPTDHestonEngine::chF
(tools/cxx/src/pricingengines/vanilla/analyticptdhestonengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/AnalyticPTDHestonEngineCxxVectorTests.cs."""
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
ptd = _load("analyticptdhestonengine", "pricingengines/vanilla/analyticptdhestonengine.py")
R = bm.R
ZS = [[0.5, -0.5], [2.0, -0.5], [10.0, -0.5], [1.0, 0.0], [3.0, -1.0], [-1.5, 0.3]]
TS = [0.1, 0.6, 1.2, 3.0]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for mname, m in ptd.MODELS:
            # the model's spot and v0 through the harness's process spec; its κ, θ, σ and ρ the model's own
            process = bm.process_spec(("ptd", m["s0"], m["v0"], 1.0, 0.04, 0.3, -0.5))
            args = [m["grid"], m["pTimes"], m["theta"], m["kappa"], m["sigma"], m["rho"]]
            r = H.call("AnalyticPTDHestonEngine::chF", process, *args, ZS, TS)
            out.append({"name": mname,
                        "inputs": {"settings": st, "process": process, "grid": m["grid"], "pTimes": m["pTimes"],
                                   "theta": m["theta"], "kappa": m["kappa"], "sigma": m["sigma"], "rho": m["rho"],
                                   "zs": ZS, "ts": TS},
                        "expected": {"chF": [v.enc(z) for z in r["chF"]], "lnChF": [v.enc(z) for z in r["lnChF"]]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticptdhestonengine.hpp", cases)
