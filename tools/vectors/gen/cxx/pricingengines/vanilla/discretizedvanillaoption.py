# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscretizedVanillaOption (ql/pricingengines/vanilla/discretizedvanillaoption.hpp, .cpp), which
the wheel doesn't bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4): mandatoryTimes, the
exercise dates' times on the process's clock, each moved to the closest time of a grid that isn't empty. European,
American and Bermudan exercises (one Bermudan date before the reference date, a negative time), on Act/365 and
Act/360, on the empty grid, two regular grids and one over mandatory times, from the evaluation date 15 October 2026.

The process and payoff are tools/cxx/src/pricingengines/vanilla/discretizedvanillaoption.cpp's, the grid
tools/cxx/src/timegrid.hpp's spec. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/DiscretizedVanillaOptionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
DAY_COUNTERS = [("Act/365", {"type": "Actual365Fixed"}), ("Act/360", {"type": "Actual360"})]
EXERCISES = [("European", "European", [R + 365]), ("American", "American", [R + 30, R + 400]),
             ("Bermudan", "Bermudan", [R + 91, R + 182, R + 273, R + 365]),
             ("Bermudan from before the reference date", "Bermudan", [R - 10, R + 100, R + 200])]
GRIDS = [("no grid", {}), ("10 steps to 1", {"end": 1.0, "steps": 10}), ("7 steps to 1.2", {"end": 1.2, "steps": 7}),
         ("mandatory times", {"times": [0.25, 0.5, 1.0], "steps": 12})]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for dname, dc in DAY_COUNTERS:
            for ename, etype, dates in EXERCISES:
                for gname, grid in GRIDS:
                    out.append({"name": f"{ename}, {dname}, {gname}",
                                "inputs": {"settings": st, "referenceDate": R, "dayCounter": dc,
                                           "exerciseType": etype, "dates": dates, "grid": grid},
                                "expected": {"mandatoryTimes": v.attempt(lambda: H.call(
                                    "DiscretizedVanillaOption::mandatoryTimes", R, dc, etype, dates, grid))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/discretizedvanillaoption.hpp", cases)
