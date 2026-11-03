# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticH1HWEngine (ql/pricingengines/vanilla/analytich1hwengine.hpp, .cpp), on the
evaluation date 15 October 2026: Grzelak and Oosterlee's H1-HW approximation, the Heston model with a correlated
Hull-White short rate.

Three Heston parameter sets on flat and linear zero curves (QuantLib's hybridhestonhullwhiteprocess test's H1-HW set,
v0 = theta 0.05, kappa 0.3, its vol of variance 0.3, rho -0.3; a variance above its mean, v0 0.08, kappa 1.5, theta
0.04, sigma 0.3, rho -0.7; and tools/vectors/gen/processes/hestonprocess.py's equity set), each with two Hull-White
models on the risk-free curve (reversion 0.1 and 0.03, volatility 0.01) and two equity-rate correlations (0.6, the
test's, and 0.2), under both constructors (Gauss-Laguerre of order 144, and the adaptive Gauss-Lobatto integration at
a relative tolerance of 1e-8 and 10,000 evaluations): the NPVs of calls and puts at three strikes (0.8, 1 and 1.2 of
the spot) and three maturities (a year, two and five years). Then the constructor's refusal of a negative
correlation ("Fourier integration is not stable if the equity interest rate correlation is negative").

Left out: every structure is on the risk-free curve's day counter and reference date; lambda T below 2e-2, where
nanoql evaluates I4 in double-double (tests/NanoQL.Tests/Divergences/AnalyticH1HWSmallReversionTests.cs), the smallest
here 0.03; and every set whose fit E[sqrt(v_t)] ~ a + b exp(-ct) has no decaying exponential, (Lambda(1) - a)/b
outside (0, 1), where nanoql takes the fit's limits (tests/NanoQL.Tests/Divergences/AnalyticH1HWBandTests.cs): each
set here is on the side 8 kappa theta / sigma^2 > 1, its ratio computed below as QuantLib's Fj_Helper computes it, and
asserted inside.

A case holds one process, hestonprocess.py's spec without its discretization, the Hull-White model {"a", "sigma"} on
the risk-free curve, rhoSr, the engine {"integrationOrder"} or {"relTolerance", "maxEvaluations"}, its maturities in
days and its strikes. The wheel's calls, by QuantLib-SWIG 1.44's vanilla.i and shortratemodels.i:
AnalyticH1HWEngine(HestonModel(HestonProcess(...)), HullWhite(riskFree, a, sigma), rhoSr, ...) on a VanillaOption, NPV
the option's accessor. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticH1HWEngineVectorTests.cs."""
import importlib.util
import math
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
ahhw = _load("analytichestonhullwhiteengine", "pricingengines/vanilla/analytichestonhullwhiteengine.py")
R = bsp.R
PARAMETERS = [
    ("H1-HW test's set", 100.0, 0.05, 0.3, 0.05, 0.3, -0.3),
    ("variance above its mean", 100.0, 0.08, 1.5, 0.04, 0.3, -0.7),
    hp.PARAMETERS[0],
]
RHO_SR = [0.6, 0.2]
MATURITIES = [365, 730, 1825]
ENGINES = [("Gauss-Laguerre 144", {"integrationOrder": 144}),
           ("Gauss-Lobatto 1e-8", {"relTolerance": 1e-8, "maxEvaluations": 10000})]


def fit_ratio(v0, kappa, theta, sigma):
    """QuantLib's Fj_Helper's (Lambda(1) - a)/b on the side 8 kappa theta / sigma^2 > 1 (analytich1hwengine.cpp)."""
    gamma2 = sigma * sigma
    assert 8.0 * kappa * theta / gamma2 > 1.0
    d = 4.0 * kappa * theta / gamma2
    c1 = gamma2 * (1.0 - math.exp(-kappa)) / (4.0 * kappa)
    lam1 = 4.0 * kappa * v0 * math.exp(-kappa) / (gamma2 * (1.0 - math.exp(-kappa)))
    approx = math.sqrt(c1 * (lam1 - 1.0) + c1 * d * (1.0 + 1.0 / (2.0 * (d + lam1))))
    a = math.sqrt(theta - gamma2 / (8.0 * kappa))
    b = math.sqrt(v0) - a
    return (approx - a) / b


def cases():
    st = v.settings(R)
    out = []
    for params in PARAMETERS:
        _, s0, v0, kappa, theta, sigma, rho = params
        ratio = fit_ratio(v0, kappa, theta, sigma)
        assert 0.0 < ratio < 1.0, (params[0], ratio)
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = ahhw.heston_spec(params, rf, q)
            strikes = [k * s0 for k in ahhw.STRIKES]
            for hw in ahhw.HULL_WHITE:
                heston, hull_white = ahhw.models(spec, hw)
                for rho_sr in RHO_SR:
                    for ename, espec in ENGINES:
                        if "integrationOrder" in espec:
                            engine = ql.AnalyticH1HWEngine(heston, hull_white, rho_sr, espec["integrationOrder"])
                        else:
                            engine = ql.AnalyticH1HWEngine(heston, hull_white, rho_sr, espec["relTolerance"],
                                                           espec["maxEvaluations"])
                        out.append({"name": f"{params[0]}, {cname} curves, a {hw['a']}, rhoSr {rho_sr}, {ename}",
                                    "inputs": {"settings": st, "process": spec, "hullWhite": hw, "rhoSr": rho_sr,
                                               "engine": espec, "maturities": MATURITIES, "strikes": strikes},
                                    "expected": {"npv": ahhw.npvs(engine, spec, MATURITIES, strikes)}})
    spec = ahhw.heston_spec(PARAMETERS[0], bsp.FLAT_R, bsp.FLAT_Q)
    heston, hull_white = ahhw.models(spec, ahhw.HULL_WHITE[0])
    refused = v.attempt(lambda: ql.AnalyticH1HWEngine(heston, hull_white, -0.4, 144) and None)
    out.append({"name": "a negative equity-rate correlation",
                "inputs": {"settings": st, "process": spec, "hullWhite": ahhw.HULL_WHITE[0], "rhoSr": -0.4,
                           "engine": {"integrationOrder": 144}},
                "expected": {"engine": refused}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analytich1hwengine.hpp", cases)
