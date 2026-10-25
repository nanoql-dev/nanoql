# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdBatesVanillaEngine (ql/pricingengines/vanilla/fdbatesvanillaengine.hpp, .cpp), on the
evaluation date 15 October 2026: the NPV, delta, gamma and theta it reports.

European and American calls and puts at the money and away from it, on tools/vectors/gen/models/equity/batesmodel.py's
three parameter sets (their sigma/kappa at most 1) on flat curves and the second on zero curves, on a 30 x 40 x 20
grid; then on the second set: QuantLib's default grid (100 x 100 x 50, Hundsdorfer), damping steps, every named scheme
description, a cash-or-nothing put, cash dividends, and an expired option.

Left out (the S-hes plan's §2.10): every structure is on the risk-free curve's day counter and reference date (on
others QuantLib reads them at the risk-free curve's times, tests/NanoQL.Tests/Divergences/
FdHestonMismatchedTimesTests.cs); no case has a sigma over kappa above 2 (HR9) or a variance distribution on which the
variance mesher's quantile grid falls back (HR8).

The specs are a pricing {"process", "payoff", "exercise", "dividends", "tGrid", "xGrid", "vGrid", "dampingSteps",
"scheme"}: the process batesmodel.py's, the exercise and the scheme
tools/vectors/gen/pricingengines/vanilla/fdblackscholesvanillaengine.py's, the dividends
tools/vectors/gen/methods/finitedifferences/meshers/fdmblackscholesmesher.py's. The wheel's calls, by QuantLib-SWIG
1.44's options.i: BatesModel(BatesProcess(...)) and FdBatesVanillaEngine's two constructors (with and without
dividends), on a VanillaOption; NPV, delta, gamma and theta the option's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/FdBatesVanillaEngineVectorTests.cs."""
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
bp = _load("batesprocess", "processes/batesprocess.py")
bm = _load("batesmodel", "models/equity/batesmodel.py")
fbs, bsm, bsp, bc = fbv.fbs, fbv.bsm, fbv.bsp, fbv.bc
R = fbv.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
RESULTS = fbv.RESULTS


def bates_spec(params, rf=None, q=None):
    _, s0, v0, kappa, theta, sigma, rho, lam, nu, delta = params
    return {"riskFree": rf or bsp.FLAT_R, "dividend": q or bsp.FLAT_Q, "s0": s0, "v0": v0, "kappa": kappa,
            "theta": theta, "sigma": sigma, "rho": rho, "lambda": lam, "nu": nu, "delta": delta}


def pricing_spec(process, payoff, ex, dividends=(), t_grid=30, x_grid=40, v_grid=20, damping=0, scheme=None):
    return {"process": process, "payoff": payoff, "exercise": ex, "dividends": list(dividends), "tGrid": t_grid,
            "xGrid": x_grid, "vGrid": v_grid, "dampingSteps": damping,
            "scheme": fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Hundsdorfer())}


def results(spec):
    """The engine's results for a spec, each null where QuantLib doesn't provide it, or the refusal."""
    model = ql.BatesModel(bp.process(spec["process"]))
    head = [model]
    if spec["dividends"]:
        head.append(bsm.dividends(spec["dividends"]))
    tail = [spec["tGrid"], spec["xGrid"], spec["vGrid"], spec["dampingSteps"], fbs.scheme(spec["scheme"])]
    option = ql.VanillaOption(bc.payoff(spec["payoff"]), fbv.exercise(spec["exercise"]))
    option.setPricingEngine(ql.FdBatesVanillaEngine(*head, *tail))
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
    sets = [(params, "flat", bates_spec(params)) for params in bm.PARAMETERS]
    sets.append((bm.PARAMETERS[1], "zero", bates_spec(bm.PARAMETERS[1], bsp.ZERO_R, bsp.ZERO_Q)))
    for params, cname, p in sets:
        s0 = params[1]
        for kind, ex in (("European", european), ("American", american)):
            for option_type in (CALL, PUT):
                for strike in (s0, round(1.1 * s0, 8)):
                    payoff = bc.payoff_spec("PlainVanilla", option_type, strike)
                    add(f"{params[0]}, {cname} curves, {kind} {'call' if option_type == CALL else 'put'} {strike}",
                        pricing_spec(p, payoff, ex))
    p = sets[1][2]
    put, call = bc.payoff_spec("PlainVanilla", PUT, 100.0), bc.payoff_spec("PlainVanilla", CALL, 100.0)
    add("default grid, European put", pricing_spec(p, put, european, t_grid=100, x_grid=100, v_grid=50))
    add("two damping steps, American put", pricing_spec(p, put, american, damping=2))
    for dname in fbs.NAMED:
        add(f"American put, {dname}", pricing_spec(p, put, american, t_grid=20, x_grid=30, v_grid=15,
                                                   scheme=getattr(ql.FdmSchemeDesc, dname)()))
    add("cash-or-nothing put", pricing_spec(p, bc.payoff_spec("CashOrNothing", PUT, 105.0), european))
    divs = [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)]
    add("dividends, European put", pricing_spec(p, put, european, divs))
    add("dividends, American put", pricing_spec(p, put, american, divs))
    add("dividends, American call", pricing_spec(p, call, american, divs))
    add("expired", pricing_spec(p, put, fbv.exercise_spec("European", [-1])))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/fdbatesvanillaengine.hpp", cases)
