# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GaussianCopulaSpreadEngine (ql/pricingengines/basket/gaussiancopulaspreadengine.hpp, .cpp), on
the evaluation date 15 October 2026: the engine's NPV on kirkengine.py's setups, at its default 64 points, the two
processes on one risk-free curve object, as the engine requires (QuantLib compares the handles' links; the second
process is rebuilt on the first's curve, as the reader does). The wheel's calls, by QuantLib-SWIG 1.44's
basketoptions.i: GaussianCopulaSpreadEngine(process1, process2, correlation); BasketOption's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Basket/GaussianCopulaSpreadEngineVectorTests.cs."""
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


kirk = _load("kirkengine", "pricingengines/basket/kirkengine.py")
bsp = kirk.bsp


def shared(engine_s):
    """The two processes, the second on the first's risk-free handle."""
    p1 = bsp.process(engine_s["processes"][0])
    s2 = engine_s["processes"][1]
    q = ql.YieldTermStructureHandle(bsp.curve(s2["dividend"]))
    vol = ql.BlackVolTermStructureHandle(bsp.bvts.black_vol(s2["volatility"]))
    p2 = ql.GeneralizedBlackScholesProcess(ql.QuoteHandle(ql.SimpleQuote(s2["x0"])), q, p1.riskFreeRate(), vol)
    return p1, p2


def cases():
    st = v.settings(kirk.R)
    out = []
    for name, option_s, engine_s in kirk.SETUPS:
        option, keep = kirk.bo.option(option_s)
        p1, p2 = shared(engine_s)
        option.setPricingEngine(ql.GaussianCopulaSpreadEngine(p1, p2, engine_s["correlation"]))
        out.append({"name": name, "inputs": {"settings": st, "option": option_s, "engine": engine_s},
                    "expected": {"NPV": option.NPV()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/gaussiancopulaspreadengine.hpp", cases)
