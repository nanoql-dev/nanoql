# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmArithmeticAverageCondition
(ql/methods/finitedifferences/stepconditions/fdmarithmeticaveragecondition.hpp, .cpp): the finite-difference Asian
engine's step condition, which at an averaging time moves each value to the average updated by the spot, by a cubic
spline in the log average.

On a mesher of the log spot and the log average, each uniform over [ln 50, ln 200] (8 and 7 points, the spot first or
second), a value function of both, the condition at averaging times of 0.25, 0.5 and 1.0 with no past fixing and with
two, applied at an averaging time (0.5, 1.0) and at a time that isn't one (0.3): the values after. Fixing dates given
twice are left out (the S-bar plan's §2.10: BR5 (xxviii)).

The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: Uniform1dMesher(start, end, size), FdmMesherComposite(m1, m2),
FdmArithmeticAverageCondition(averageTimes, runningAverage, pastFixings, mesher, equityDirection) and its
applyTo(array, t). Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/StepConditions/FdmArithmeticAverageConditionVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

LO, HI = math.log(50.0), math.log(200.0)
TIMES = [0.25, 0.5, 1.0]
R = ql.Date(15, ql.October, 2026)


def cases():
    st = v.settings(R)
    out = []
    for direction, sizes in ((0, (8, 7)), (1, (7, 8))):
        m = ql.FdmMesherComposite(ql.Uniform1dMesher(LO, HI, sizes[0]), ql.Uniform1dMesher(LO, HI, sizes[1]))
        n = sizes[0] * sizes[1]
        grid0 = [LO + (HI - LO) * (i % sizes[0]) / (sizes[0] - 1) for i in range(n)]
        grid1 = [LO + (HI - LO) * (i // sizes[0]) / (sizes[1] - 1) for i in range(n)]
        spot, average = (grid0, grid1) if direction == 0 else (grid1, grid0)
        start = [max(math.exp(a) - 100.0, 0.0) + 0.05 * math.exp(s) + 0.3 * (s - a) ** 2 for s, a in zip(spot, average)]
        for past in (0, 2):
            for t in (0.5, 1.0, 0.3):
                cond = ql.FdmArithmeticAverageCondition(TIMES, 0.0, past, m, direction)
                a = ql.Array(len(start))
                for i, x in enumerate(start):
                    a[i] = x
                cond.applyTo(a, t)
                out.append({"name": f"the spot in direction {direction}, {past} past fixings, applied at {t:g}",
                            "inputs": {"settings": st, "direction": direction, "sizes": list(sizes), "averageTimes": TIMES,
                                       "pastFixings": past, "t": t, "values": start},
                            "expected": {"values": [a[i] for i in range(len(start))]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/stepconditions/fdmarithmeticaveragecondition.hpp", cases)
