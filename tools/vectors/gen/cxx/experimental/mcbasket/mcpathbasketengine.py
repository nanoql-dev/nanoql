# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCPathBasketEngine, MCAmericanPathEngine and LongstaffSchwartzMultiPathPricer
(ql/experimental/mcbasket/mcpathbasketengine.hpp, .cpp, mcamericanpathengine.hpp, .cpp,
longstaffschwartzmultipathpricer.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness on
path payoffs of its own: fixed-seed Monte Carlo prices of path options.

The basket engine: an average call at the last fixing and an Asian basket call over every fixing, on two and three
assets, on four quarterly fixings, on four steps, eight and twelve a year, pseudo-random at seeds 42 and 7, with
antithetic paths, and low-discrepancy; the NPV, and the error estimate under the pseudo-random traits. The American
engine: a Bermudan put on the largest of two and three assets at each quarterly fixing, with and without antithetic
paths, at 512 calibration samples and at QuantLib's default; the NPV and the error estimate. The adapted payoffs: the
average call deferred a quarter on the curve at the last fixing through the basket engine, and the maximum put through
the American engine, each equal to its plain payoff's value but for the deferral.

No output is left out (the S-xeq plan's §2.8): the processes' structures share the evaluation date and Act/365, so the
engines' clock is the risk-free curve's; every option's fixings are sorted (the S-xeq run's R2-3) and none is expired.
The payoffs, processes and options are tools/cxx/src/experimental/mcbasket/mcpathbasketengine.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/McBasket/MCPathBasketEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
TWO = [[100.0, 0.04, 0.02, 0.22], [95.0, 0.04, 0.01, 0.30]]
THREE = [[100.0, 0.04, 0.02, 0.22], [90.0, 0.04, 0.01, 0.30], [110.0, 0.04, 0.03, 0.25]]
QUARTERS = [91, 182, 273, 364]


def basket(traits, processes, payoff, steps, per_year, antithetic, seed, correlation=0.4, samples=1023):
    return {"engine": "basket", "traits": traits, "processes": processes, "correlation": correlation,
            "fixingDays": QUARTERS, "payoff": payoff, "timeSteps": steps, "timeStepsPerYear": per_year,
            "antithetic": antithetic, "samples": samples, "seed": seed}


def american(processes, payoff, steps, antithetic, seed, calibration, correlation=0.4, samples=1023):
    return {"engine": "american", "processes": processes, "correlation": correlation, "fixingDays": QUARTERS,
            "payoff": payoff, "timeSteps": steps, "timeStepsPerYear": None, "antithetic": antithetic,
            "samples": samples, "seed": seed, "calibrationSamples": calibration}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        average, asian, put = ["AverageCall", 100.0], ["AsianBasketCall", 95.0], ["BermudanMaxPut", 105.0]
        adapted_average, adapted_put = ["AdaptedAverageCall", 100.0], ["AdaptedBermudanMaxPut", 105.0]
        setups = [
            ("basket, average call, two assets, four steps", basket("pr", TWO, average, 4, None, False, 42)),
            ("basket, average call, two assets, eight steps", basket("pr", TWO, average, 8, None, False, 42)),
            ("basket, Asian call, three assets, twelve steps a year", basket("pr", THREE, asian, None, 12, False, 7)),
            ("basket, Asian call, three assets, antithetic", basket("pr", THREE, asian, 4, None, True, 7)),
            ("basket, average call, low-discrepancy", basket("ld", TWO, average, 4, None, False, 42)),
            ("basket, Asian call, low-discrepancy, three assets", basket("ld", THREE, asian, 4, None, False, 42,
                                                                         correlation=-0.2)),
            ("basket, adapted average call, two assets", basket("pr", TWO, adapted_average, 4, None, False, 42)),
            ("American, maximum put, two assets", american(TWO, put, 4, False, 42, 512)),
            ("American, adapted maximum put, two assets", american(TWO, adapted_put, 4, False, 42, 512)),
            ("American, maximum put, two assets, antithetic", american(TWO, put, 4, True, 42, 512)),
            ("American, maximum put, three assets, default calibration", american(THREE, put, 4, False, 7, None)),
        ]
        for name, s in setups:
            common = [s["processes"], s["correlation"], s["fixingDays"], s["payoff"], s["timeSteps"],
                      s["timeStepsPerYear"], s["antithetic"], s["samples"], s["seed"]]
            if s["engine"] == "basket":
                r = H.call("MCPathBasketEngine", s["traits"], *common)
            else:
                r = H.call("MCAmericanPathEngine", *common, s["calibrationSamples"])
            out.append({"name": name, "inputs": {"settings": st, "engine": s}, "expected": r})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/mcbasket/mcpathbasketengine.hpp", cases)
