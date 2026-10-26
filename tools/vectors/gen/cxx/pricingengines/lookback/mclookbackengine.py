# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MakeMCLookbackEngine (ql/pricingengines/lookback/mclookbackengine.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: the builder's checks for each of the four lookback
options.

For the fixed, floating, partial fixed and partial floating options: steps given, steps per year given, neither
("number of steps not given") and both ("number of steps overspecified"); samples then a tolerance ("number of samples
already set"), a tolerance then samples ("tolerance already set"); a tolerance alone; and a tolerance on
low-discrepancy paths ("chosen random generator policy does not allow an error estimate").

The engine's prices are left out (the S-bar plan's §2.10): QuantLib's path pricers take the path's extremum from its
second node, never the spot or the running extremum, and its partial pricers snap their window to a regular grid and
drop λ, so BR5 (xx)'s fold of the observed extremum, its window on the grid and (xxi)'s λ change every price, the
unseasoned option's included (tests/NanoQL.Tests/Divergences/McLookbackSeasonedTests.cs, McLookbackWindowTests.cs,
McLookbackLambdaTests.cs).

The builder's process is tools/cxx/src/pricingengines/lookback/mclookbackengine.cpp's, on the evaluation date 15
October 2026. nanoql's builder takes its seed at construction (the S-bar plan's BR1); the harness sets QuantLib's to
42. Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Lookback/MCLookbackEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
INSTRUMENTS = ["fixed", "floating", "partial fixed", "partial floating"]
BUILDERS = [("steps", "pseudorandom", 10, None, 100, None, "samples"),
            ("steps per year", "pseudorandom", None, 12, 100, None, "samples"),
            ("no steps", "pseudorandom", None, None, 100, None, "samples"),
            ("steps and steps per year", "pseudorandom", 10, 12, 100, None, "samples"),
            ("samples, then a tolerance", "pseudorandom", 10, None, 100, 0.01, "samples"),
            ("a tolerance, then samples", "pseudorandom", 10, None, 100, 0.01, "tolerance"),
            ("a tolerance alone", "pseudorandom", 10, None, None, 0.01, "tolerance"),
            ("a tolerance on low-discrepancy paths", "lowdiscrepancy", 10, None, None, 0.01, "tolerance")]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for instrument in INSTRUMENTS:
            for name, traits, steps, per_year, samples, tolerance, order in BUILDERS:
                out.append({"name": f"builder, the {instrument} option, {name}",
                            "inputs": {"settings": st, "instrument": instrument, "traits": traits, "steps": steps,
                                       "stepsPerYear": per_year, "samples": samples, "tolerance": tolerance,
                                       "order": order},
                            "expected": {"built": v.attempt(lambda: H.call("MakeMCLookbackEngine", instrument, traits,
                                                                           steps, per_year, samples, tolerance,
                                                                           order))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/lookback/mclookbackengine.hpp", cases)
