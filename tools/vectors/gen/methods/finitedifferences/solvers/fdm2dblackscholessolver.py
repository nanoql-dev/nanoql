# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Fdm2dBlackScholesSolver (ql/methods/finitedifferences/solvers/fdm2dblackscholessolver.hpp,
.cpp), on the evaluation date 15 October 2026: the two-dimensional Black-Scholes solver on the problem the
two-dimensional engine builds (two Black-Scholes meshers, FdmLogBasketInnerValue, the vanilla step conditions of a
European or American exercise), its valueAt, thetaAt, deltaXat, deltaYat, gammaXat, gammaYat and gammaXYat at five
pairs of prices around the spots, under Hundsdorfer's and Douglas's schemes and with the local volatility.

Every structure is on Act/365 (Fixed) from the evaluation date and the two processes share one risk-free curve
(tests/NanoQL.Tests/Divergences/Fd2dTwoRiskFreeCurvesTests.cs and Fd2dMismatchedTimesTests.cs).

The specs are tools/vectors/gen/pricingengines/basket/fd2dblackscholesvanillaengine.py's processes and
tools/vectors/gen/instruments/basketoption.py's payoffs and exercises; a solver {"processes", "correlation",
"payoff", "exercise", "xGrid", "yGrid", "maturity", "tGrid", "dampingSteps", "scheme", "localVol",
"illegalLocalVolOverwrite"}. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmBlackScholesMesher (on each process,
its spot the strike, the engine's eps, scale factor and concentration), FdmMesherComposite, FdmLogBasketInnerValue,
FdmStepConditionComposite.vanillaComposite, FdmSolverDesc and Fdm2dBlackScholesSolver, the C++ constructors and
factory; the solver's members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Solvers/Fdm2dBlackScholesSolverVectorTests.cs."""
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
bo, fbs, bc, bsp = fd2.bo, fd2.fbs, fd2.bc, fd2.bsp
R = bo.R
CALL, PUT = bo.CALL, bo.PUT
NULL = ql.nullDouble()
POINTS = [(1.0, 1.0), (0.9, 1.05), (1.1, 0.95), (1.2, 1.2), (0.85, 0.8)]


def solver_spec(processes, correlation, payoff, ex, grids=(21, 21), t_grid=20, damping=0, scheme=None,
                local_vol=False, overwrite=None):
    return {"processes": processes, "correlation": correlation, "payoff": payoff, "exercise": ex,
            "xGrid": grids[0], "yGrid": grids[1], "maturity": 1.0, "tGrid": t_grid, "dampingSteps": damping,
            "scheme": fbs.scheme_spec(scheme or ql.FdmSchemeDesc.Hundsdorfer()), "localVol": local_vol,
            "illegalLocalVolOverwrite": overwrite}


def outputs(spec):
    p1, p2 = (bsp.process(p) for p in spec["processes"])
    t = spec["maturity"]
    m1 = ql.FdmBlackScholesMesher(spec["xGrid"], p1, t, p1.x0(), NULL, NULL, 0.0001, 1.5, (p1.x0(), 0.1))
    m2 = ql.FdmBlackScholesMesher(spec["yGrid"], p2, t, p2.x0(), NULL, NULL, 0.0001, 1.5, (p2.x0(), 0.1))
    mesher = ql.FdmMesherComposite(m1, m2)
    payoff = bo.payoff(spec["payoff"])
    calc = ql.FdmLogBasketInnerValue(payoff, mesher)
    conditions = ql.FdmStepConditionComposite.vanillaComposite(
        ql.DividendSchedule(), bo.exercise(spec["exercise"]), mesher, calc, R, ql.Actual365Fixed())
    desc = ql.FdmSolverDesc(mesher, ql.FdmBoundaryConditionSet(), conditions, calc, t, spec["tGrid"],
                            spec["dampingSteps"])
    overwrite = -NULL if spec["illegalLocalVolOverwrite"] is None else spec["illegalLocalVolOverwrite"]
    solver = ql.Fdm2dBlackScholesSolver(p1, p2, spec["correlation"], desc, fbs.scheme(spec["scheme"]),
                                        spec["localVol"], overwrite)
    us = [(a * p1.x0(), b * p2.x0()) for a, b in POINTS]
    out = {}
    for name, f in (("valueAt", solver.valueAt), ("thetaAt", solver.thetaAt), ("deltaXat", solver.deltaXat),
                    ("deltaYat", solver.deltaYat), ("gammaXat", solver.gammaXat), ("gammaYat", solver.gammaYat),
                    ("gammaXYat", solver.gammaXYat)):
        out[name] = v.enc([f(u, w) for u, w in us])
    return [list(p) for p in us], out


def cases():
    st = v.settings(R)
    european, american = bo.exercise_spec("European", 365), bo.exercise_spec("American", 365)
    spread_put = bo.payoff_spec("Spread", bc.payoff_spec("PlainVanilla", PUT, 5.0))
    avg_call = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", CALL, 100.0), weights=[0.5, 0.5])
    max_call = bo.payoff_spec("Max", bc.payoff_spec("PlainVanilla", CALL, 100.0))
    setups = [("flat, average call", solver_spec(fd2.FLAT_PAIR, 0.5, avg_call, european)),
              ("flat, American spread put", solver_spec(fd2.FLAT_PAIR, -0.3, spread_put, american)),
              ("zero curves, maximum call, Douglas",
               solver_spec(fd2.ZERO_PAIR, 0.5, max_call, european, scheme=ql.FdmSchemeDesc.Douglas())),
              ("local volatility, average call",
               solver_spec(fd2.SURFACE_PAIR, 0.5, avg_call, european, grids=(15, 15), t_grid=10, local_vol=True))]
    out = []
    for name, spec in setups:
        points, expected = outputs(spec)
        out.append({"name": name, "inputs": {"settings": st, "solver": spec, "points": points}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdm2dblackscholessolver.hpp", cases)
