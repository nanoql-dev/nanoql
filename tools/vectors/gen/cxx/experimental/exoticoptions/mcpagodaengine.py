# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PagodaOption and MCPagodaEngine (ql/experimental/exoticoptions/pagodaoption.hpp, .cpp,
mcpagodaengine.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: fixed-seed Monte
Carlo prices of pagoda options.

QuantLib's case's four assets, at spots of 0.15 to 0.35 and a roof of 0.20, on four quarterly fixings, pseudo-random at
its seed 86421 and at 42, with and without antithetic paths, and low-discrepancy; two assets at a spot of 100 with a
roof of 20 on two fixings and on one; the NPV, and the error estimate under the pseudo-random traits. Then an expired
option (low-discrepancy, as an expired multi-asset option's error estimate is QuantLib's unset value and nanoql's 0, the
S-xeq run's R2-6).

The outputs nanoql fixes are left out (the S-xeq plan's §2.8): the processes' structures share the evaluation date and
Act/365, so the engine's clock is the risk-free curve's; every option has a fixing, where QuantLib's engine reads past
an empty list and nanoql's option refuses it (tests/NanoQL.Tests/Conventions/ExperimentalExoticConventionTests.cs). The
processes and option are tools/cxx/src/experimental/exoticoptions/mcpagodaengine.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/ExoticOptions/MCPagodaEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
FOUR = [[0.15, 0.05, 0.01, 0.30], [0.20, 0.05, 0.05, 0.35], [0.35, 0.05, 0.04, 0.25], [0.30, 0.05, 0.03, 0.20]]
TWO = [[100.0, 0.04, 0.02, 0.22], [95.0, 0.04, 0.01, 0.30]]
QUARTERS = [90, 180, 270, 360]


def spec(traits, processes, correlation, days, roof, fraction, antithetic, samples, seed):
    return {"traits": traits, "processes": processes, "correlation": correlation, "fixingDays": days, "roof": roof,
            "fraction": fraction, "antithetic": antithetic, "samples": samples, "seed": seed}


def arguments(s):
    return [s[k] for k in ("traits", "processes", "correlation", "fixingDays", "roof", "fraction", "antithetic",
                           "samples", "seed")]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        setups = [
            ("QuantLib's case, pseudo-random", spec("pr", FOUR, 0.3, QUARTERS, 0.20, 0.62, False, 1023, 86421)),
            ("QuantLib's case, pseudo-random, antithetic", spec("pr", FOUR, 0.3, QUARTERS, 0.20, 0.62, True, 1023,
                                                                86421)),
            ("QuantLib's case, seed 42", spec("pr", FOUR, -0.1, QUARTERS, 0.20, 0.62, False, 2047, 42)),
            ("QuantLib's case, low-discrepancy", spec("ld", FOUR, 0.3, QUARTERS, 0.20, 0.62, False, 1023, 86421)),
            ("two assets, two fixings", spec("pr", TWO, 0.5, [182, 365], 20.0, 0.6, False, 1023, 7)),
            ("two assets, one fixing", spec("pr", TWO, 0.5, [365], 20.0, 0.6, False, 1023, 7)),
            ("expired", spec("ld", TWO, 0.5, [-30, -1], 20.0, 0.6, False, 1023, 7)),
        ]
        for name, s in setups:
            try:
                r = H.call("MCPagodaEngine", *arguments(s))
                expected = {"NPV": r["NPV"]}
                if "errorEstimate" in r:
                    expected["errorEstimate"] = r["errorEstimate"]
            except RuntimeError:
                expected = {"NPV": {"throws": True}}
            out.append({"name": name, "inputs": {"settings": st, "engine": s}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/exoticoptions/mcpagodaengine.hpp", cases)
