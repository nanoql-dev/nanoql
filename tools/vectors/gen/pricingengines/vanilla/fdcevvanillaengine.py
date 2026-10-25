# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdCEVVanillaEngine (ql/pricingengines/vanilla/fdcevvanillaengine.hpp, .cpp), on the
evaluation date 15 October 2026: the NPV, delta, gamma and theta it reports.

European, American and Bermudan calls and puts on CEV forwards of 100 and 2.1 at strikes below, at and above them, for
elasticities -0.5, 0.45, 0.9 and 1.45 (delta below and above 2), volatility parameters at 30%, on flat curves at 3%
and -1%, on the default grid (50 x 400, Douglas); then testFdmCevOp's grid (100 x 1000, one damping step, eps 1e-6),
other scaling factors, schemes and a cash-or-nothing payoff; and an expired option. The band |beta - 1| < 1e-3, where
nanoql's mesher and upper boundary take the lognormal limit (the S-van plan's VR10,
tests/NanoQL.Tests/Divergences/FdCevNearOneTests.cs), is none of the grid's; an exercise on or before the curve's
date, which nanoql refuses and QuantLib throws on, isn't in it either; and the cases where QuantLib's density inverse
steps below zero, which nanoql fixes (tests/NanoQL.Tests/Divergences/CevDensityBelowZeroTests.cs), are left out by
Boost's message.

The specs are a pricing {"f0", "alpha", "beta", "rTS", "payoff", "exercise", "tGrid", "xGrid", "dampingSteps",
"scalingFactor", "eps", "scheme"}, the exercise and the scheme tools/vectors/gen/pricingengines/vanilla/
fdblackscholesvanillaengine.py's. The wheel's calls, by QuantLib-SWIG 1.44's options.i: FdCEVVanillaEngine(f0, alpha,
beta, rTS, tGrid, xGrid, dampingSteps, scalingFactor, eps, schemeDesc) on a VanillaOption; NPV, delta, gamma and theta
the option's accessors. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/FdCEVVanillaEngineVectorTests.cs."""
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
cm = _load("fdcev1dmesher", "methods/finitedifferences/meshers/fdmcev1dmesher.py")
fbs, bsp, bc = fbv.fbs, fbv.bsp, fbv.bc
R = fbv.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
RESULTS = fbv.RESULTS


def pricing_spec(f0, alpha, beta, rate, payoff, ex, t_grid=50, x_grid=400, damping=0, scaling=1.0, eps=1e-4,
                 scheme=None):
    return {"f0": f0, "alpha": alpha, "beta": beta, "rTS": bsp.flat_spec(R, rate), "payoff": payoff, "exercise": ex,
            "tGrid": t_grid, "xGrid": x_grid, "dampingSteps": damping, "scalingFactor": scaling, "eps": eps,
            "scheme": fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Douglas())}


def results(spec):
    """The engine's results for a spec, each null where QuantLib doesn't provide it, the refusal, or None to leave
    the case out."""
    curve = ql.YieldTermStructureHandle(bsp.curve(spec["rTS"]))
    option = ql.VanillaOption(bc.payoff(spec["payoff"]), fbv.exercise(spec["exercise"]))
    option.setPricingEngine(ql.FdCEVVanillaEngine(
        spec["f0"], spec["alpha"], spec["beta"], curve, spec["tGrid"], spec["xGrid"], spec["dampingSteps"],
        spec["scalingFactor"], spec["eps"], fbs.scheme(spec["scheme"])))
    try:
        option.NPV()
    except RuntimeError as e:
        if cm.BELOW_ZERO in str(e):
            return None
        return {r: dict(v.THROWS) for r in RESULTS}
    return {r: v.result(lambda r=r: getattr(option, r)()) for r in RESULTS}


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec):
        expected = results(spec)
        if expected is not None:
            out.append({"name": name, "inputs": {"settings": st, "pricing": spec}, "expected": expected})

    exercises = [("European", fbv.exercise_spec("European", [365])), ("American", fbv.exercise_spec("American", [365])),
                 ("Bermudan", fbv.exercise_spec("Bermudan", [91, 182, 273, 365]))]
    for f0 in (100.0, 2.1):
        for beta in (-0.5, 0.45, 0.9, 1.45):
            alpha = 0.3 * f0 ** (1.0 - beta)
            for rate in (0.03, -0.01) if f0 == 100.0 else (0.15,):
                for kind, ex in exercises:
                    for option_type in (CALL, PUT):
                        for k in (0.9, 1.0, 1.15):
                            payoff = bc.payoff_spec("PlainVanilla", option_type, k * f0)
                            add(f"f0 {f0} beta {beta} r {rate}, {kind} {'call' if option_type == CALL else 'put'} "
                                f"{k * f0}", pricing_spec(f0, alpha, beta, rate, payoff, ex))
    alpha = 0.75
    put = bc.payoff_spec("PlainVanilla", PUT, 2.3)
    european, american = exercises[0][1], exercises[1][1]
    add("testFdmCevOp's grid, put", pricing_spec(2.1, alpha, 0.45, 0.15, put, european, 100, 1000, 1, 1.0, 1e-6))
    add("testFdmCevOp's grid, call", pricing_spec(2.1, alpha, 0.45, 0.15, bc.payoff_spec("PlainVanilla", CALL, 2.3),
                                                  european, 100, 1000, 1, 1.0, 1e-6))
    add("scaling 1.5, American put", pricing_spec(2.1, alpha, 0.45, 0.15, put, american, 60, 200, 0, 1.5))
    for dname in fbs.NAMED:
        add(f"American put, {dname}", pricing_spec(2.1, alpha, 0.45, 0.15, put, american, 40, 120,
                                                   scheme=getattr(ql.FdmSchemeDesc, dname)()))
    add("cash-or-nothing put", pricing_spec(2.1, alpha, 0.45, 0.15, bc.payoff_spec("CashOrNothing", PUT, 2.3), european))
    add("expired", pricing_spec(2.1, alpha, 0.45, 0.15, put, fbv.exercise_spec("European", [-1])))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/fdcevvanillaengine.hpp", cases)
