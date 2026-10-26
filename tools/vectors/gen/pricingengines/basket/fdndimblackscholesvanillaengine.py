# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdndimBlackScholesVanillaEngine (ql/pricingengines/basket/fdndimblackscholesvanillaengine.hpp,
.cpp), on the evaluation date 15 October 2026: the n-dimensional finite-difference engine's NPV for European and
American average, spread, minimum and maximum basket options on one, two and three Black-Scholes processes, on one grid
size scaled by the eigenvalues and on a grid size per direction, under Douglas's scheme (QuantLib's C++ default, given
explicitly: the wheel's default is Hundsdorfer's), Hundsdorfer's, and implicit Euler and TR-BDF2, whose Krylov solves
are measured, not pinned (the S-exo plan's ED9).

Every structure is on Act/365 (Fixed) from the evaluation date and every process shares one risk-free curve; each
covariance is positive definite: on structures dated or day-counted apart QuantLib reads them at the first process's
risk-free times (tests/NanoQL.Tests/Divergences/FdNdimMismatchedTimesTests.cs), on two risk-free curves its American
branch builds every forward on the first's and its European branch refuses
(tests/NanoQL.Tests/Divergences/FdNdimTwoRiskFreeCurvesTests.cs), and on a singular covariance its mesh collapses
(tests/NanoQL.Tests/Divergences/FdNdimSingularCovarianceTests.cs).

The specs are tools/vectors/gen/instruments/basketoption.py's options and an engine {"processes", "rho", "xGrids" (one,
or one per process), "tGrid", "dampingSteps", "scheme"}. The wheel's calls, by QuantLib-SWIG 1.44's basketoptions.i:
FdndimBlackScholesVanillaEngine(processes, rho, xGrid or dim, tGrid, dampingSteps, schemeDesc), the C++ constructors;
BasketOption's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Basket/FdndimBlackScholesVanillaEngineVectorTests.cs."""
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


fd2 = _load("fd2dblackscholesvanillaengine", "pricingengines/basket/fd2dblackscholesvanillaengine.py")
bo, fbs, bc, bsp, bvts = fd2.bo, fd2.fbs, fd2.bc, fd2.bsp, fd2.bvts
R = bo.R
CALL, PUT = bo.CALL, bo.PUT

THREE = [bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q),
         bsp.process_spec("BlackScholesMertonProcess", 90.0, bsp.FLAT_R, bvts.vol_spec_constant(R, 0.3),
                          bsp.flat_spec(R, 0.01)),
         bsp.process_spec("GeneralizedBlackScholesProcess", 110.0, bsp.FLAT_R, bvts.vol_spec_constant(R, 0.25),
                          bsp.flat_spec(R, 0.03))]
RHO3 = [[1.0, 0.3, -0.2], [0.3, 1.0, 0.4], [-0.2, 0.4, 1.0]]


def engine_spec(processes, rho, x_grids, t_grid=20, damping=0, scheme=None):
    return {"processes": processes, "rho": rho, "xGrids": list(x_grids), "tGrid": t_grid, "dampingSteps": damping,
            "scheme": fbs.scheme_spec(scheme or ql.FdmSchemeDesc.Douglas())}


def results(option_s, engine_s):
    option, keep = bo.option(option_s)
    processes = [bsp.process(p) for p in engine_s["processes"]]
    rho = ql.Matrix(engine_s["rho"])
    grids = engine_s["xGrids"]
    scheme = fbs.scheme(engine_s["scheme"])
    if len(grids) == 1:
        engine = ql.FdndimBlackScholesVanillaEngine(processes, rho, grids[0], engine_s["tGrid"],
                                                    engine_s["dampingSteps"], scheme)
    else:
        engine = ql.FdndimBlackScholesVanillaEngine(processes, rho, grids, engine_s["tGrid"],
                                                    engine_s["dampingSteps"], scheme)
    option.setPricingEngine(engine)
    return {"NPV": option.NPV()}


def cases():
    st = v.settings(R)
    european, american = bo.exercise_spec("European", 365), bo.exercise_spec("American", 365)
    one = [fd2.FLAT_PAIR[0]]
    flat2, zero2 = fd2.FLAT_PAIR, fd2.ZERO_PAIR
    rho2 = [[1.0, 0.5], [0.5, 1.0]]
    rho2n = [[1.0, -0.3], [-0.3, 1.0]]
    vanilla_put = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", PUT, 105.0), weights=[1.0])
    vanilla_call = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", CALL, 95.0), n=1)
    spread_call = bo.payoff_spec("Spread", bc.payoff_spec("PlainVanilla", CALL, 5.0))
    spread_put = bo.payoff_spec("Spread", bc.payoff_spec("PlainVanilla", PUT, 5.0))
    avg2 = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", CALL, 100.0), weights=[0.6, 0.4])
    max2 = bo.payoff_spec("Max", bc.payoff_spec("PlainVanilla", CALL, 105.0))
    avg3_call = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", CALL, 100.0), n=3)
    avg3_put = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", PUT, 100.0), weights=[0.5, 0.25, 0.25])
    min3 = bo.payoff_spec("Min", bc.payoff_spec("PlainVanilla", PUT, 100.0))
    setups = [
        ("one asset, call", vanilla_call, european, engine_spec(one, [[1.0]], [41])),
        ("one asset, American put", vanilla_put, american, engine_spec(one, [[1.0]], [41])),
        ("two assets, spread call", spread_call, european, engine_spec(flat2, rho2, [21])),
        ("two assets, American spread put", spread_put, american, engine_spec(flat2, rho2, [21])),
        ("two assets, average call, grids per direction", avg2, european, engine_spec(flat2, rho2n, [21, 13])),
        ("two assets, American maximum call, Hundsdorfer", max2, american,
         engine_spec(flat2, rho2, [17], scheme=ql.FdmSchemeDesc.Hundsdorfer())),
        ("two assets, zero curves, spread call", spread_call, european, engine_spec(zero2, rho2, [21])),
        ("two assets, spread call, implicit Euler", spread_call, european,
         engine_spec(flat2, rho2, [17], scheme=ql.FdmSchemeDesc.ImplicitEuler())),
        ("two assets, American average, TR-BDF2", avg2, american,
         engine_spec(flat2, rho2, [17], scheme=ql.FdmSchemeDesc.TrBDF2())),
        ("two assets, zero curves, American average", avg2, american, engine_spec(zero2, rho2n, [17], damping=2)),
        ("three assets, average call", avg3_call, european, engine_spec(THREE, RHO3, [9], t_grid=8)),
        ("three assets, American average put", avg3_put, american, engine_spec(THREE, RHO3, [9], t_grid=8)),
        ("three assets, minimum put, grids per direction", min3, european,
         engine_spec(THREE, RHO3, [9, 7, 5], t_grid=8)),
    ]
    out = []
    for name, payoff, ex, engine in setups:
        option = bo.option_spec(payoff, ex)
        out.append({"name": name, "inputs": {"settings": st, "option": option, "engine": engine},
                    "expected": results(option, engine)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/fdndimblackscholesvanillaengine.hpp", cases)
