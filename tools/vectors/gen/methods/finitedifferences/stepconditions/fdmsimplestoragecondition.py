# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmSimpleStorageCondition (ql/methods/finitedifferences/stepconditions/
fdmsimplestoragecondition.hpp, .cpp), on the evaluation date 15 October 2026: the storage condition on a log-spot mesher
by a uniform mesher of storage levels (0 to 100 in 6 and 11 levels), its calculator a log-spot call's or put's inner
value, at change rates within one level, across several and over the whole range: applyTo(a, t) on seeded values at each
of three exercise times and at a time between them, where nothing changes; and on an array one short, refused.

The meshers are tools/vectors/gen/methods/finitedifferences/meshers/fdmmeshercomposite.py's specs, the payoffs
tools/vectors/gen/methods/finitedifferences/stepconditions/fdmsimpleswingcondition.py's. The wheel's calls, by
QuantLib-SWIG 1.44's fdm.i: FdmSimpleStorageCondition(exerciseTimes, mesher, calculator, changeRate) and its applyTo,
writing the wheel's Array in place; FdmLogInnerValue(payoff, mesher, 0). Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/StepConditions/FdmSimpleStorageConditionVectorTests.cs."""
import importlib.util
import math
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


swing_cond = _load("fdmsimpleswingcondition", "methods/finitedifferences/stepconditions/fdmsimpleswingcondition.py")
mc, bc, swing, R = swing_cond.mc, swing_cond.bc, swing_cond.swing, swing_cond.R
arr = swing_cond.arr
TIMES = [0.25, 0.5, 1.0]
BETWEEN = 0.6
SPOT = {"type": "Uniform", "start": math.log(50.0), "end": math.log(150.0), "size": 7}


def cases():
    st = v.settings(R)
    rng = random.Random(20261027)
    out = []
    for levels in (6, 11):
        mspec = mc.composite_spec(SPOT, {"type": "Uniform", "start": 0.0, "end": 100.0, "size": levels})
        mesher, keep = mc.composite(mspec)
        n = mesher.layout().size()
        for pname, pspec in (("call", bc.payoff_spec("PlainVanilla", int(ql.Option.Call), 1.0)),
                             ("put", bc.payoff_spec("PlainVanilla", int(ql.Option.Put), 120.0))):
            calc = ql.FdmLogInnerValue(swing.payoff(pspec), mesher, 0)
            for rate in (7.5, 35.0, 100.0):
                cond = ql.FdmSimpleStorageCondition(TIMES, mesher, calc, rate)
                a = [rng.uniform(0.0, 3000.0) for _ in range(n)]
                applied = []
                for t in TIMES + [BETWEEN]:
                    x = ql.Array(a)
                    cond.applyTo(x, t)
                    applied.append(arr(x))
                short = ql.Array(a[:-1])
                out.append({"name": f"{levels} levels, {pname}, change rate {rate}",
                            "inputs": {"settings": st, "mesher": mspec, "payoff": pspec, "exerciseTimes": TIMES,
                                       "changeRate": rate, "array": a, "times": TIMES + [BETWEEN]},
                            "expected": {"applyTo": applied,
                                         "applyToShort": v.attempt(lambda: cond.applyTo(short, TIMES[-1]))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/stepconditions/fdmsimplestoragecondition.hpp", cases)
