# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmHestonHullWhiteSolver (ql/methods/finitedifferences/solvers/fdmhestonhullwhitesolver.hpp,
.cpp): a rollback on the Heston-Hull-White operator, read at points of the spot, variance and short-rate space, on the
evaluation date 15 October 2026.

European puts at the money on tools/vectors/gen/processes/hestonprocess.py's first two parameter sets on flat curves,
with tools/vectors/gen/methods/finitedifferences/operators/fdmhestonhullwhiteop.py's first Hull-White process at the
equity-rate correlation -0.5, on a mesher of 15 log-spots concentrated at the spot, the 7 variances
FdmHestonVarianceMesher places for a year and 5 short-rate states from -0.1 to 0.1, under the Hundsdorfer scheme with
20 time steps; then on the first set: an American put, an American call, a European call on zero curves with the second Hull-White process at the correlation 0.3, the Douglas, Craig-Sneyd and modified Craig-Sneyd
schemes, and two damping steps. Each case's outputs at eighteen points (the spot at 0.9, 1 and 1.1 times the
process's, the variance at 0.8 and 1.25 times v0, the short rate's state at -0.02, 0 and 0.03): valueAt, deltaAt and
gammaAt at a shift of 1% of the spot, as QuantLib's engine reads them; and thetaAt at those of them where the theta
doesn't cancel ("thetaPoints", tools/vectors/gen/methods/finitedifferences/solvers/fdm2dimsolver.py's rule: kept where
the value is at most 1e4 times the theta's change over its snapshot interval).

Left out: every structure is on the risk-free curve's day counter and reference date (on others QuantLib reads the
dividend curve at the risk-free curve's times, tests/NanoQL.Tests/Divergences/HestonHullWhiteMismatchedTimesTests.cs);
and cash dividends, which QuantLib's dividend handler applies on a three-dimensional grid to the lines through the other
directions' first points only, nanoql to every line (tests/NanoQL.Tests/Divergences/FdmDividendHandler3dTests.cs).

A case holds the set-up spec {"process", "hullWhite", "correlation", "payoff", "exercise", "dividends", "mesher",
"tGrid", "dampingSteps"}, the process hestonprocess.py's, the Hull-White process and the mesher fdmhestonhullwhiteop.py's,
the payoff and exercise tools/vectors/gen/pricingengines/vanilla/fdblackscholesvanillaengine.py's; the scheme
(tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's spec), and the points. The wheel's calls,
by QuantLib-SWIG 1.44's fdm.i: FdmMesherComposite, FdmLogInnerValue(payoff, mesher, 0),
FdmStepConditionComposite.vanillaComposite(dividends, exercise, mesher, calculator, referenceDate, dayCounter) on the
risk-free curve's date and day counter, FdmSolverDesc(mesher, FdmBoundaryConditionSet(), conditions, calculator,
maturity, tGrid, dampingSteps) with the maturity the risk-free curve's time to the exercise's last date, and
FdmHestonHullWhiteSolver(hestonProcess, hwProcess, corrEquityShortRate, solverDesc, schemeDesc); the outputs its C++
members. Read back by tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Solvers/FdmHestonHullWhiteSolverVectorTests.cs."""
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
fho = fhho.fho
fhv = fho.fhv
fbv, fbs, hp, bsm, bsp, bc, mc = fhv.fbv, fhv.fbs, fhv.hp, fhv.bsm, fhv.bsp, fhv.bc, fho.mc
R = fhv.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
CANCELLATION = 1e4


def setup_spec(process, payoff, ex, dividends=(), hw=None, correlation=-0.5, t_grid=20, damping=0):
    return {"process": process, "hullWhite": hw or fhho.HULL_WHITE[0], "correlation": correlation, "payoff": payoff,
            "exercise": ex, "dividends": list(dividends), "mesher": fhho.mesher_spec(process, 15, 7, 5),
            "tGrid": t_grid, "dampingSteps": damping}


def built(spec):
    """The processes, the solver description on the wheel, the stopping times, and what keeps them alive."""
    p = hp.process(spec["process"])
    hw = fhho.hull_white(p, spec["hullWhite"])
    mesher, keep = mc.composite(spec["mesher"])
    curve = p.riskFreeRate()
    ref, dc = curve.referenceDate(), curve.dayCounter()
    exercise = fbv.exercise(spec["exercise"])
    maturity = dc.yearFraction(ref, exercise.lastDate())
    payoff = bc.payoff(spec["payoff"])
    calculator = ql.FdmLogInnerValue(payoff, mesher, 0)
    conditions = ql.FdmStepConditionComposite.vanillaComposite(bsm.dividends(spec["dividends"]), exercise, mesher,
                                                               calculator, ref, dc)
    desc = ql.FdmSolverDesc(mesher, ql.FdmBoundaryConditionSet(), conditions, calculator, maturity, spec["tGrid"],
                            spec["dampingSteps"])
    return p, hw, desc, list(conditions.stoppingTimes()), (keep, exercise, payoff, calculator, conditions, mesher)


def points_of(spec):
    s0, v0 = spec["process"]["s0"], spec["process"]["v0"]
    return [[a * s0, b * v0, r] for a in (0.9, 1.0, 1.1) for b in (0.8, 1.25) for r in (-0.02, 0.0, 0.03)]


def theta_kept(values, thetas, stops, maturity):
    """fdm2dimsolver.py's rule: the theta kept where the value is at most 1e4 times its change over the snapshot."""
    t = 0.99 * min(1.0 / 365.0, stops[0] if stops else maturity)
    return [i for i, (value, theta) in enumerate(zip(values, thetas)) if abs(value) <= CANCELLATION * abs(theta * t)]


