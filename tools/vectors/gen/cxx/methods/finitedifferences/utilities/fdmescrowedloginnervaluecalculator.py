# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmEscrowedLogInnerValueCalculator
(ql/methods/finitedifferences/utilities/fdmescrowedloginnervaluecalculator.hpp, .cpp), which the wheel doesn't bind,
each output computed by the C++ harness (docs/porting-playbook.md §9.4): innerValue and avgInnerValue at every point of
a uniform log mesher, at four times, for calls and puts on escrowed fixed dividends over flat forwards: two dividends
inside a year, none, and one past the maturity, on the evaluation date 15 October 2026. The finite-difference engine
reads only innerValue, through its early-exercise condition; avgInnerValue is innerValue, which QuantLib's solvers
read through other calculators.

The harness's call is tools/cxx/src/methods/finitedifferences/utilities/fdmescrowedloginnervaluecalculator.cpp's. Read
back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Utilities/FdmEscrowedLogInnerValueCalculatorCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
SCHEDULES = [("two dividends", [[R + 91, 2.0], [R + 273, 3.0]]), ("no dividend", []),
             ("a dividend past maturity", [[R + 182, 1.5], [R + 500, 4.0]])]
TIMES = [0.0, 0.2, 0.5, 1.0]


def cases():
    with cxx.connect() as H:
        st = H.settings(R)
        out = []
        for sname, divs in SCHEDULES:
            for option_type, strike in ((1, 100.0), (-1, 95.0)):
                spec = {"riskFreeRate": 0.04, "dividendYield": 0.01, "dividends": divs, "maturity": 1.0,
                        "optionType": option_type, "strike": strike, "xMin": math.log(40.0),
                        "xMax": math.log(250.0), "size": 25}
                r = H.call("FdmEscrowedLogInnerValueCalculator::values", 0.04, 0.01, divs, 1.0, option_type, strike,
                           spec["xMin"], spec["xMax"], 25, TIMES)
                out.append({"name": f"{sname}, {'call' if option_type == 1 else 'put'}",
                            "inputs": {"settings": st, "calculator": spec, "times": TIMES},
                            "expected": {"innerValue": r["innerValue"], "avgInnerValue": r["avgInnerValue"]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdmescrowedloginnervaluecalculator.hpp", cases)
