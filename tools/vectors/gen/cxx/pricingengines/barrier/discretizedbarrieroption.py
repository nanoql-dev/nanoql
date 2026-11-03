# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscretizedBarrierOption and DiscretizedDermanKaniBarrierOption
(ql/pricingengines/barrier/discretizedbarrieroption.hpp, .cpp), which the wheel doesn't expose, each output computed by
the C++ harness: each asset's rollback on a Cox-Ross-Rubinstein lattice, its present value and mandatory times, and
the plain asset's checkBarrier on given values.

The rollbacks: the four barrier types at 90 (down) and 110 (up), with and without a rebate of 3, a call and a put struck
at 100, European in a year, American over the year and Bermudan at its quarters, on 50 steps, both assets. The checks:
the plain asset at the grid's step 0, 25 and 50 for each type, on values 1 to n over the lattice's n nodes there,
European in a year, a rebate of 3 and a call struck at 100, and at step 25 for the American and the Bermudan.

The outputs nanoql fixes are left out: the lattice's grid is the asset's own regular grid, its last time the maturity
the asset starts at, so the American window is QuantLib 1.44's under #2878's comparison too
(tests/NanoQL.Tests/Divergences/BarrierUpstreamFixesTests.cs). The option and lattice are
tools/cxx/src/pricingengines/barrier/discretizedbarrieroption.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Barrier/DiscretizedBarrierOptionCxxVectorTests.cs."""
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
TYPES = [("down-and-in", 0, 90.0), ("up-and-in", 1, 110.0), ("down-and-out", 2, 90.0), ("up-and-out", 3, 110.0)]
EXERCISES = [("European", "European", [R + 365]), ("American", "American", [R, R + 365]),
             ("Bermudan", "Bermudan", [R + 91, R + 182, R + 273, R + 365])]
STEPS = 50


def option_spec(barrier_type, barrier, rebate, option_type, exercise_type, dates):
    return {"referenceDate": R, "barrierType": barrier_type, "barrier": barrier, "rebate": rebate,
            "optionType": option_type, "strike": 100.0, "exerciseType": exercise_type, "dates": dates, "steps": STEPS}


def arguments(o):
    return [o["referenceDate"], o["barrierType"], o["barrier"], o["rebate"], o["optionType"], o["strike"],
            o["exerciseType"], o["dates"], o["steps"]]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for tname, t, barrier in TYPES:
            for rebate in (0.0, 3.0):
                for kind, option_type in (("call", CALL), ("put", PUT)):
                    for ename, etype, dates in EXERCISES:
                        o = option_spec(t, barrier, rebate, option_type, etype, dates)
                        for asset, enhanced in (("plain", False), ("Derman-Kani", True)):
                            r = H.call("DiscretizedBarrierOption::rollback", *arguments(o), enhanced)
                            out.append({"name": f"rollback, {tname}, rebate {rebate:g}, {kind}, {ename}, {asset}",
                                        "inputs": {"settings": st, "option": o, "enhanced": enhanced},
                                        "expected": {"presentValue": r["presentValue"],
                                                     "mandatoryTimes": r["mandatoryTimes"]}})
        checks = [(tname, t, barrier, "European", "European", [R + 365], step)
                  for tname, t, barrier in TYPES for step in (0, 25, STEPS)]
        checks += [(tname, t, barrier, ename, etype, dates, 25)
                   for tname, t, barrier in TYPES for ename, etype, dates in EXERCISES[1:]]
        for tname, t, barrier, ename, etype, dates, step in checks:
            o = option_spec(t, barrier, 3.0, CALL, etype, dates)
            values = [float(j + 1) for j in range(step + 1)]
            out.append({"name": f"checkBarrier, {tname}, {ename}, step {step}",
                        "inputs": {"settings": st, "option": o, "step": step, "values": values},
                        "expected": {"values": H.call("DiscretizedBarrierOption::checkBarrier", *arguments(o), step,
                                                      values)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/discretizedbarrieroption.hpp", cases)
