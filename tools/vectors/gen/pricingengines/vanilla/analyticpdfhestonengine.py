# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticPDFHestonEngine (ql/pricingengines/vanilla/analyticpdfhestonengine.hpp, .cpp), on
the evaluation date 15 October 2026: European options on any payoff under the Heston model, the payoff integrated
against the density of the log spot at expiry.

Two Heston parameter sets (tools/vectors/gen/processes/hestonprocess.py's equity and far-from-Feller sets), each on one
flat and one linear zero curve serving as both the risk-free and the dividend curve: the NPV of plain-vanilla calls and
puts, cash-or-nothing calls and asset-or-nothing puts at five strikes and two maturities, at QuantLib's default accuracy
and at 1e-8. With no carry, the forward is the spot and QuantLib's window, centred on the forward's mirror, is nanoql's,
centred on the forward (every case with carry is left out), and every κt is above the branch where nanoql's form
departs. The density and distribution members, Pv and Cdf, which the wheel doesn't expose, are the harness's
(tools/vectors/gen/cxx/pricingengines/vanilla/analyticpdfhestonengine.py).

A case holds one process {"riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma", "rho"}, the curves
tools/vectors/gen/processes/blackscholesprocess.py's specs, and an engine {"eps", "maxIterations"}. The wheel's calls,
by QuantLib-SWIG 1.44's options.i: HestonModel(HestonProcess(...)), AnalyticPDFHestonEngine(model, eps, maxIterations)
and the option's NPV. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticPDFHestonEngineVectorTests.cs."""
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
MATURITIES = [182, 730]
STRIKES = [0.7, 0.9, 1.0, 1.1, 1.4]
ENGINES = [("default accuracy", {"eps": 1e-6, "maxIterations": 10000}), ("1e-8", {"eps": 1e-8, "maxIterations": 10000})]
PAYOFFS = [("plain call", "PlainVanilla", ql.Option.Call), ("plain put", "PlainVanilla", ql.Option.Put),
           ("cash-or-nothing call", "CashOrNothing", ql.Option.Call),
           ("asset-or-nothing put", "AssetOrNothing", ql.Option.Put)]


def payoff(kind, option_type, strike):
    if kind == "PlainVanilla":
        return ql.PlainVanillaPayoff(option_type, strike)
    if kind == "CashOrNothing":
        return ql.CashOrNothingPayoff(option_type, strike, 1.0)
    return ql.AssetOrNothingPayoff(option_type, strike)


def cases():
    st = v.settings(R)
    out = []
    for pname, s0, v0, kappa, theta, sigma, rho in (hp.PARAMETERS[0], hp.PARAMETERS[2]):
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_R)), ("zero", (bsp.ZERO_R, bsp.ZERO_R))):
            spec = {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
                    "rho": rho}
            model = ql.HestonModel(hp.process({**spec, "discretization": 0}))
            for ename, espec in ENGINES:
                e = ql.AnalyticPDFHestonEngine(model, espec["eps"], espec["maxIterations"])
                npv = []
                for days in MATURITIES:
                    for _, kind, option_type in PAYOFFS:
                        for k in STRIKES:
                            o = ql.VanillaOption(payoff(kind, option_type, k * s0), ql.EuropeanExercise(R + days))
                            o.setPricingEngine(e)
                            npv.append(v.attempt(lambda: o.NPV()))
                out.append({"name": f"{pname}, {cname} curves, {ename}",
                            "inputs": {"settings": st, "process": spec, "engine": espec, "maturities": MATURITIES,
                                       "payoffs": [[kind, int(t)] for _, kind, t in PAYOFFS],
                                       "strikes": [k * s0 for k in STRIKES]},
                            "expected": {"npv": npv}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticpdfhestonengine.hpp", cases)
