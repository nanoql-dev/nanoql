# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmStepConditionComposite
(ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp, .cpp), on the evaluation date 15 October
2026, on a Black-Scholes mesher with a log put's calculator.

The constructor: an American condition and a snapshot at 0.4 under unsorted, repeated stopping times, which it sorts
and makes unique; applyTo at 0.4 runs the conditions in order, so the snapshot holds the exercised values. joinConditions
of a snapshot at 0.25 and a composite of an American and a Bermudan condition: its stopping times, and applyTo at 0.25.
vanillaComposite for European, American and Bermudan exercises, with no dividends, dividends inside the exercise, one
before the reference date and one after the last exercise date (which it leaves out), and one on the last exercise
date: its stopping times (each dividend time and the same plus 1e-5, capped at maturity, and the Bermudan exercise
times), and applyTo at each stopping time and at 0.6.

The specs are tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's calculators and dividends on
fdmmeshercomposite.py's composites, an exercise {"type": "European" | "American" | "Bermudan", "dates"}. The wheel's
calls, by QuantLib-SWIG 1.44's fdm.i: FdmStepConditionComposite(stoppingTimes, conditions), the C++ constructor taking
the times as one list; joinConditions and vanillaComposite its static members; stoppingTimes and applyTo the C++
members, applyTo writing the wheel's Array in place. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/StepConditions/FdmStepConditionCompositeVectorTests.cs."""
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
A365 = ql.Actual365Fixed()


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def exercise(spec):
    dates = [ql.Date(d) for d in spec["dates"]]
    if spec["type"] == "European":
        return ql.EuropeanExercise(dates[-1])
    if spec["type"] == "American":
        return ql.AmericanExercise(R, dates[-1])
    return ql.BermudanExercise(dates)


def applied(cond, a, times):
    out = []
    for t in times:
        x = ql.Array(a)
        cond.applyTo(x, t)
        out.append(arr(x))
    return out


def cases():
    st = v.settings(R)
    rng = random.Random(20261027)
    mspec = mc.composite_spec(bsm.bs_spec(21, bsp.PROCESSES[0][1], 1.0, 100.0))
    mesher, keep = mc.composite(mspec)
    n = mesher.layout().size()
    cspec = fbs.calculator_spec("Log", bc.payoff_spec("PlainVanilla", int(ql.Option.Put), 100.0))
    calc = fbs.calculator(cspec, mesher)
    out = []

    a = [rng.uniform(0.0, 30.0) for _ in range(n)]
    snapshot = ql.FdmSnapshotCondition(0.4)
    conds = ql.FdmStepConditionVector()
    conds.push_back(ql.FdmAmericanStepCondition(mesher, calc))
    conds.push_back(snapshot)
    times = [0.7, 0.2, 0.4, 0.2, 0.9]
    comp = ql.FdmStepConditionComposite(times, conds)
    x = ql.Array(a)
    comp.applyTo(x, 0.4)
    out.append({"name": "constructor", "inputs": {"settings": st, "mesher": mspec, "calculator": cspec,
                                                  "stoppingTimes": times, "snapshot": 0.4, "array": a, "t": 0.4},
                "expected": {"stoppingTimes": v.enc(list(comp.stoppingTimes())), "applyTo": arr(x),
                             "snapshot": arr(snapshot.getValues())}})

    a = [rng.uniform(0.0, 30.0) for _ in range(n)]
    dates = [(R + d).serialNumber() for d in (91, 182, 365)]
    inner = ql.FdmStepConditionVector()
    bermudan = ql.FdmBermudanStepCondition([ql.Date(d) for d in dates], R, A365, mesher, calc)
    inner.push_back(ql.FdmAmericanStepCondition(mesher, calc))
    inner.push_back(bermudan)
    c2 = ql.FdmStepConditionComposite(list(bermudan.exerciseTimes()), inner)
    snapshot = ql.FdmSnapshotCondition(0.25)
    joined = ql.FdmStepConditionComposite.joinConditions(snapshot, c2)
    x = ql.Array(a)
    joined.applyTo(x, 0.25)
    out.append({"name": "joinConditions", "inputs": {"settings": st, "mesher": mspec, "calculator": cspec,
                                                     "dates": dates, "snapshot": 0.25, "array": a, "t": 0.25},
                "expected": {"stoppingTimes": v.enc(list(joined.stoppingTimes())), "applyTo": arr(x),
                             "snapshot": arr(snapshot.getValues())}})

    schedules = [("no dividends", []), ("dividends inside", [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)]),
                 ("dividends outside", [bsm.fixed(1.0, -10), bsm.fixed(2.0, 120), bsm.fixed(4.0, 500)]),
                 ("a dividend at maturity", [bsm.fixed(2.0, 365)])]
    exercises = [{"type": "European", "dates": [(R + 365).serialNumber()]},
                 {"type": "American", "dates": [(R + 365).serialNumber()]},
                 {"type": "Bermudan", "dates": [(R + d).serialNumber() for d in (91, 182, 273, 365)]}]
    for ex in exercises:
        for sname, divs in schedules:
            a = [rng.uniform(0.0, 30.0) for _ in range(n)]
            comp = ql.FdmStepConditionComposite.vanillaComposite(bsm.dividends(divs), exercise(ex), mesher, calc, R,
                                                                 A365)
            st_times = list(comp.stoppingTimes())
            out.append({"name": f"vanillaComposite, {ex['type']}, {sname}",
                        "inputs": {"settings": st, "mesher": mspec, "calculator": cspec, "exercise": ex,
                                   "dividends": divs, "referenceDate": R.serialNumber(),
                                   "dayCounter": bsm.bvts.A365, "array": a, "times": st_times + [0.6]},
                        "expected": {"stoppingTimes": v.enc(st_times), "applyTo": applied(comp, a, st_times + [0.6])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp", cases)
