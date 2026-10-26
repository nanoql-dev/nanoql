# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BarrierPathPricer, BiasedBarrierPathPricer and MakeMCBarrierEngine
(ql/pricingengines/barrier/mcbarrierengine.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++
harness: the two pricers on given paths, and the builder's checks.

The pricers: the four barrier types at 90 (down) and 110 (up), with and without a rebate of 3, a call and a put struck
at 100, on three paths of five steps over a year (one staying between the barriers, one dipping to 88 and one rising
to 112), the discounts at 3% at the path's times; the Brownian-bridge pricer at seeds 5 and 42. The builder: steps
given, steps per year given, neither ("number of steps not given") and both ("number of steps overspecified");
samples then a tolerance ("number of samples already set"), a tolerance then samples ("tolerance already set"); and a
tolerance on low-discrepancy paths ("chosen random generator policy does not allow an error estimate").

The pricers' process and the builder's are tools/cxx/src/pricingengines/barrier/mcbarrierengine.cpp's, on the
evaluation date 15 October 2026. nanoql's builder takes its seed at construction (the S-bar plan's BR1); the harness
sets QuantLib's to 42. Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Barrier/MCBarrierEngineCxxVectorTests.cs."""
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
CALL, PUT = 1, -1
TYPES = [("down-and-in", 0, 90.0), ("up-and-in", 1, 110.0), ("down-and-out", 2, 90.0), ("up-and-out", 3, 110.0)]
TIMES = [0.0, 0.2, 0.4, 0.6, 0.8, 1.0]
PATHS = [("inside", [100.0, 102.0, 97.0, 104.0, 99.0, 103.0]), ("dipping", [100.0, 95.0, 88.0, 92.0, 96.0, 101.0]),
         ("rising", [100.0, 106.0, 112.0, 108.0, 104.0, 99.0])]
DISCOUNTS = [math.exp(-0.03 * t) for t in TIMES]
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
        for tname, t, barrier in TYPES:
            for rebate in (0.0, 3.0):
                for kind, option_type in (("call", CALL), ("put", PUT)):
                    for pname, values in PATHS:
                        head = [t, barrier, rebate, option_type, 100.0, DISCOUNTS]
                        spec = {"barrierType": t, "barrier": barrier, "rebate": rebate, "optionType": option_type,
                                "strike": 100.0, "discounts": DISCOUNTS, "times": TIMES, "values": values}
                        name = f"{tname}, rebate {rebate:g}, {kind}, the path {pname}"
                        out.append({"name": f"biased pricer, {name}", "inputs": {"settings": st, "pricer": spec},
                                    "expected": {"price": H.call("BiasedBarrierPathPricer", *head, TIMES, values)}})
                        for seed in (5, 42):
                            out.append({"name": f"bridge pricer at seed {seed}, {name}",
                                        "inputs": {"settings": st, "pricer": dict(spec, seed=seed)},
                                        "expected": {"price": H.call("BarrierPathPricer", *head, seed, TIMES,
                                                                     values)}})
        for name, traits, steps, per_year, samples, tolerance, order in BUILDERS:
            out.append({"name": f"builder, {name}",
                        "inputs": {"settings": st, "traits": traits, "steps": steps, "stepsPerYear": per_year,
                                   "samples": samples, "tolerance": tolerance, "order": order},
                        "expected": {"built": v.attempt(lambda: H.call("MakeMCBarrierEngine", traits, steps, per_year,
                                                                       samples, tolerance, order))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/mcbarrierengine.hpp", cases)