def variants():
    """The cases' names, set-ups and schemes."""
    european, american = fbv.exercise_spec("European", [365]), fbv.exercise_spec("American", [365])
    out = []
    for params in hp.PARAMETERS[:2]:
        p = fhv.heston_spec(params)
        out.append((f"{params[0]}, European put",
                    setup_spec(p, bc.payoff_spec("PlainVanilla", PUT, params[1]), european), None))
    p = fhv.heston_spec(hp.PARAMETERS[0])
    put, call = bc.payoff_spec("PlainVanilla", PUT, 100.0), bc.payoff_spec("PlainVanilla", CALL, 100.0)
    out.append(("American put", setup_spec(p, put, american), None))
    out.append(("American call", setup_spec(p, call, american), None))
    out.append(("European call, zero curves, the second Hull-White process",
                setup_spec(fhv.heston_spec(hp.PARAMETERS[0], bsp.ZERO_R, bsp.ZERO_Q), call, european,
                           hw=fhho.HULL_WHITE[1], correlation=0.3), None))
    for dname in ("Douglas", "CraigSneyd", "ModifiedCraigSneyd"):
        out.append((f"American put, {dname}", setup_spec(p, put, american), getattr(ql.FdmSchemeDesc, dname)()))
    out.append(("two damping steps, European put", setup_spec(p, put, european, damping=2), None))
    return out


def cases():
    st = v.settings(R)
    out = []
    for name, spec, scheme in variants():
        p, hw, desc, stops, keep = built(spec)
        scheme_spec = fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Hundsdorfer())
        solver = ql.FdmHestonHullWhiteSolver(p, hw, spec["correlation"], desc, fbs.scheme(scheme_spec))
        points = points_of(spec)
        expected = {"valueAt": [v.enc(solver.valueAt(s, var, r)) for s, var, r in points],
                    "deltaAt": [v.enc(solver.deltaAt(s, var, r, 0.01 * s)) for s, var, r in points],
                    "gammaAt": [v.enc(solver.gammaAt(s, var, r, 0.01 * s)) for s, var, r in points]}
        thetas = [solver.thetaAt(s, var, r) for s, var, r in points]
        kept = theta_kept(expected["valueAt"], thetas, stops, desc.getMaturity())
        expected["thetaAt"] = [v.enc(thetas[i]) for i in kept]
        out.append({"name": name,
                    "inputs": {"settings": st, "setup": spec, "scheme": scheme_spec, "points": points,
                               "thetaPoints": [points[i] for i in kept]},
                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdmhestonhullwhitesolver.hpp", cases)
