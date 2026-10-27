# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCDiscreteAveragingAsianEngineBase
(ql/pricingengines/asian/mcdiscreteasianenginebase.hpp), whose grid the wheel doesn't expose, each output computed by
the C++ harness: the time grid the base builds from an option's fixings and exercise.

Monthly fixings over a year, on the fixing times alone, at 24 steps and at 12 steps a year; with the exercise after the
last fixing, without and with it joining the grid; a fixing today; a date given twice (the grid keeps each time once,
as nanoql's; its pricers count it twice, the S-bar plan's BR5 (xxviii)); and the refusals, every fixing in the past and
the one left today ("all fixings are in the past"). The times and the mandatory times.

The engine's process is tools/cxx/src/pricingengines/asian/mcdiscreteasianenginebase.cpp's, on the evaluation date 15
October 2026. Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Asian/MCDiscreteAveragingAsianEngineBaseCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
CALL, PUT = 1, -1
BUILDERS = [("samples", "pseudorandom", 100, None, "samples"),
            ("samples, then a tolerance", "pseudorandom", 100, 0.01, "samples"),
            ("a tolerance, then samples", "pseudorandom", 100, 0.01, "tolerance"),
            ("a tolerance alone", "pseudorandom", None, 0.01, "tolerance"),
            ("neither", "pseudorandom", None, None, "samples"),
            ("a tolerance on low-discrepancy paths", "lowdiscrepancy", None, 0.01, "tolerance"),
            ("samples on low-discrepancy paths", "lowdiscrepancy", 100, None, "samples")]
# paths: their times, the first 0 or not (the grid then starts at 0, a node that isn't a fixing), and a value per node
PATHS = [("from 0", [0.0, 0.25, 0.5, 0.75, 1.0], [100.0, 104.0, 97.0, 110.0, 101.0]),
         ("from a quarter", [0.25, 0.5, 0.75, 1.0], [100.0, 104.0, 97.0, 110.0, 101.0]),
         ("one fixing", [1.0], [100.0, 92.0])]


def cases():
    monthly = [30 * i for i in range(1, 13)]
    grids = [("monthly fixings, the fixing times alone", monthly, 360, None, None, False),
             ("monthly fixings, 24 steps", monthly, 360, 24, None, False),
             ("monthly fixings, 12 steps a year", monthly, 360, None, 12, False),
             ("the exercise 40 days after the last fixing, not in the grid", monthly, 400, None, None, False),
             ("the exercise 40 days after the last fixing, in the grid", monthly, 400, None, None, True),
             ("the exercise on the last fixing, the grid asked to include it", monthly, 360, None, None, True),
             ("a fixing today", [0] + monthly, 360, None, None, False),
             ("a date given twice", [30, 60, 60, 90], 90, None, None, False),
             ("a past fixing, then monthly", [-10] + monthly, 360, None, None, False),
             ("every fixing in the past", [-20, -10], 360, None, None, False),
             ("the one fixing left today", [-10, 0], 360, None, None, False)]
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for name, days, exercise, steps, per_year, include in grids:
            grid = v.attempt(lambda: H.call("MCDiscreteAveragingAsianEngineBase.timeGrid", days, exercise, steps,
                                            per_year, include))
            expected = ({"times": v.THROWS, "mandatoryTimes": v.THROWS} if grid == v.THROWS
                        else {"times": grid["times"], "mandatoryTimes": grid["mandatoryTimes"]})
            out.append({"name": name,
                        "inputs": {"settings": st, "fixingDays": days, "exerciseDays": exercise, "steps": steps,
                                   "stepsPerYear": per_year, "includeExerciseDate": include},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/asian/mcdiscreteasianenginebase.hpp", cases)
