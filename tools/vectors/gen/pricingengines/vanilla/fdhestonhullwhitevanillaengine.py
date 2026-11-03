# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdHestonHullWhiteVanillaEngine (ql/pricingengines/vanilla/fdhestonhullwhitevanillaengine.hpp,
.cpp), on the evaluation date 15 October 2026: the NPV, delta, gamma and theta it reports.

European and American calls and puts at 0.9, 1 and 1.1 of the spot on tools/vectors/gen/processes/hestonprocess.py's
first set on flat curves with tools/vectors/gen/methods/finitedifferences/operators/fdmhestonhullwhiteop.py's first
Hull-White process at the equity-rate correlation -0.5, on a 20 x 30 x 10 x 7 grid, with and without the control
variate; then on that set: the operator-splitting schemes with and without the control variate, the second
Hull-White process at the correlation 0.3, zero curves, the second Heston set, damping steps, QuantLib's default grid
(50 x 100 x 40 x 20), and an expired option.

Left out: every structure is on the risk-free curve's day counter and reference date (on others QuantLib reads its
times from the risk-free curve, tests/NanoQL.Tests/Divergences/HestonHullWhiteMismatchedTimesTests.cs); cash dividends,
which QuantLib's dividend handler applies on a three-dimensional grid to the lines through the other directions' first
points only (tests/NanoQL.Tests/Divergences/FdmDividendHandler3dTests.cs); every market whose equity grid nanoql
widens, the carry large against the volatility or a strike far from the forwards
(tests/NanoQL.Tests/Divergences/FdHestonHullWhiteHighCarryTests.cs): none here, the carry 0.02 at a volatility of 0.2;
TR-BDF2, which nanoql steps on the operator set over the whole step where QuantLib's BDF2 stage reads it over its
trapezoidal sub-step, and the operator moves in time with the Hull-White model's fitting term
(tests/NanoQL.Tests/Divergences/TrBdf2TimeDependentTests.cs); and the multiple-strikes calculation, the harness's
(tools/vectors/gen/cxx/pricingengines/vanilla/fdhestonhullwhitevanillaengine.py).

The specs are a pricing {"process", "hullWhite", "correlation", "payoff", "exercise", "tGrid", "xGrid", "vGrid",
"rGrid", "dampingSteps", "controlVariate", "scheme"}: the process hestonprocess.py's, the Hull-White process
fdmhestonhullwhiteop.py's, the exercise and the scheme tools/vectors/gen/pricingengines/vanilla/
fdblackscholesvanillaengine.py's. The wheel's calls, by QuantLib-SWIG 1.44's options.i: HestonModel(HestonProcess(...))
and FdHestonHullWhiteVanillaEngine(model, hwProcess, corrEquityShortRate, tGrid, xGrid, vGrid, rGrid, dampingSteps,
controlVariate, schemeDesc), on a VanillaOption; NPV, delta, gamma and theta the option's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/FdHestonHullWhiteVanillaEngineVectorTests.cs."""
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


fhho = _load("fdmhestonhullwhiteop", "methods/finitedifferences/operators/fdmhestonhullwhiteop.py")
fhv = fhho.fhv
fbv, fbs, hp, bsp, bc = fhv.fbv, fhv.fbs, fhv.hp, fhv.bsp, fhv.bc
R = fhv.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
RESULTS = fbv.RESULTS


def pricing_spec(process, payoff, ex, hw=None, correlation=-0.5, t_grid=20, x_grid=30, v_grid=10, r_grid=7,
                 damping=0, control_variate=True, scheme=None):
    return {"process": process, "hullWhite": hw or fhho.HULL_WHITE[0], "correlation": correlation, "payoff": payoff,
            "exercise": ex, "tGrid": t_grid, "xGrid": x_grid, "vGrid": v_grid, "rGrid": r_grid,
            "dampingSteps": damping, "controlVariate": control_variate,
            "scheme": fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Hundsdorfer())}


def results(spec):
    """The engine's results for a spec, each null where QuantLib doesn't provide it, or the refusal."""
    p = hp.process(spec["process"])
    model = ql.HestonModel(p)
    engine = ql.FdHestonHullWhiteVanillaEngine(
        model, fhho.hull_white(p, spec["hullWhite"]), spec["correlation"], spec["tGrid"], spec["xGrid"],
        spec["vGrid"], spec["rGrid"], spec["dampingSteps"], spec["controlVariate"], fbs.scheme(spec["scheme"]))
    option = ql.VanillaOption(bc.payoff(spec["payoff"]), fbv.exercise(spec["exercise"]))
    option.setPricingEngine(engine)
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
    first = fhv.heston_spec(hp.PARAMETERS[0])
    for ename, ex in (("European", european), ("American", american)):
        for kind, kname in ((CALL, "call"), (PUT, "put")):
            for k in (0.9, 1.0, 1.1):
                payoff = bc.payoff_spec("PlainVanilla", kind, round(k * first["s0"], 8))
                for cv in (True, False):
                    add(f"{ename} {kname} at {k}S, control variate {'on' if cv else 'off'}",
                        pricing_spec(first, payoff, ex, control_variate=cv))
    put = bc.payoff_spec("PlainVanilla", PUT, first["s0"])
    for sname in ("Douglas", "CraigSneyd", "ModifiedCraigSneyd", "ModifiedHundsdorfer"):
        for cv in (True, False):
            add(f"European put, {sname}, control variate {'on' if cv else 'off'}",
                pricing_spec(first, put, european, control_variate=cv, scheme=getattr(ql.FdmSchemeDesc, sname)()))
    add("European put, the second Hull-White process at 0.3",
        pricing_spec(first, put, european, hw=fhho.HULL_WHITE[1], correlation=0.3))
    add("American put, zero curves",
        pricing_spec(fhv.heston_spec(hp.PARAMETERS[0], bsp.ZERO_R, bsp.ZERO_Q), put, american))
    second = fhv.heston_spec(hp.PARAMETERS[1])
    add("European call, the second Heston set",
        pricing_spec(second, bc.payoff_spec("PlainVanilla", CALL, second["s0"]), european))
    add("American put, two damping steps", pricing_spec(first, put, american, damping=2))
    add("European put, QuantLib's default grid",
        pricing_spec(first, put, european, t_grid=50, x_grid=100, v_grid=40, r_grid=20))
    add("expired", pricing_spec(first, put, fbv.exercise_spec("European", [-1])))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/fdhestonhullwhitevanillaengine.hpp", cases)
