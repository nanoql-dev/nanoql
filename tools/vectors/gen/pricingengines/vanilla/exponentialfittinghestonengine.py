# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ExponentialFittingHestonEngine (ql/pricingengines/vanilla/exponentialfittinghestonengine.hpp,
.cpp), on the evaluation date 15 October 2026: European options under the Heston model by an exponentially fitted
Gauss-Laguerre rule of 64 nodes on the analytic engine's control variate.

tools/vectors/gen/processes/hestonprocess.py's three Heston parameter sets on flat and linear zero curves: the NPV of
calls and puts at seven strikes, from deep in to deep out of the money, and three maturities, by the optimal control
variate (QuantLib's default), Andersen and Piterbarg's, the asymptotic characteristic function's and the angled
contour's, and the optimal one at a scaling of 0.5 and at a damping exponent of -0.3. Every market's κt is at least
0.2, above the vAvg branch at κt = 1e-2 (the S-hes plan's HR7 (xxv)).

A case holds one process {"riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma", "rho"}, the curves
tools/vectors/gen/processes/blackscholesprocess.py's specs, and an engine {"cv" (AnalyticHestonEngine's
ComplexLogFormula integer), "scaling" (or null), "alpha"}. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
HestonModel(HestonProcess(...)), ExponentialFittingHestonEngine(model, cv, scaling, alpha) and the option's NPV. Read
back by tests/NanoQL.Tests/Vectors/PricingEngines/ExponentialFittingHestonEngineVectorTests.cs."""
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
MATURITIES = [91, 365, 1825]
STRIKES = [0.4, 0.7, 0.9, 1.0, 1.1, 1.4, 2.5]
H = ql.AnalyticHestonEngine
ENGINES = [("optimal control variate", {"cv": int(H.OptimalCV), "scaling": None, "alpha": -0.5}),
           ("Andersen-Piterbarg", {"cv": int(H.AndersenPiterbarg), "scaling": None, "alpha": -0.5}),
           ("asymptotic ChF", {"cv": int(H.AsymptoticChF), "scaling": None, "alpha": -0.5}),
           ("angled contour", {"cv": int(H.AngledContour), "scaling": None, "alpha": -0.5}),
           ("optimal, scaling 0.5", {"cv": int(H.OptimalCV), "scaling": 0.5, "alpha": -0.5}),
           ("optimal, alpha -0.3", {"cv": int(H.OptimalCV), "scaling": None, "alpha": -0.3})]


def cases():
    st = v.settings(R)
    out = []
    for pname, s0, v0, kappa, theta, sigma, rho in hp.PARAMETERS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
                    "rho": rho}
            model = ql.HestonModel(hp.process({**spec, "discretization": 0}))
            for ename, espec in ENGINES:
                e = ql.ExponentialFittingHestonEngine(model, espec["cv"], espec["scaling"], espec["alpha"])
                npv = []
                for days in MATURITIES:
                    for kind in (ql.Option.Call, ql.Option.Put):
                        for k in STRIKES:
                            o = ql.VanillaOption(ql.PlainVanillaPayoff(kind, k * s0), ql.EuropeanExercise(R + days))
                            o.setPricingEngine(e)
                            npv.append(v.attempt(lambda: o.NPV()))
                out.append({"name": f"{pname}, {cname} curves, {ename}",
                            "inputs": {"settings": st, "process": spec, "engine": espec, "maturities": MATURITIES,
                                       "strikes": [k * s0 for k in STRIKES]},
                            "expected": {"npv": npv}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/exponentialfittinghestonengine.hpp", cases)
