# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticPerformanceEngine (ql/pricingengines/cliquet/analyticperformanceengine.hpp, .cpp),
on the evaluation date 15 October 2026: performance options on analyticcliquetengine.py's setups (calls and puts at
moneyness 0.9, 1 and 1.1, three reset schedules, two flat markets and one of zero curves under a flat volatility): the
NPV, delta, gamma, theta, vega, rho and dividend rho.

The outputs nanoql fixes are left out: the theta on the zero curves, where QuantLib reads each period's risk-free rate
over the period after its reset and nanoql the rate to it (tests/NanoQL.Tests/Divergences/CliquetThetaTests.cs);
volatilities off flat (ForwardVegaTermStructureTests.cs).

The specs are analyticcliquetengine.py's. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
CliquetOption(payoff, exercise, resetDates), AnalyticPerformanceEngine(process), and the option's NPV and Greeks.
Read back by tests/NanoQL.Tests/Vectors/PricingEngines/Cliquet/AnalyticPerformanceEngineVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


cliquet = _load("analyticcliquetengine", "pricingengines/cliquet/analyticcliquetengine.py")
bsp = cliquet.bsp
R = cliquet.R


def cases():
    st = v.settings(R)
    out = []
    for name, ospec, pspec, flat in cliquet.setups():
        o = cliquet.option(ospec)
        o.setPricingEngine(ql.AnalyticPerformanceEngine(bsp.process(pspec)))
        expected = {"NPV": o.NPV()}
        for g in cliquet.GREEKS:
            if g == "theta" and not flat:
                continue
            expected[g] = v.result(lambda: getattr(o, g)())
        out.append({"name": name, "inputs": {"settings": st, "option": ospec, "process": pspec}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/cliquet/analyticperformanceengine.hpp", cases)
