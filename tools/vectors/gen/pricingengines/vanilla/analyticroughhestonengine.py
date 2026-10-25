# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticRoughHestonEngine (ql/pricingengines/vanilla/analyticroughhestonengine.hpp, .cpp), on
the evaluation date 15 October 2026: European options under the rough Heston model, its fractional Riccati equation
solved by the Adams scheme, the Padé approximation or the lifted approximation.

Two rough Heston models (Hurst exponents 0.1 and 0.3 on QuantLib's own case's parameters, v₀ = θ 0.04, κ 0.3, σ 0.4, ρ
-0.7) on flat and linear zero curves: the NPV of calls and puts at five strikes and three maturities by each route at
QuantLib's defaults (Gauss-Laguerre of order 128, 256 time steps, 20 lifted factors), the Adams route at 64 time steps,
the lifted at 10 factors, and the Adams route on Gauss-Lobatto at 1e-8 with a damping exponent of -0.3. The members,
ChF, LnChF, RiccatiSolution and the time form, which the wheel doesn't expose, are the harness's
(tools/vectors/gen/cxx/pricingengines/vanilla/analyticroughhestonengine.py).

A case holds one model {"process" (tools/vectors/gen/processes/hestonprocess.py's spec), "hurst"} and an engine
{"order", "timeSteps", "approximation" (QuantLib's integer), "nFactors"} or {"lobatto" [relTolerance, maxEvaluations],
"timeSteps", "epsilon", "alpha", "approximation", "nFactors"}. The wheel's calls, by QuantLib-SWIG 1.44's
calibratedmodel.i and options.i: RoughHestonModel(HestonProcess(...), hurst), AnalyticRoughHestonEngine's two
constructors and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticRoughHestonEngineVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
hp = _load("hestonprocess", "processes/hestonprocess.py")
R = bsp.R
MATURITIES = [91, 365, 730]
STRIKES = [75.0, 90.0, 100.0, 110.0, 130.0]
HURSTS = [0.1, 0.3]
E = ql.AnalyticRoughHestonEngine
ENGINES = [
    ("Adams", {"order": 128, "timeSteps": 256, "approximation": int(E.AdamsPredictorCorrector), "nFactors": 20}),
    ("Pade", {"order": 128, "timeSteps": 256, "approximation": int(E.Pade), "nFactors": 20}),
    ("lifted", {"order": 128, "timeSteps": 256, "approximation": int(E.Lifted), "nFactors": 20}),
    ("Adams at 64 steps", {"order": 128, "timeSteps": 64, "approximation": int(E.AdamsPredictorCorrector),
                           "nFactors": 20}),
    ("lifted at 10 factors", {"order": 128, "timeSteps": 256, "approximation": int(E.Lifted), "nFactors": 10}),
    ("Adams on Lobatto, alpha -0.3", {"lobatto": [1e-8, 100000], "timeSteps": 256, "epsilon": 1e-12, "alpha": -0.3,
                                      "approximation": int(E.AdamsPredictorCorrector), "nFactors": 20}),
]


def model(spec):
    return ql.RoughHestonModel(hp.process({**spec["process"], "discretization": 0}), spec["hurst"])


def engine(m, spec):
    if "order" in spec:
        return ql.AnalyticRoughHestonEngine(m, spec["order"], spec["timeSteps"], spec["approximation"], spec["nFactors"])
    rel, n = spec["lobatto"]
    return ql.AnalyticRoughHestonEngine(m, ql.FourierIntegration.gaussLobatto(rel, ql.nullDouble(), n),
                                        spec["timeSteps"], spec["epsilon"], spec["alpha"], spec["approximation"],
                                        spec["nFactors"])


def cases():
    st = v.settings(R)
    out = []
    for hurst in HURSTS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = {"process": {"riskFree": rf, "dividend": q, "s0": 100.0, "v0": 0.04, "kappa": 0.3, "theta": 0.04,
                                "sigma": 0.4, "rho": -0.7}, "hurst": hurst}
            m = model(spec)
            for ename, espec in ENGINES:
                e = engine(m, espec)
                npv = []
                for days in MATURITIES:
                    for kind in (ql.Option.Call, ql.Option.Put):
                        for k in STRIKES:
                            o = ql.VanillaOption(ql.PlainVanillaPayoff(kind, k), ql.EuropeanExercise(R + days))
                            o.setPricingEngine(e)
                            npv.append(v.attempt(lambda: o.NPV()))
                out.append({"name": f"H {hurst}, {cname} curves, {ename}",
                            "inputs": {"settings": st, "model": spec, "engine": espec, "maturities": MATURITIES,
                                       "strikes": STRIKES},
                            "expected": {"npv": npv}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticroughhestonengine.hpp", cases)
