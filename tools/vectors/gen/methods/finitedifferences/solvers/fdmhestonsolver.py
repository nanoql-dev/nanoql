# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmHestonSolver (ql/methods/finitedifferences/solvers/fdmhestonsolver.hpp, .cpp): a
rollback on the Heston operator, read at points of the spot and variance plane, on the evaluation date 15 October
2026.

European puts at the money on tools/vectors/gen/processes/hestonprocess.py's three parameter sets on flat curves, on
tools/vectors/gen/methods/finitedifferences/operators/fdmhestonop.py's mesher (25 log-spots, 15 variances) under the
Hundsdorfer scheme with 20 time steps; then on the first set: an American put, an American call with cash dividends, a
European call on zero curves, the Douglas, Craig-Sneyd and modified Craig-Sneyd schemes, two damping steps, a quanto
helper and fdhestonvanillaengine.py's fixed local volatility surface as the leverage function. Each case's outputs at
nine points (the spot at 0.9, 1 and 1.1 times the process's, the variance at 0.8, 1 and 1.25 times v0): valueAt,
deltaAt, gammaAt, meanVarianceDeltaAt and meanVarianceGammaAt; and thetaAt at those of them where the theta doesn't
cancel ("thetaPoints", tools/vectors/gen/methods/finitedifferences/solvers/fdm2dimsolver.py's rule: kept where the
value is at most 1e4 times the theta's change over its snapshot interval).

Left out (the S-hes plan's §2.10): every structure is on the risk-free curve's day counter and reference date (on
others QuantLib reads them at the risk-free curve's times, tests/NanoQL.Tests/Divergences/
FdHestonMismatchedTimesTests.cs).

A case holds the set-up spec {"process", "payoff", "exercise", "dividends", "mesher", "tGrid", "dampingSteps"}, the
process hestonprocess.py's, the payoff and exercise tools/vectors/gen/pricingengines/vanilla/
fdblackscholesvanillaengine.py's, the mesher fdmhestonop.py's; the scheme (tools/vectors/gen/methods/
finitedifferences/solvers/fdmbackwardsolver.py's spec), the quanto helper and the leverage fdhestonvanillaengine.py's,
and the points. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmMesherComposite, FdmLogInnerValue(payoff, mesher,
0), FdmStepConditionComposite.vanillaComposite(dividends, exercise, mesher, calculator, referenceDate, dayCounter) on
the risk-free curve's date and day counter, FdmSolverDesc(mesher, FdmBoundaryConditionSet(), conditions, calculator,
maturity, tGrid, dampingSteps) with the maturity the risk-free curve's time to the exercise's last date, and
FdmHestonSolver(process, solverDesc, schemeDesc, quantoHelper, leverageFct); the outputs its C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Solvers/FdmHestonSolverVectorTests.cs."""
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


fho = _load("fdmhestonop", "methods/finitedifferences/operators/fdmhestonop.py")
fhv = fho.fhv
fbv, fbs, hp, bsm, bsp, bc, mc = fhv.fbv, fhv.fbs, fhv.hp, fhv.bsm, fhv.bsp, fhv.bc, fho.mc
R = fhv.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
CANCELLATION = 1e4
OUTPUTS = ["valueAt", "thetaAt", "deltaAt", "gammaAt", "meanVarianceDeltaAt", "meanVarianceGammaAt"]


def setup_spec(process, payoff, ex, dividends=(), t_grid=20, damping=0):
    return {"process": process, "payoff": payoff, "exercise": ex, "dividends": list(dividends),
            "mesher": fho.heston_mesher(process, 25, 15), "tGrid": t_grid, "dampingSteps": damping}


def built(spec):
    """The solver description on the wheel, the stopping times, and what keeps it alive."""
    p = hp.process(spec["process"])
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
    return p, desc, list(conditions.stoppingTimes()), (keep, exercise, payoff, calculator, conditions, mesher)


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec, scheme=None, quanto=None, leverage=None):
        p, desc, stops, keep = built(spec)
        scheme_spec = fbs.scheme_spec(scheme if scheme is not None else ql.FdmSchemeDesc.Hundsdorfer())
        solver = ql.FdmHestonSolver(p, desc, fbs.scheme(scheme_spec), bsm.quanto(quanto), fhv.leverage(leverage))
        s0, v0 = spec["process"]["s0"], spec["process"]["v0"]
        points = [[a * s0, b * v0] for a in (0.9, 1.0, 1.1) for b in (0.8, 1.0, 1.25)]
        expected = {o: [v.enc(getattr(solver, o)(s, var)) for s, var in points] for o in OUTPUTS}
        t = 0.99 * min(1.0 / 365.0, stops[0] if stops else desc.getMaturity())
        kept = [i for i, (value, theta) in enumerate(zip(expected["valueAt"], expected["thetaAt"]))
                if abs(value) <= CANCELLATION * abs(theta * t)]
        expected["thetaAt"] = [expected["thetaAt"][i] for i in kept]
        out.append({"name": name,
                    "inputs": {"settings": st, "setup": spec, "scheme": scheme_spec, "quanto": quanto,
                               "leverage": leverage, "points": points, "thetaPoints": [points[i] for i in kept]},
                    "expected": expected})

    european, american = fbv.exercise_spec("European", [365]), fbv.exercise_spec("American", [365])
    for params in hp.PARAMETERS:
        p = fhv.heston_spec(params)
        add(f"{params[0]}, European put", setup_spec(p, bc.payoff_spec("PlainVanilla", PUT, params[1]), european))
    p = fhv.heston_spec(hp.PARAMETERS[0])
    put, call = bc.payoff_spec("PlainVanilla", PUT, 100.0), bc.payoff_spec("PlainVanilla", CALL, 100.0)
    add("American put", setup_spec(p, put, american))
    add("American call, dividends", setup_spec(p, call, american, [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)]))
    add("European call, zero curves",
        setup_spec(fhv.heston_spec(hp.PARAMETERS[0], bsp.ZERO_R, bsp.ZERO_Q), call, european))
    for dname in ("Douglas", "CraigSneyd", "ModifiedCraigSneyd"):
        add(f"American put, {dname}", setup_spec(p, put, american), getattr(ql.FdmSchemeDesc, dname)())
    add("two damping steps, European put", setup_spec(p, put, european, damping=2))
    add("quanto, European put", setup_spec(p, put, european), quanto=bsm.FLAT_QUANTO)
    add("surface leverage, American put", setup_spec(p, put, american), leverage=fhv.SURFACE_LEVERAGE)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdmhestonsolver.hpp", cases)
