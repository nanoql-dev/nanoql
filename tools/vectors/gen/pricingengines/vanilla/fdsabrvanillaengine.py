# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdSabrVanillaEngine (ql/pricingengines/vanilla/fdsabrvanillaengine.hpp, .cpp), on the
evaluation date 15 October 2026: the NPV it reports, and its delta, gamma and theta, which it doesn't provide.

European, American and Bermudan calls and puts on SABR forwards (testFdmSabrCevPricing's f0 1.2, alpha 0.35, at beta
0.1 and 0.9; testFdmSabrVsVolApproximation's f0 100, alpha 0.35, beta 0.85, nu 0.75, rho 0.85; and BenchOp's f0 0.5,
alpha 0.5, beta 0.5, nu 0.4, rho 0) at strikes below, at and above the forward, on flat curves at 5% and 0, on small
grids (25 x 60 x 15, Hundsdorfer) and one at the default (50 x 400 x 50); then other damping steps, scaling, eps and
schemes; and the parameter refusals of beta 1 and nu below 0. At nu = 0, where QuantLib's engine throws and nanoql's
prices on the CEV engine (the S-van plan's VR4 (xxxi), tests/NanoQL.Tests/Divergences/FdSabrZeroNuTests.cs), no case
is; nor in the band |beta - 1| < 1e-3 (VR10).

The specs are a pricing {"f0", "alpha", "beta", "nu", "rho", "rTS", "payoff", "exercise", "tGrid", "fGrid", "xGrid",
"dampingSteps", "scalingFactor", "eps", "scheme"}, the exercise and the scheme tools/vectors/gen/pricingengines/vanilla/
fdblackscholesvanillaengine.py's. The wheel's calls, by QuantLib-SWIG 1.44's options.i: FdSabrVanillaEngine(f0, alpha,
beta, nu, rho, rTS, tGrid, fGrid, xGrid, dampingSteps, scalingFactor, eps, schemeDesc) on a VanillaOption; NPV, delta,
gamma and theta the option's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/FdSabrVanillaEngineVectorTests.cs."""
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


fbv = _load("fdblackscholesvanillaengine", "pricingengines/vanilla/fdblackscholesvanillaengine.py")
fbs, bsp, bc = fbv.fbs, fbv.bsp, fbv.bc
R = fbv.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
RESULTS = fbv.RESULTS
MODELS = [("beta 0.1", 1.2, 0.35, 0.1, 0.4, 0.25), ("beta 0.9", 1.2, 0.35, 0.9, 0.4, 0.25),
          ("testFdmSabrVsVolApproximation's", 100.0, 0.35, 0.85, 0.75, 0.85), ("BenchOp's", 0.5, 0.5, 0.5, 0.4, 0.0)]


def pricing_spec(model, rate, payoff, ex, grids=(25, 60, 15), damping=0, scaling=1.0, eps=1e-4, scheme=None):
    f0, alpha, beta, nu, rho = model
    return {"f0": f0, "alpha": alpha, "beta": beta, "nu": nu, "rho": rho, "rTS": bsp.flat_spec(R, rate),
            "payoff": payoff, "exercise": ex, "tGrid": grids[0], "fGrid": grids[1], "xGrid": grids[2],
            "dampingSteps": damping, "scalingFactor": scaling, "eps": eps,
            "scheme": fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Hundsdorfer())}


def results(spec):
    curve = ql.YieldTermStructureHandle(bsp.curve(spec["rTS"]))
    option = ql.VanillaOption(bc.payoff(spec["payoff"]), fbv.exercise(spec["exercise"]))
    try:
        option.setPricingEngine(ql.FdSabrVanillaEngine(
            spec["f0"], spec["alpha"], spec["beta"], spec["nu"], spec["rho"], curve, spec["tGrid"], spec["fGrid"],
            spec["xGrid"], spec["dampingSteps"], spec["scalingFactor"], spec["eps"], fbs.scheme(spec["scheme"])))
        option.NPV()
    except RuntimeError:
        return {r: dict(v.THROWS) for r in RESULTS}
    return {r: v.result(lambda r=r: getattr(option, r)()) for r in RESULTS}


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec):
        out.append({"name": name, "inputs": {"settings": st, "pricing": spec}, "expected": results(spec)})

    exercises = [("European", fbv.exercise_spec("European", [365])), ("American", fbv.exercise_spec("American", [365])),
                 ("Bermudan", fbv.exercise_spec("Bermudan", [91, 182, 273, 365]))]
    for name, f0, alpha, beta, nu, rho in MODELS:
        model = (f0, alpha, beta, nu, rho)
        for rate in (0.05, 0.0):
            for kind, ex in exercises:
                for option_type in (CALL, PUT):
                    for k in (0.9, 1.0, 1.1):
                        payoff = bc.payoff_spec("PlainVanilla", option_type, k * f0)
                        add(f"{name} r {rate}, {kind} {'call' if option_type == CALL else 'put'} {k * f0}",
                            pricing_spec(model, rate, payoff, ex))
    model = MODELS[2][1:]
    put = bc.payoff_spec("PlainVanilla", PUT, 100.0)
    european, american = exercises[0][1], exercises[1][1]
    add("the default grid, European put", pricing_spec(model, 0.05, put, european, (50, 400, 50)))
    add("damped, scaled, eps 1e-6", pricing_spec(model, 0.05, put, american, (20, 60, 15), 2, 1.5, 1e-6))
    for dname in ("Douglas", "CraigSneyd", "ModifiedCraigSneyd", "ModifiedHundsdorfer"):
        add(f"American put, {dname}", pricing_spec(model, 0.05, put, american, scheme=getattr(ql.FdmSchemeDesc, dname)()))
    add("beta 1 refused", pricing_spec((100.0, 0.35, 1.0, 0.75, 0.85), 0.05, put, european))
    add("nu below 0 refused", pricing_spec((100.0, 0.35, 0.85, -0.1, 0.85), 0.05, put, european))
    add("expired", pricing_spec(model, 0.05, put, fbv.exercise_spec("European", [-1])))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/fdsabrvanillaengine.hpp", cases)
