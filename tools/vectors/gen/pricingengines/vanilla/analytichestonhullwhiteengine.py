# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticHestonHullWhiteEngine (ql/pricingengines/vanilla/analytichestonhullwhiteengine.hpp,
.cpp), on the evaluation date 15 October 2026: the analytic Heston engine with a Hull-White short rate independent of
the equity.

tools/vectors/gen/processes/hestonprocess.py's three parameter sets on flat and linear zero curves, each with two
Hull-White models on the risk-free curve (reversion 0.1 and 0.03, volatility 0.01), under both constructors
(Gauss-Laguerre of order 144 and 96, and the adaptive Gauss-Lobatto integration at a relative tolerance of 1e-8 and
10,000 evaluations): the NPVs of calls and puts at three strikes (0.8, 1 and 1.2 of the spot) and three maturities
(half a year, a year and five years).

Left out: every structure is on the risk-free curve's day counter and reference date; and a·t below 1e-2, where
nanoql takes a series for QuantLib's cancelling variance offset
(tests/NanoQL.Tests/Divergences/AnalyticHestonHullWhiteSmallReversionTests.cs): the smallest here is 0.015.

A case holds one process, hestonprocess.py's spec without its discretization, the Hull-White model {"a", "sigma"} on
the risk-free curve, the engine {"integrationOrder"} or {"relTolerance", "maxEvaluations"}, its maturities in days
and its strikes. The wheel's calls, by QuantLib-SWIG 1.44's vanilla.i and shortratemodels.i:
AnalyticHestonHullWhiteEngine(HestonModel(HestonProcess(...)), HullWhite(riskFree, a, sigma), ...) on a
VanillaOption, NPV the option's accessor. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticHestonHullWhiteEngineVectorTests.cs."""
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
MATURITIES = [182, 365, 1825]
STRIKES = [0.8, 1.0, 1.2]
HULL_WHITE = [{"a": 0.1, "sigma": 0.01}, {"a": 0.03, "sigma": 0.01}]
ENGINES = [("Gauss-Laguerre 144", {"integrationOrder": 144}), ("Gauss-Laguerre 96", {"integrationOrder": 96}),
           ("Gauss-Lobatto 1e-8", {"relTolerance": 1e-8, "maxEvaluations": 10000})]


def heston_spec(params, rf, q):
    _, s0, v0, kappa, theta, sigma, rho = params
    return {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
            "rho": rho}


def models(spec, hw):
    process = hp.process({**spec, "discretization": 0})
    hull_white = ql.HullWhite(process.riskFreeRate(), hw["a"], hw["sigma"])
    return ql.HestonModel(process), hull_white


def npvs(engine, spec, maturities, strikes):
    out = []
    for days in maturities:
        for kind in (ql.Option.Call, ql.Option.Put):
            for k in strikes:
                o = ql.VanillaOption(ql.PlainVanillaPayoff(kind, k), ql.EuropeanExercise(R + days))
                o.setPricingEngine(engine)
                out.append(v.attempt(lambda: o.NPV()))
    return out


def cases():
    st = v.settings(R)
    out = []
    for params in hp.PARAMETERS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = heston_spec(params, rf, q)
            strikes = [k * spec["s0"] for k in STRIKES]
            for hw in HULL_WHITE:
                heston, hull_white = models(spec, hw)
                for ename, espec in ENGINES:
                    if "integrationOrder" in espec:
                        engine = ql.AnalyticHestonHullWhiteEngine(heston, hull_white, espec["integrationOrder"])
                    else:
                        engine = ql.AnalyticHestonHullWhiteEngine(heston, hull_white, espec["relTolerance"],
                                                                  espec["maxEvaluations"])
                    out.append({"name": f"{params[0]}, {cname} curves, a {hw['a']}, {ename}",
                                "inputs": {"settings": st, "process": spec, "hullWhite": hw, "engine": espec,
                                           "maturities": MATURITIES, "strikes": strikes},
                                "expected": {"npv": npvs(engine, spec, MATURITIES, strikes)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analytichestonhullwhiteengine.hpp", cases)
