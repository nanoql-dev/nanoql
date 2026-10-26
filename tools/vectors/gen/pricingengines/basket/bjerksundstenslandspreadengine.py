# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BjerksundStenslandSpreadEngine (ql/pricingengines/basket/bjerksundstenslandspreadengine.hpp,
.cpp), on the evaluation date 15 October 2026: the engine's NPV on kirkengine.py's setups, each second forward and
strike summing above zero (where they don't, QuantLib's NaN is nanoql's refusal,
tests/NanoQL.Tests/Divergences/SpreadApproximationStrikeTests.cs). The wheel's calls, by QuantLib-SWIG 1.44's
basketoptions.i: BjerksundStenslandSpreadEngine(process1, process2, correlation); BasketOption's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Basket/BjerksundStenslandSpreadEngineVectorTests.cs."""
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

def cases():
    return kirk.spread_cases(ql.BjerksundStenslandSpreadEngine)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/bjerksundstenslandspreadengine.hpp", cases)
