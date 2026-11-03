# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's COSHestonEngine (ql/pricingengines/vanilla/coshestonengine.hpp, .cpp), on the evaluation
date 15 October 2026: European options under the Heston model by Fang and Oosterlee's cosine series.

tools/vectors/gen/processes/hestonprocess.py's three Heston parameter sets on flat and linear zero curves: the NPV of
calls and puts at five strikes and three maturities from 18 months to five years, at QuantLib's default truncation and
order (L 16, N 200) and at (12, 75) and (25, 600). Every market's κt is at least 1.2, at or above every cumulant's
cut, so the cumulants, which set the truncation range, are QuantLib's own double arithmetic (§2.10 leaves out the κt
below the cuts), and every σ is far above the small-σ branch. The members, ChF, C1 to C4, Mu, Var, Skew and Kurtosis,
which the wheel doesn't expose, are the harness's (tools/vectors/gen/cxx/pricingengines/vanilla/coshestonengine.py).

A case holds one process {"riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma", "rho"}, the curves
tools/vectors/gen/processes/blackscholesprocess.py's specs, and an engine {"L", "N"}. The wheel's calls, by
QuantLib-SWIG 1.44's options.i: HestonModel(HestonProcess(...)), COSHestonEngine(model, L, N) and the option's NPV.
Read back by tests/NanoQL.Tests/Vectors/PricingEngines/COSHestonEngineVectorTests.cs."""
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
MATURITIES = [548, 1095, 1825]
STRIKES = [0.6, 0.9, 1.0, 1.1, 1.5]
ENGINES = [("L 16, N 200", {"L": 16.0, "N": 200}), ("L 12, N 75", {"L": 12.0, "N": 75}),
           ("L 25, N 600", {"L": 25.0, "N": 600})]


def cases():
    st = v.settings(R)
    out = []
    for pname, s0, v0, kappa, theta, sigma, rho in hp.PARAMETERS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
                    "rho": rho}
            model = ql.HestonModel(hp.process({**spec, "discretization": 0}))
            for ename, espec in ENGINES:
                e = ql.COSHestonEngine(model, espec["L"], espec["N"])
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
    v.main(__file__, "ql/pricingengines/vanilla/coshestonengine.hpp", cases)
