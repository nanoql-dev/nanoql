# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VariancePathPricer and the Monte Carlo variance-swap engine's checks
(ql/pricingengines/forward/mcvarianceswapengine.hpp) through the C++ harness, which the wheel can't reach.

- "pricer" cases: the pricer's value, a path's integrated local variance over its time, on a Black-Scholes-Merton
  process with a constant volatility, a variance curve and a variance surface (whose local variance follows the
  path), over flat, rising, falling and zigzag paths of 4, 12 and 52 steps to a quarter, a year and 1.9 years.
- "check" cases: MakeMCVarianceSwapEngine's refusals of steps not given or given both ways, of a tolerance beside a
  number of samples (either order) and of a tolerance on low-discrepancy numbers; the engine's constructor's of no
  steps, both kinds of steps, and zero steps of each kind.

A pricer case holds "volatility" ("constant" | "curve" | "surface"), "maturity" and the path's "values" (a step
between each two on a uniform grid to the maturity); a check case its "setting". The harness's calls:
VariancePathPricer::value and MakeMCVarianceSwapEngine::check (tools/cxx/src/pricingengines/forward/
mcvarianceswapengine.cpp, which states each process). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Forward/MCVarianceSwapEngineCxxVectorTests.cs."""
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
SHAPES = [
    ("flat", lambda u: 100.0),
    ("rising", lambda u: 100.0 * math.exp(0.3 * u)),
    ("falling", lambda u: 100.0 * math.exp(-0.25 * u)),
    ("zigzag", lambda u: 100.0 + 15.0 * math.sin(7.0 * u) - 5.0 * u),
]
SETTINGS = ["noSteps", "overspecified", "toleranceThenSamples", "samplesThenTolerance", "lowDiscrepancyTolerance",
            "noTimeSteps", "bothTimeSteps", "zeroTimeSteps", "zeroTimeStepsPerYear"]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def attempt(f):
            try:
                return {"value": v.enc(f())}
            except RuntimeError as e:
                return {"error": str(e)}

        for volatility in ("constant", "curve", "surface"):
            for maturity in (0.25, 1.0, 1.9):
                for steps in (4, 12, 52):
                    for sname, shape in SHAPES:
                        values = [shape(i / steps) for i in range(steps + 1)]
                        out.append({"name": f"pricer: {volatility}, {maturity:g} years, {steps} steps, {sname}",
                                    "inputs": {"settings": st, "volatility": volatility, "maturity": maturity,
                                               "values": values},
                                    "expected": attempt(lambda: H.call("VariancePathPricer::value", volatility,
                                                                       maturity, values))})
        for setting in SETTINGS:
            out.append({"name": f"check: {setting}", "inputs": {"settings": st, "setting": setting},
                        "expected": attempt(lambda: H.call("MakeMCVarianceSwapEngine::check", setting))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/forward/mcvarianceswapengine.hpp", cases)
