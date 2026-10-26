# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Fd2dBlackScholesVanillaEngine (ql/pricingengines/basket/fd2dblackscholesvanillaengine.hpp,
.cpp), on the evaluation date 15 October 2026: the two-dimensional finite-difference engine's NPV, delta, gamma and
theta for European and American spread, minimum, maximum and average basket options on two Black-Scholes processes, at
correlations of 0.5 and -0.3, on flat curves and constant volatilities, on zero curves with a constant volatility and a
surface, with the local volatility (with and without an illegal-local-volatility overwrite), under Hundsdorfer's scheme
(the default), Douglas's, Craig-Sneyd's with damping steps, and implicit Euler and TR-BDF2, whose Krylov solves are
measured, not pinned (the S-exo plan's ED9).

Every structure of both processes is on Act/365 (Fixed) from the evaluation date and the two share one risk-free curve:
on structures dated or day-counted apart QuantLib reads the second process's at the first's risk-free times
(tests/NanoQL.Tests/Divergences/Fd2dMismatchedTimesTests.cs), and on two risk-free curves its price discounts at the
second's (tests/NanoQL.Tests/Divergences/Fd2dTwoRiskFreeCurvesTests.cs).

The specs are tools/vectors/gen/processes/blackscholesprocess.py's processes and tools/vectors/gen/instruments/
basketoption.py's options; an engine {"processes", "correlation", "xGrid", "yGrid", "tGrid", "dampingSteps", "scheme"
(tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's), "localVol", "illegalLocalVolOverwrite"
(null for QuantLib's default, -Null<Real>())}. The wheel's calls, by QuantLib-SWIG 1.44's basketoptions.i:
Fd2dBlackScholesVanillaEngine(p1, p2, correlation, xGrid, yGrid, tGrid, dampingSteps, schemeDesc, localVol,
illegalLocalVolOverwrite), the C++ constructor; BasketOption's NPV, delta, gamma and theta, the C++ members. Read back
by tests/NanoQL.Tests/Vectors/PricingEngines/Basket/Fd2dBlackScholesVanillaEngineVectorTests.cs."""
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


bo = _load("basketoption", "instruments/basketoption.py")
fbs = _load("fdmbackwardsolver", "methods/finitedifferences/solvers/fdmbackwardsolver.py")
bc, bsp = bo.bc, bo.bsp
bvts = fbs.bsm.bvts
R = bo.R
CALL, PUT = bo.CALL, bo.PUT
NULL = ql.nullDouble()

FLAT_PAIR = [bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q),
             bsp.process_spec("BlackScholesMertonProcess", 95.0, bsp.FLAT_R, bvts.vol_spec_constant(R, 0.3),
                              bsp.flat_spec(R, 0.01))]
ZERO_PAIR = [bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.CONSTANT, bsp.ZERO_Q),
             bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.SURFACE, bsp.ZERO_Q)]
SURFACE_PAIR = [bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.SURFACE, bsp.ZERO_Q),
                bsp.process_spec("GeneralizedBlackScholesProcess", 105.0, bsp.ZERO_R, bsp.SURFACE, bsp.FLAT_Q)]


def engine_spec(processes, correlation, x_grid=21, y_grid=21, t_grid=20, damping=0, scheme=None, local_vol=False,
                overwrite=None):
    return {"processes": processes, "correlation": correlation, "xGrid": x_grid, "yGrid": y_grid, "tGrid": t_grid,
            "dampingSteps": damping, "scheme": fbs.scheme_spec(scheme or ql.FdmSchemeDesc.Hundsdorfer()),
            "localVol": local_vol, "illegalLocalVolOverwrite": overwrite}


def results(option_s, engine_s):
    option, keep = bo.option(option_s)
    p1, p2 = (bsp.process(p) for p in engine_s["processes"])
    overwrite = -NULL if engine_s["illegalLocalVolOverwrite"] is None else engine_s["illegalLocalVolOverwrite"]
    option.setPricingEngine(ql.Fd2dBlackScholesVanillaEngine(
        p1, p2, engine_s["correlation"], engine_s["xGrid"], engine_s["yGrid"], engine_s["tGrid"],
        engine_s["dampingSteps"], fbs.scheme(engine_s["scheme"]), engine_s["localVol"], overwrite))
    return {"NPV": option.NPV(), "delta": option.delta(), "gamma": option.gamma(), "theta": option.theta()}


def cases():
    st = v.settings(R)
    european, american = bo.exercise_spec("European", 365), bo.exercise_spec("American", 365)
    spread_call = bo.payoff_spec("Spread", bc.payoff_spec("PlainVanilla", CALL, 5.0))
    spread_put = bo.payoff_spec("Spread", bc.payoff_spec("PlainVanilla", PUT, 5.0))
    min_call = bo.payoff_spec("Min", bc.payoff_spec("PlainVanilla", CALL, 90.0))
    max_put = bo.payoff_spec("Max", bc.payoff_spec("PlainVanilla", PUT, 110.0))
    avg_call = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", CALL, 100.0), weights=[0.5, 0.5])
    avg_put = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", PUT, 100.0), n=2)
    setups = [
        ("flat, spread call", spread_call, european, engine_spec(FLAT_PAIR, 0.5)),
        ("flat, spread put", spread_put, european, engine_spec(FLAT_PAIR, 0.5)),
        ("flat, American spread put", spread_put, american, engine_spec(FLAT_PAIR, 0.5)),
        ("flat, minimum call", min_call, european, engine_spec(FLAT_PAIR, -0.3)),
        ("flat, American maximum put", max_put, american, engine_spec(FLAT_PAIR, -0.3)),
        ("flat, average call", avg_call, european, engine_spec(FLAT_PAIR, 0.5)),
        ("flat, American average put", avg_put, american, engine_spec(FLAT_PAIR, 0.5)),
        ("zero curves, spread call", spread_call, european, engine_spec(ZERO_PAIR, 0.5)),
        ("zero curves, American spread put", spread_put, american, engine_spec(ZERO_PAIR, -0.3)),
        ("zero curves, average call, Douglas", avg_call, european,
         engine_spec(ZERO_PAIR, 0.5, scheme=ql.FdmSchemeDesc.Douglas())),
        ("zero curves, maximum put, Craig-Sneyd, damped", max_put, european,
         engine_spec(ZERO_PAIR, 0.5, scheme=ql.FdmSchemeDesc.CraigSneyd(), damping=2)),
        ("flat, spread call, implicit Euler", spread_call, european,
         engine_spec(FLAT_PAIR, 0.5, scheme=ql.FdmSchemeDesc.ImplicitEuler())),
        ("flat, American average put, TR-BDF2", avg_put, american,
         engine_spec(FLAT_PAIR, 0.5, scheme=ql.FdmSchemeDesc.TrBDF2())),
        ("local volatility, spread call", spread_call, european,
         engine_spec(SURFACE_PAIR, 0.5, x_grid=15, y_grid=15, t_grid=10, local_vol=True)),
        ("local volatility, American average put", avg_put, american,
         engine_spec(SURFACE_PAIR, -0.3, x_grid=15, y_grid=15, t_grid=10, local_vol=True)),
        ("local volatility, overwrite 0.25, minimum call", min_call, european,
         engine_spec(SURFACE_PAIR, 0.5, x_grid=15, y_grid=15, t_grid=10, local_vol=True, overwrite=0.25)),
    ]
    out = []
    for name, payoff, ex, engine in setups:
        option = bo.option_spec(payoff, ex)
        out.append({"name": name, "inputs": {"settings": st, "option": option, "engine": engine},
                    "expected": results(option, engine)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/fd2dblackscholesvanillaengine.hpp", cases)
