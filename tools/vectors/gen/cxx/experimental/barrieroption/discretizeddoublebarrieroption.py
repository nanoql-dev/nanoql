# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscretizedDoubleBarrierOption (ql/experimental/barrieroption/discretizeddoublebarrieroption.hpp,
.cpp), the binomial double-barrier engine's default asset, which the wheel doesn't expose (QuantLib-SWIG binds the
Derman-Kani asset only), each output computed by the C++ harness.

The four double-barrier types on [80, 120], a call and a put struck at 100, rebates of 0 and 3, European in a year,
American over it and Bermudan at its quarters, on Cox-Ross-Rubinstein trees of 50 and 51 steps, spot 100, rates of 3%
and 1%, volatility 20%: the NPV, delta, gamma and theta. Then each asset's mandatory times, plain and Derman-Kani, for
each exercise on no grid and on grids of 50 and 51 steps.

The outputs nanoql fixes are left out (the S-xeq plan's §2.8): the process's structures share the evaluation date and
Act/365, so the tree's clock is the risk-free curve's (XER5 (xi)), and the Cox-Ross-Rubinstein tree builds on the
steps given (XER5 (x)); an American is kept only where the grid's last time, maturity/steps × steps, isn't below the
maturity, where QuantLib 1.44 skips the exercise at expiry and nanoql takes #2878 (XER5 (xii)). The process and option
are tools/cxx/src/experimental/barrieroption/discretizeddoublebarrieroption.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/BarrierOption/DiscretizedDoubleBarrierOptionCxxVectorTests.cs."""
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
TYPES = [("knock-in", 0), ("knock-out", 1), ("KIKO", 2), ("KOKI", 3)]
EXERCISES = [("European", [365]), ("American", [0, 365]), ("Bermudan", [91, 182, 273, 365])]
OUTPUTS = ["NPV", "delta", "gamma", "theta"]


def spec(barrier_type, option_type, rebate, exercise_type, days, steps):
    return {"referenceDate": R, "spot": 100.0, "dividendYield": 0.01, "riskFreeRate": 0.03, "vol": 0.2,
            "barrierType": barrier_type, "barrierLo": 80.0, "barrierHi": 120.0, "rebate": rebate,
            "optionType": option_type, "strike": 100.0, "exerciseType": exercise_type, "days": days, "steps": steps}


def arguments(s):
    return [s[k] for k in ("referenceDate", "spot", "dividendYield", "riskFreeRate", "vol", "barrierType", "barrierLo",
                           "barrierHi", "rebate", "optionType", "strike", "exerciseType", "days", "steps")]


def grid_reaches_maturity(steps):
    # Act/365 over 365 days: maturity 1; QuantLib's TimeGrid(1, steps) ends at (1/steps) × steps
    return (1.0 / steps) * steps >= 1.0


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for tname, t in TYPES:
            for kind, option_type in (("call", CALL), ("put", PUT)):
                for rebate in (0.0, 3.0):
                    for ename, days in EXERCISES:
                        for steps in (50, 51):
                            if ename == "American" and not grid_reaches_maturity(steps):
                                continue
                            s = spec(t, option_type, rebate, ename, days, steps)
                            try:
                                r = H.call("BinomialDoubleBarrierEngine", *arguments(s))
                                expected = {k: r[k] for k in OUTPUTS}
                            except RuntimeError:
                                expected = {"NPV": {"throws": True}}
                            out.append({"name": f"{tname} {kind}, rebate {rebate:g}, {ename}, {steps} steps",
                                        "inputs": {"settings": st, "option": s}, "expected": expected})
        for enhanced in (False, True):
            for ename, days in EXERCISES:
                for steps in (0, 50, 51):
                    s = spec(1, CALL, 0.0, ename, days, steps)
                    times = H.call("DiscretizedDoubleBarrierOption::mandatoryTimes", *arguments(s), enhanced)
                    out.append({"name": f"mandatory times, {'Derman-Kani' if enhanced else 'plain'}, {ename}, "
                                        f"{steps or 'no'} grid steps",
                                "inputs": {"settings": st, "option": s, "enhanced": enhanced},
                                "expected": {"mandatoryTimes": times}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/barrieroption/discretizeddoublebarrieroption.hpp", cases)
