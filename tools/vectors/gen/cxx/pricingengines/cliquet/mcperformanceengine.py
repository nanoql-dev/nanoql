# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PerformanceOptionPathPricer (ql/pricingengines/cliquet/mcperformanceengine.hpp, .cpp)
through the C++ harness, which the wheel can't reach: the pricer's value on fixed paths (calls and puts at strikes 0.9,
1 and 1.1, each period's discount its own), and its refusal of a path whose length isn't the discounts' and one more
("discounts/options mismatch").

A case holds the pricer's "optionType", "strike" and "discounts" and the path's "values" (a step between each two on a
uniform grid to a year). The harness's call: PerformanceOptionPathPricer::value
(tools/cxx/src/pricingengines/cliquet/mcperformanceengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Cliquet/MCPerformanceEngineCxxVectorTests.cs."""
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
DISCOUNTS = [0.99, 0.98, 0.97, 0.955]
PATHS = [
    ("rising", [100.0, 104.0, 98.5, 110.25, 121.0]),
    ("falling", [100.0, 95.0, 101.5, 88.0, 79.75]),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def case(name, option_type, strike, discounts, values):
            try:
                expected = {"value": v.enc(H.call("PerformanceOptionPathPricer::value", option_type, strike,
                                                  discounts, values))}
            except RuntimeError as e:
                expected = {"error": str(e)}
            out.append({"name": name,
                        "inputs": {"settings": st, "optionType": option_type, "strike": strike,
                                   "discounts": discounts, "values": values},
                        "expected": expected})

        for pname, values in PATHS:
            for option_type in (CALL, PUT):
                for strike in (0.9, 1.0, 1.1):
                    case(f"{pname}, {'call' if option_type == CALL else 'put'}, strike {strike:g}", option_type,
                         strike, DISCOUNTS, values)
        case("refused: a discount short", CALL, 1.0, DISCOUNTS[:3], PATHS[0][1])
        case("refused: a discount over", CALL, 1.0, DISCOUNTS + [0.94], PATHS[0][1])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/cliquet/mcperformanceengine.hpp", cases)
