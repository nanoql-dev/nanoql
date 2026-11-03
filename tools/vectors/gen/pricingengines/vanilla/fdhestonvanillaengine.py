# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdHestonVanillaEngine (ql/pricingengines/vanilla/fdhestonvanillaengine.hpp, .cpp), on the
evaluation date 15 October 2026: the NPV, delta, gamma and theta it reports.

European and American calls and puts at the money and away from it, on tools/vectors/gen/processes/hestonprocess.py's
three parameter sets (their sigma/kappa at most 1.5) on flat curves and the first on zero curves, on a 40 x 50 x 25
grid; then on the first set: QuantLib's default grid (100 x 100 x 50, Hundsdorfer), damping steps, every named scheme
description, a mixing factor of 0.8, a cash-or-nothing put, cash dividends, a quanto helper, the two together, a
leverage function (a constant local volatility, and a fixed local volatility surface whose nodes fall between the
grid's times and whose strikes span every spot and forward the engine reads it at), a leverage function with dividends and a quanto helper, and an
expired option.

Left out: every structure is on the risk-free curve's day counter and reference date (on others QuantLib reads them at
the risk-free curve's times, tests/NanoQL.Tests/Divergences/FdHestonMismatchedTimesTests.cs); no case has a mixed sigma
over kappa above 2 (tests/NanoQL.Tests/Divergences/FdHestonSkewFactorTests.cs) or a variance distribution on which the
variance mesher's quantile grid falls back (tests/NanoQL.Tests/Divergences/FdmHestonVarianceMesherFallbackTests.cs); the
leverage surface read outside its strikes past its last time, where QuantLib's interpolator extrapolates and nanoql
holds the volatility; and the multiple-strikes calculation is the harness's
(tools/vectors/gen/cxx/pricingengines/vanilla/fdhestonvanillaengine.py).

The specs are a pricing {"process", "payoff", "exercise", "dividends", "quanto", "tGrid", "xGrid", "vGrid",
"dampingSteps", "scheme", "leverage", "mixingFactor"}: the process hestonprocess.py's, the exercise and the scheme
tools/vectors/gen/pricingengines/vanilla/fdblackscholesvanillaengine.py's, the dividends and the quanto helper
tools/vectors/gen/methods/finitedifferences/meshers/fdmblackscholesmesher.py's, and the leverage null, a
{"type": "LocalConstantVol", "referenceDate", "volatility", "dayCounter"} or
tools/vectors/gen/termstructures/volatility/equityfx/fixedlocalvolsurface.py's spec. The wheel's calls, by
QuantLib-SWIG 1.44's options.i: HestonModel(HestonProcess(...)) and FdHestonVanillaEngine's four constructors (with
and without dividends and a quanto helper), on a VanillaOption; NPV, delta, gamma and theta the option's accessors.
Read back by tests/NanoQL.Tests/Vectors/PricingEngines/FdHestonVanillaEngineVectorTests.cs."""
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
hp = _load("hestonprocess", "processes/hestonprocess.py")
flv = _load("fixedlocalvolsurface", "termstructures/volatility/equityfx/fixedlocalvolsurface.py")
fbs, bsm, bsp, bc = fbv.fbs, fbv.bsm, fbv.bsp, fbv.bc
R = fbv.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
RESULTS = fbv.RESULTS
QE = int(ql.HestonProcess.QuadraticExponentialMartingale)
CONSTANT_LEVERAGE = {"type": "LocalConstantVol", "referenceDate": R.serialNumber(), "volatility": 0.9,
                     "dayCounter": {"type": "Actual365Fixed"}}
SURFACE_LEVERAGE = flv.surface_spec(
    "times", [0.13, 0.37, 0.71, 1.9], [10.0, 30.0, 60.0, 90.0, 110.0, 150.0, 300.0, 1000.0],
    [[1.2, 1.18, 1.15, 1.12], [1.15, 1.12, 1.1, 1.08], [1.08, 1.06, 1.05, 1.04], [1.0, 0.99, 0.98, 0.98],
     [0.96, 0.96, 0.97, 0.97], [0.94, 0.95, 0.96, 0.97], [0.98, 0.98, 0.99, 0.99], [1.02, 1.01, 1.01, 1.0]])


def heston_spec(params, rf=None, q=None):
    _, s0, v0, kappa, theta, sigma, rho = params
    return {"riskFree": rf or bsp.FLAT_R, "dividend": q or bsp.FLAT_Q, "s0": s0, "v0": v0, "kappa": kappa,
            "theta": theta, "sigma": sigma, "rho": rho, "discretization": QE}


def pricing_spec(process, payoff, ex, dividends=(), quanto=None, t_grid=40, x_grid=50, v_grid=25, damping=0,
                 scheme=None, leverage=None, mixing=1.0):
    return {"process": process, "payoff": payoff, "exercise": ex, "dividends": list(dividends), "quanto": quanto,
            "tGrid": t_grid, "xGrid": x_grid, "vGrid": v_grid, "dampingSteps": damping,
            "scheme": fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Hundsdorfer()),
            "leverage": leverage, "mixingFactor": mixing}


def leverage(spec):
    if spec is None:
        return None
    if spec["type"] == "LocalConstantVol":
        return ql.LocalConstantVol(ql.Date(spec["referenceDate"]), spec["volatility"], ql.Actual365Fixed())
    return flv.fixed_local_vol(spec)


def results(spec):
    """The engine's results for a spec, each null where QuantLib doesn't provide it, or the refusal."""
    model = ql.HestonModel(hp.process(spec["process"]))
    head = [model]
    if spec["dividends"]:
        head.append(bsm.dividends(spec["dividends"]))
    helper = bsm.quanto(spec["quanto"])
    if helper is not None:
        head.append(helper)
    tail = [spec["tGrid"], spec["xGrid"], spec["vGrid"], spec["dampingSteps"], fbs.scheme(spec["scheme"])]
    lev = leverage(spec["leverage"])
    if lev is not None or spec["mixingFactor"] != 1.0:
        tail += [lev, spec["mixingFactor"]]
    option = ql.VanillaOption(bc.payoff(spec["payoff"]), fbv.exercise(spec["exercise"]))
    option.setPricingEngine(ql.FdHestonVanillaEngine(*head, *tail))
    try:
        option.NPV()
    except RuntimeError:
        return {r: dict(v.THROWS) for r in RESULTS}
    return {r: v.result(lambda r=r: getattr(option, r)()) for r in RESULTS}


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec):
        out.append({"name": name, "inputs": {"settings": st, "pricing": spec}, "expected": results(spec)})

    european, american = fbv.exercise_spec("European", [365]), fbv.exercise_spec("American", [365])
    sets = [(params, "flat", heston_spec(params)) for params in hp.PARAMETERS]
    sets.append((hp.PARAMETERS[0], "zero", heston_spec(hp.PARAMETERS[0], bsp.ZERO_R, bsp.ZERO_Q)))
    for params, cname, p in sets:
        s0 = params[1]
        for kind, ex in (("European", european), ("American", american)):
            for option_type in (CALL, PUT):
                for strike in (s0, round(1.1 * s0, 8)):
                    payoff = bc.payoff_spec("PlainVanilla", option_type, strike)
                    add(f"{params[0]}, {cname} curves, {kind} {'call' if option_type == CALL else 'put'} {strike}",
                        pricing_spec(p, payoff, ex))
    p = sets[0][2]
    put, call = bc.payoff_spec("PlainVanilla", PUT, 100.0), bc.payoff_spec("PlainVanilla", CALL, 100.0)
    add("default grid, European put", pricing_spec(p, put, european, t_grid=100, x_grid=100, v_grid=50))
    add("default grid, American put", pricing_spec(p, put, american, t_grid=100, x_grid=100, v_grid=50))
    add("two damping steps, American put", pricing_spec(p, put, american, damping=2))
    for dname in fbs.NAMED:
        add(f"American put, {dname}", pricing_spec(p, put, american, t_grid=30, x_grid=40, v_grid=20,
                                                   scheme=getattr(ql.FdmSchemeDesc, dname)()))
    add("mixing factor 0.8, European put", pricing_spec(p, put, european, mixing=0.8))
    add("cash-or-nothing put", pricing_spec(p, bc.payoff_spec("CashOrNothing", PUT, 105.0), european))
    divs = [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)]
    add("dividends, European put", pricing_spec(p, put, european, divs))
    add("dividends, American put", pricing_spec(p, put, american, divs))
    add("dividends, American call", pricing_spec(p, call, american, divs))
    add("quanto, European put", pricing_spec(p, put, european, quanto=bsm.FLAT_QUANTO))
    add("quanto, American call", pricing_spec(p, call, american, quanto=bsm.FLAT_QUANTO))
    add("quanto with dividends, American put", pricing_spec(p, put, american, divs, quanto=bsm.FLAT_QUANTO))
    add("constant leverage, European put", pricing_spec(p, put, european, leverage=CONSTANT_LEVERAGE))
    add("surface leverage, European put", pricing_spec(p, put, european, leverage=SURFACE_LEVERAGE))
    add("surface leverage, American call", pricing_spec(p, call, american, leverage=SURFACE_LEVERAGE))
    add("surface leverage, mixing factor 0.8, two years",
        pricing_spec(p, put, fbv.exercise_spec("European", [730]), leverage=SURFACE_LEVERAGE, mixing=0.8))
    add("surface leverage, quanto with dividends, American put",
        pricing_spec(p, put, american, divs, quanto=bsm.FLAT_QUANTO, leverage=SURFACE_LEVERAGE))
    add("expired", pricing_spec(p, put, fbv.exercise_spec("European", [-1])))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/fdhestonvanillaengine.hpp", cases)
