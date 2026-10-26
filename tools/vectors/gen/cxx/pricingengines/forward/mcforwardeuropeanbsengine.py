# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ForwardEuropeanBSPathPricer and MakeMCForwardEuropeanBSEngine
(ql/pricingengines/forward/mcforwardeuropeanbsengine.hpp, .cpp) through the C++ harness, which the wheel can't reach:
the pricer's value on fixed paths (calls and puts, moneyness 0, 0.9, 1 and 1.25, the reset at the start, in the middle
and at expiry, paths ending in and out of the money), its refusal of a negative moneyness, and the builder's checks
(steps not given, overspecified, a tolerance beside samples either way, a tolerance on low-discrepancy numbers).

A case holds either the pricer's "optionType", "moneyness", "resetIndex", "discount" and the path's "values" (a step
between each two on a uniform grid to a year), or the builder's "setting". The harness's calls:
ForwardEuropeanBSPathPricer::value and MakeMCForwardEuropeanBSEngine::check
(tools/cxx/src/pricingengines/forward/mcforwardeuropeanbsengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Forward/MCForwardEuropeanBSEngineCxxVectorTests.cs."""
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
PATHS = [
    ("rising", [100.0, 104.0, 98.5, 110.25, 121.0]),
    ("falling", [100.0, 95.0, 101.5, 88.0, 79.75]),
    ("flat after the reset", [100.0, 92.0, 92.0, 92.0, 92.0]),
]
SETTINGS = ["noSteps", "overspecified", "toleranceThenSamples", "samplesThenTolerance", "lowDiscrepancyTolerance"]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for pname, values in PATHS:
            for option_type in (CALL, PUT):
                for moneyness in (0.0, 0.9, 1.0, 1.25):
                    for reset in (0, 2, 4):
                        args = (option_type, moneyness, reset, 0.95, values)
                        out.append({"name": f"{pname}, {'call' if option_type == CALL else 'put'}, moneyness "
                                            f"{moneyness:g}, reset {reset}",
                                    "inputs": {"settings": st, "optionType": option_type, "moneyness": moneyness,
                                               "resetIndex": reset, "discount": 0.95, "values": values},
                                    "expected": {"value": v.enc(H.call("ForwardEuropeanBSPathPricer::value", *args))}})
        try:
            H.call("ForwardEuropeanBSPathPricer::value", CALL, -0.1, 2, 0.95, PATHS[0][1])
            raise AssertionError("a negative moneyness priced")
        except RuntimeError as e:
            out.append({"name": "refused: a negative moneyness",
                        "inputs": {"settings": st, "optionType": CALL, "moneyness": -0.1, "resetIndex": 2,
                                   "discount": 0.95, "values": PATHS[0][1]},
                        "expected": {"error": str(e)}})
        for setting in SETTINGS:
            try:
                H.call("MakeMCForwardEuropeanBSEngine::check", setting)
                raise AssertionError(f"{setting} built")
            except RuntimeError as e:
                out.append({"name": f"builder: {setting}", "inputs": {"settings": st, "setting": setting},
                            "expected": {"error": str(e)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/forward/mcforwardeuropeanbsengine.hpp", cases)
