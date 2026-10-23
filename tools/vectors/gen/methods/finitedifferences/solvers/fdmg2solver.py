# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmG2Solver (ql/methods/finitedifferences/solvers/fdmg2solver.hpp, .cpp): a swaption's value
rolled back on the G2 operator and read at points of the factors' plane, on the evaluation date 15 January 2025.

The finite-difference G2 swaption engine's set-up (fdg2swaptionengine.cpp), built here as the engine builds it: each
factor's Ornstein-Uhlenbeck quantiles to the last exercise (tools/vectors/gen/methods/finitedifferences/meshers/
fdmsimpleprocess1dmesher.py's spec, one averaging step), the swap's inner value on the discounting and forwarding models
(tools/vectors/gen/methods/finitedifferences/utilities/fdmaffinemodelswapinnervalue.py's), QuantLib's vanilla step
conditions on the exercise, no boundary conditions. A European and a Bermudan payer, and a Bermudan receiver, on
tools/vectors/gen/methods/finitedifferences/operators/fdmg2op.py's two G2 models, on 12 by 10 points with 10 time steps,
under the Hundsdorfer, Douglas, Craig-Sneyd, modified Craig-Sneyd and modified Hundsdorfer schemes, and the implicit
Euler scheme, whose two-dimensional step runs QuantLib's BiCGStab; one with two damping steps. Each case's output is
valueAt at nine points of the factors' plane.

Generated and left out: the TR-BDF2 scheme, whose second stage QuantLib runs on the operator at the time its trapezoidal
sub-step set, where nanoql sets it over the whole step for both stages (the S-eq plan's SEQR9;
tests/NanoQL.Tests/Divergences/TrBdf2TimeDependentTests.cs): the G2 operator depends on time through its fitting term,
so its values move by about 1e-6.

A case holds the set-up spec {"model", "forwardingModel", "swap" (tools/vectors/gen/instruments/swaption.py's),
"exercise" (swaption.py's), "sizes", "invEps", "tGrid", "dampingSteps"}, which tools/vectors/gen/methods/
finitedifferences/solvers/fdmhullwhitesolver.py and fdm2dimsolver.py share, the scheme
(tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's spec) and the points. The wheel's calls, by
QuantLib-SWIG 1.44's fdm.i: FdmSimpleProcess1dMesher, FdmMesherComposite, FdmAffineG2ModelSwapInnerValue,
FdmStepConditionComposite.vanillaComposite, FdmBoundaryConditionSet, FdmSolverDesc and FdmG2Solver(model, solverDesc,
schemeDesc), the C++ constructors and functions; valueAt the C++ member. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Solvers/FdmG2SolverVectorTests.cs."""
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


inv = _load("fdmaffinemodelswapinnervalue", "methods/finitedifferences/utilities/fdmaffinemodelswapinnervalue.py")
fbs = _load("fdmbackwardsolver", "methods/finitedifferences/solvers/fdmbackwardsolver.py")
g2op, swp, spm, mc = inv.g2op, inv.swp, inv.spm, inv.mc
fvf, irx = swp.fvf, swp.irx
TODAY = swp.TODAY
M, Y = ql.Months, ql.Years
SCHEMES = ["Hundsdorfer", "Douglas", "CraigSneyd", "ModifiedCraigSneyd", "ModifiedHundsdorfer", "ImplicitEuler",
           "TrBDF2"]
LEFT_OUT_SCHEMES = ["TrBDF2"]
POINTS_2D = [[x, y] for x in (-0.02, 0.0, 0.015) for y in (-0.01, 0.0, 0.01)]


def scheme(name):
    return fbs.scheme_spec(getattr(ql.FdmSchemeDesc, name)())


def setup_spec(model, swap, exercise, sizes, inv_eps=1e-5, t_grid=10, damping=0):
    return {"model": model, "forwardingModel": inv.forwarding_model(model, swp.FORECAST), "swap": swap,
            "exercise": exercise, "sizes": list(sizes), "invEps": inv_eps, "tGrid": t_grid, "dampingSteps": damping}


def built(spec):
    """The engine's set-up on the wheel: (model, mesher, solver description, maturity), and what keeps it alive."""
    model = g2op.make_model(spec["model"])
    fwd = g2op.make_model(spec["forwardingModel"])
    curve = irx.handle(spec["model"]["curve"])
    swap = fvf.make_swap(spec["swap"])
    exercise = swp.make_exercise(spec["exercise"])
    ref, dc = curve.referenceDate(), curve.dayCounter()
    maturity = dc.yearFraction(ref, exercise.lastDate())
    mspec = mc.composite_spec(*g2op.factor_meshers(spec["model"], spec["sizes"], maturity, spec["invEps"]))
    mesher, keep = spm.composite(mspec)
    t2d = inv.time_map(curve, list(exercise.dates()))
    cls = (ql.FdmAffineG2ModelSwapInnerValue if spec["model"]["type"] == "G2"
           else ql.FdmAffineHullWhiteModelSwapInnerValue)
    calculator = cls(model, fwd, swap, t2d, mesher, 0)
    conditions = ql.FdmStepConditionComposite.vanillaComposite(ql.DividendSchedule(), exercise, mesher, calculator,
                                                               ref, dc)
    desc = ql.FdmSolverDesc(mesher, ql.FdmBoundaryConditionSet(), conditions, calculator, maturity, spec["tGrid"],
                            spec["dampingSteps"])
    return model, mesher, desc, (fwd, swap, exercise, keep, t2d, calculator, conditions)


def swaptions():
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    payer = swp.vanilla(swp.PAYER, 1e6, 0.031, start, maturity)
    receiver = swp.vanilla(swp.RECEIVER, 1e6, 0.035, start, maturity)
    starts = [ql.as_coupon(c).accrualStartDate() for c in fvf.make_swap(payer).fixedLeg()]
    return [("European payer", payer, swp.european(exercise)), ("Bermudan payer", payer, swp.bermudan(starts)),
            ("Bermudan receiver", receiver, swp.bermudan(starts))]


def cases():
    out = []
    for k, model in enumerate(g2op.G2S):
        for sname, swap, exercise in swaptions():
            for sch in SCHEMES if k == 0 else SCHEMES[:1]:
                for damping in (0, 2) if sch == "Douglas" else (0,):
                    with v.case_settings(TODAY) as st:
                        spec = setup_spec(model, swap, exercise, (12, 10), damping=damping)
                        m, mesher, desc, keep = built(spec)
                        solver = ql.FdmG2Solver(m, desc, fbs.scheme(scheme(sch)))
                        values = [solver.valueAt(x, y) for x, y in POINTS_2D]
                        if sch in LEFT_OUT_SCHEMES:
                            continue
                        out.append({"name": f"model {k}, {sname}, {sch}{', 2 damping steps' if damping else ''}",
                                    "inputs": {"settings": st, "setup": spec, "scheme": scheme(sch),
                                               "points": POINTS_2D},
                                    "expected": {"valueAt": values}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdmg2solver.hpp", cases)
