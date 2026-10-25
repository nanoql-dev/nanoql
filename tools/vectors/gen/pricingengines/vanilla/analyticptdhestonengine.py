# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticPTDHestonEngine (ql/pricingengines/vanilla/analyticptdhestonengine.hpp, .cpp), on
the evaluation date 15 October 2026: European options under the piecewise time-dependent Heston model.

Three models on flat and linear zero curves: constant parameters on a grid of two steps over 20 years (QuantLib's
testAnalyticPiecewiseTimeDependent form), σ piecewise on [0, 0.25, 0.75, 10] (testPiecewiseTimeDependentComparison's),
and every parameter piecewise on [0, 0.5, 1.5, 4] with κ, θ, σ and ρ moving at each step. The NPV of calls and puts at
five strikes and three maturities, the last at 3 years inside every grid, by Gatheral's form at the Gauss-Laguerre
orders 144 and 64 and on Gauss-Lobatto at a relative tolerance of 1e-10, and by Andersen and Piterbarg's form on
Gauss-Laguerre of order 128 and on the discrete trapezoid rule of 128 points at an epsilon of 1e-10. The
characteristic function's members, ChF and LnChF, which the wheel doesn't expose, are the harness's
(tools/vectors/gen/cxx/pricingengines/vanilla/analyticptdhestonengine.py).

A case holds one model {"riskFree", "dividend", "s0", "v0", "grid" (the mandatory times), "pTimes" (the parameters'
step times, empty for constants), "theta", "kappa", "sigma", "rho" (each a list of values, one per step, or one for a
constant)} and an engine {"order"}, {"relTolerance", "maxEvaluations"} or {"cpxLog" (0 Gatheral, 1 Andersen-Piterbarg),
"integration" ["laguerre", n] or ["discreteTrapezoid", n], "epsilon"}. The wheel's calls, by QuantLib-SWIG 1.44's
calibratedmodel.i and options.i: PiecewiseTimeDependentHestonModel(riskFreeRate, dividendYield, s0, v0, theta, kappa,
sigma, rho, TimeGrid(times)) on ConstantParameter or PiecewiseConstantParameter, AnalyticPTDHestonEngine's three
constructors and the option's NPV. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticPTDHestonEngineVectorTests.cs."""
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
R = bsp.R
MATURITIES = [91, 365, 1095]
STRIKES = [70.0, 90.0, 100.0, 110.0, 140.0]
MODELS = [
    ("constant on two steps", {"s0": 100.0, "v0": 0.1, "grid": [20.0], "pTimes": [], "theta": [0.09], "kappa": [3.16],
                               "sigma": [0.8], "rho": [-0.8]}),
    ("sigma piecewise", {"s0": 100.0, "v0": 0.1, "grid": [0.25, 0.75, 10.0], "pTimes": [0.25, 0.75], "theta": [0.1],
                         "kappa": [1.0], "sigma": [0.30, 0.15, 1.25], "rho": [-0.75]}),
    ("every parameter piecewise", {"s0": 100.0, "v0": 0.04, "grid": [0.5, 1.5, 4.0], "pTimes": [0.5, 1.5],
                                   "theta": [0.04, 0.09, 0.06], "kappa": [1.5, 0.6, 3.0], "sigma": [0.3, 0.6, 0.45],
                                   "rho": [-0.7, -0.3, -0.85]}),
]
ENGINES = [("Gatheral, Laguerre 144", {"order": 144}), ("Gatheral, Laguerre 64", {"order": 64}),
           ("Gatheral, Lobatto 1e-10", {"relTolerance": 1e-10, "maxEvaluations": 100000}),
           ("Andersen-Piterbarg, Laguerre 128", {"cpxLog": 1, "integration": ["laguerre", 128], "epsilon": 1e-10}),
           ("Andersen-Piterbarg, discrete trapezoid 128",
            {"cpxLog": 1, "integration": ["discreteTrapezoid", 128], "epsilon": 1e-10})]


def parameter(values, p_times, constraint):
    if len(values) == 1:
        return ql.ConstantParameter(values[0], constraint)
    p = ql.PiecewiseConstantParameter(p_times, constraint)
    for i, x in enumerate(values):
        p.setParam(i, x)
    return p


def model(spec):
    t = spec["pTimes"]
    return ql.PiecewiseTimeDependentHestonModel(
        ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])), ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])),
        ql.QuoteHandle(ql.SimpleQuote(spec["s0"])), spec["v0"], parameter(spec["theta"], t, ql.PositiveConstraint()),
        parameter(spec["kappa"], t, ql.PositiveConstraint()), parameter(spec["sigma"], t, ql.PositiveConstraint()),
        parameter(spec["rho"], t, ql.BoundaryConstraint(-1.0, 1.0)), ql.TimeGrid(spec["grid"]))


def engine(m, spec):
    if "order" in spec:
        return ql.AnalyticPTDHestonEngine(m, spec["order"])
    if "relTolerance" in spec:
        return ql.AnalyticPTDHestonEngine(m, spec["relTolerance"], spec["maxEvaluations"])
    kind, n = spec["integration"]
    itg = ql.FourierIntegration.gaussLaguerre(n) if kind == "laguerre" else ql.FourierIntegration.discreteTrapezoid(n)
    return ql.AnalyticPTDHestonEngine(m, spec["cpxLog"], itg, spec["epsilon"])


def cases():
    st = v.settings(R)
    out = []
    for mname, mspec in MODELS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = {"riskFree": rf, "dividend": q, **mspec}
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
                out.append({"name": f"{mname}, {cname} curves, {ename}",
                            "inputs": {"settings": st, "model": spec, "engine": espec, "maturities": MATURITIES,
                                       "strikes": STRIKES},
                            "expected": {"npv": npv}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticptdhestonengine.hpp", cases)
