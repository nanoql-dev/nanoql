# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmSimpleSwingCondition (ql/methods/finitedifferences/stepconditions/
fdmsimpleswingcondition.hpp, .cpp), with the two Dirichlet boundaries the CEV and SABR engines apply,
FdmTimeDepDirichletBoundary (ql/methods/finitedifferences/utilities/fdmtimedepdirichletboundary.hpp, .cpp) and
FdmDiscountDirichletBoundary (ql/methods/finitedifferences/utilities/fdmdiscountdirichletboundary.hpp, .cpp), on the
evaluation date 15 October 2026.

The swing condition on a Black-Scholes mesher by a uniform mesher of the rights used (0 to 3 and 0 to 5), its
calculator a log forward or plain put's (tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's,
the payoff tools/vectors/gen/pricingengines/vanilla/fdsimplebsswingengine.py's), at minimums of 0, 2 and all:
applyTo(a, t) on seeded values at each of four exercise times and at a time between them, where nothing changes; and
on an array of the wrong size, refused. The boundaries on a two-dimensional mesher, in each direction and on each
side: the time-dependent one's value a + b·t, the discount one's a cash flow discounted on a flat curve from a maturity;
after setTime at three times, applyAfterApplying and applyAfterSolving on seeded values.

The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmSimpleSwingCondition(exerciseTimes, mesher, calculator,
swingDirection, minExercises) and its applyTo, writing the wheel's Array in place; FdmTimeDepDirichletBoundary(mesher,
function, direction, side), its function a Python callable; FdmDiscountDirichletBoundary(mesher, rTS, maturityTime,
valueOnBoundary, direction, side); setTime, applyAfterApplying and applyAfterSolving the C++ members, writing in place.
Read back by tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/StepConditions/FdmSimpleSwingConditionVectorTests.cs."""
import importlib.util
import os
import random
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


berm = _load("fdmbermudanstepcondition", "methods/finitedifferences/stepconditions/fdmbermudanstepcondition.py")
swing = _load("fdsimplebsswingengine", "pricingengines/vanilla/fdsimplebsswingengine.py")
mc, bsm, bsp, fbs, bc = berm.mc, berm.bsm, berm.bsp, berm.fbs, berm.bc
R = berm.R
PUT = int(ql.Option.Put)
TIMES = [0.25, 0.5, 0.75, 1.0]
BETWEEN = 0.6
U4 = {"type": "Uniform", "start": 0.0, "end": 3.0, "size": 4}
U6 = {"type": "Uniform", "start": 0.0, "end": 5.0, "size": 6}
BOUNDARY_TIMES = [0.0, 0.4, 1.0]


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def swing_cases(st, rng):
    out = []
    for rights_spec in (U4, U6):
        mspec = mc.composite_spec(bsm.bs_spec(11, bsp.PROCESSES[0][1], 1.0, 100.0), rights_spec)
        mesher, keep = mc.composite(mspec)
        n = mesher.layout().size()
        for pname, pspec in (("forward put", swing.forward_spec(PUT, 100.0)),
                             ("plain put", bc.payoff_spec("PlainVanilla", PUT, 100.0))):
            calc = ql.FdmLogInnerValue(swing.payoff(pspec), mesher, 0)
            for minimum in (0, 2, len(TIMES)):
                cond = ql.FdmSimpleSwingCondition(TIMES, mesher, calc, 1, minimum)
                a = [rng.uniform(0.0, 30.0) for _ in range(n)]
                applied = []
                for t in TIMES + [BETWEEN]:
                    x = ql.Array(a)
                    cond.applyTo(x, t)
                    applied.append(arr(x))
                short = ql.Array(a[:-1])
                out.append({"name": f"{rights_spec['size'] - 1} rights, {pname}, minimum {minimum}",
                            "inputs": {"settings": st, "mesher": mspec, "payoff": pspec, "exerciseTimes": TIMES,
                                       "minExercises": minimum, "array": a, "times": TIMES + [BETWEEN]},
                            "expected": {"applyTo": applied,
                                         "applyToShort": v.attempt(lambda: cond.applyTo(short, TIMES[-1]))}})
    return out


def boundary_cases(st, rng):
    out = []
    mspec = mc.composite_spec(mc.U4, mc.C6)
    mesher, keep = mc.composite(mspec)
    n = mesher.layout().size()
    curve_spec = bsp.flat_spec(R, 0.04)
    curve = bsp.curve(curve_spec)
    for direction in (0, 1):
        for side_name, side in (("lower", ql.FdmTimeDepDirichletBoundary.Lower),
                                ("upper", ql.FdmTimeDepDirichletBoundary.Upper)):
            for kind in ("timeDependent", "discount"):
                if kind == "timeDependent":
                    spec = {"type": kind, "a": 2.0, "b": 3.0, "direction": direction, "side": side_name}
                    bc_ = ql.FdmTimeDepDirichletBoundary(mesher, lambda t: 2.0 + 3.0 * t, direction, side)
                else:
                    spec = {"type": kind, "rTS": curve_spec, "maturityTime": 1.5, "valueOnBoundary": 7.0,
                            "direction": direction, "side": side_name}
                    dside = (ql.FdmDiscountDirichletBoundary.Lower if side_name == "lower"
                             else ql.FdmDiscountDirichletBoundary.Upper)
                    bc_ = ql.FdmDiscountDirichletBoundary(mesher, curve, 1.5, 7.0, direction, dside)
                a = [rng.uniform(-1.0, 1.0) for _ in range(n)]
                after_applying, after_solving = [], []
                for t in BOUNDARY_TIMES:
                    bc_.setTime(t)
                    x, y = ql.Array(a), ql.Array(a)
                    bc_.applyAfterApplying(x)
                    bc_.applyAfterSolving(y)
                    after_applying.append(arr(x))
                    after_solving.append(arr(y))
                out.append({"name": f"{kind} boundary, direction {direction}, {side_name}",
                            "inputs": {"settings": st, "mesher": mspec, "boundary": spec, "array": a,
                                       "times": BOUNDARY_TIMES},
                            "expected": {"applyAfterApplying": after_applying, "applyAfterSolving": after_solving}})
    return out


def cases():
    st = v.settings(R)
    rng = random.Random(20261026)
    return swing_cases(st, rng) + boundary_cases(st, rng)


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/stepconditions/fdmsimpleswingcondition.hpp", cases)
