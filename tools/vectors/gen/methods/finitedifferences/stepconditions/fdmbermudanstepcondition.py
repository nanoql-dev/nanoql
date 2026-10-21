# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmBermudanStepCondition
(ql/methods/finitedifferences/stepconditions/fdmbermudanstepcondition.hpp, .cpp), on the evaluation date 15 October
2026: the exercise times of four schedules (quarterly, one date, a date on the reference date, and dates on Act/360),
and applyTo(a, t) at each exercise time, where each value rises to the calculator's inner value, and at a time between
them, where nothing changes; on a Black-Scholes and a two-dimensional mesher with a log put's calculator; and the
refusal of an array of another size at an exercise time.

The specs are tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's calculators on
fdmmeshercomposite.py's composites, with the dates' serial numbers, the reference date and the day counter. The wheel's
calls, by QuantLib-SWIG 1.44's fdm.i: FdmBermudanStepCondition(exerciseDates, referenceDate, dayCounter, mesher,
calculator), the C++ constructor; exerciseTimes and applyTo the C++ members, applyTo writing the wheel's Array in place.
Read back by tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/StepConditions/FdmBermudanStepConditionVectorTests.cs."""
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


fbs = _load("fdmbackwardsolver", "methods/finitedifferences/solvers/fdmbackwardsolver.py")
mc, bsm, bsp, bc = fbs.mc, fbs.bsm, fbs.bsp, fbs.bc
R = fbs.R
SCHEDULES = [("quarterly", [91, 182, 273, 365], bsm.bvts.A365), ("one date", [200], bsm.bvts.A365),
             ("from the reference date", [0, 120, 240], bsm.bvts.A365), ("Act/360", [91, 182, 365], bsm.bvts.A360)]
MESHERS = [("Black-Scholes", mc.composite_spec(bsm.bs_spec(21, bsp.PROCESSES[0][1], 1.0, 100.0)), 0),
           ("two dimensions", mc.composite_spec(mc.U4, bsm.bs_spec(9, bsp.PROCESSES[0][1], 1.0, 100.0)), 1)]


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def cases():
    st = v.settings(R)
    rng = random.Random(20261025)
    out = []
    for mname, mspec, direction in MESHERS:
        mesher, keep = mc.composite(mspec)
        n = mesher.layout().size()
        cspec = fbs.calculator_spec("Log", bc.payoff_spec("PlainVanilla", int(ql.Option.Put), 100.0), direction)
        calc = fbs.calculator(cspec, mesher)
        for sname, offsets, dc in SCHEDULES:
            dates = [(R + d).serialNumber() for d in offsets]
            cond = ql.FdmBermudanStepCondition([ql.Date(d) for d in dates], R, bsm.bvts.day_counter(dc), mesher,
                                               calc)
            times = list(cond.exerciseTimes())
            a = [rng.uniform(0.0, 30.0) for _ in range(n)]
            applied = []
            for t in times + [0.3]:
                x = ql.Array(a)
                cond.applyTo(x, t)
                applied.append(arr(x))
            short = ql.Array(a[:-1])
            out.append({"name": f"{mname}, {sname}",
                        "inputs": {"settings": st, "mesher": mspec, "calculator": cspec, "dates": dates,
                                   "referenceDate": R.serialNumber(), "dayCounter": dc, "array": a,
                                   "times": times + [0.3]},
                        "expected": {"exerciseTimes": v.enc(times), "applyTo": applied,
                                     "applyToShort": v.attempt(lambda: cond.applyTo(short, times[-1]))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/stepconditions/fdmbermudanstepcondition.hpp", cases)
