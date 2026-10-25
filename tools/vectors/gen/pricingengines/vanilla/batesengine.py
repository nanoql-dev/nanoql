# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BatesEngine (ql/pricingengines/vanilla/batesengine.hpp, .cpp), on the evaluation date
15 October 2026: European options under the Bates model, the jumps' term added to the Heston characteristic exponent.

Three Bates models (tools/vectors/gen/models/equity/batesmodel.py's parameter sets) on flat and linear zero curves:
the NPV of calls and puts at five strikes and three maturities, by the engine's
Gauss-Laguerre constructor at QuantLib's default order 144 and at 64, and by its Gauss-Lobatto constructor at a
relative tolerance of 1e-8 and 10,000 evaluations. The det-jump and double-exponential engines, and the engines'
numbers of evaluations, which the wheel doesn't expose, are the harness's
(tools/vectors/gen/cxx/pricingengines/vanilla/batesengine.py).

A case holds one process {"riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma", "rho", "lambda", "nu",
"delta"}, the curves tools/vectors/gen/processes/blackscholesprocess.py's specs, and an engine {"order"} or
{"relTolerance", "maxEvaluations"}. The wheel's calls, by QuantLib-SWIG 1.44's options.i: BatesModel(BatesProcess(...)),
BatesEngine(model, order) and BatesEngine(model, relTolerance, maxEvaluations) and the option's NPV. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/BatesEngineVectorTests.cs."""
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
bp = _load("batesprocess", "processes/batesprocess.py")
bm = _load("batesmodel", "models/equity/batesmodel.py")
R = bsp.R
MATURITIES = [91, 365, 1825]
STRIKES = [60.0, 90.0, 100.0, 110.0, 150.0]
ENGINES = [("Laguerre 144", {"order": 144}), ("Laguerre 64", {"order": 64}),
           ("Lobatto at 1e-8", {"relTolerance": 1e-8, "maxEvaluations": 10000})]


def engine(model, spec):
    if "order" in spec:
        return ql.BatesEngine(model, spec["order"])
    return ql.BatesEngine(model, spec["relTolerance"], spec["maxEvaluations"])


def cases():
    st = v.settings(R)
    out = []
    for name, s0, v0, kappa, theta, sigma, rho, lam, nu, delta in bm.PARAMETERS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
                    "rho": rho, "lambda": lam, "nu": nu, "delta": delta}
            model = ql.BatesModel(bp.process(spec))
            for ename, espec in ENGINES:
                e = engine(model, espec)
                npv = []
                for days in MATURITIES:
                    for kind in (ql.Option.Call, ql.Option.Put):
                        for k in STRIKES:
                            o = ql.VanillaOption(ql.PlainVanillaPayoff(kind, k), ql.EuropeanExercise(R + days))
                            o.setPricingEngine(e)
                            npv.append(v.attempt(lambda: o.NPV()))
                out.append({"name": f"{name}, {cname} curves, {ename}",
                            "inputs": {"settings": st, "process": spec, "engine": espec, "maturities": MATURITIES,
                                       "strikes": STRIKES},
                            "expected": {"npv": npv}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/batesengine.hpp", cases)
