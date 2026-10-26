# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OperatorSplittingSpreadEngine (ql/pricingengines/basket/operatorsplittingspreadengine.hpp,
.cpp), on the evaluation date 15 October 2026: the engine's NPV at both orders on kirkengine.py's setups, each second
forward and strike summing above zero and each second volatility above zero (where they don't, QuantLib's NaN is
nanoql's refusal or limit, tests/NanoQL.Tests/Divergences/SpreadApproximationStrikeTests.cs and
OperatorSplittingZeroVolatilityTests.cs), the engine spec adding "order" ("First" or "Second"). The wheel's calls, by
QuantLib-SWIG 1.44's basketoptions.i: OperatorSplittingSpreadEngine(process1, process2, correlation, order);
BasketOption's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Basket/OperatorSplittingSpreadEngineVectorTests.cs."""
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

ORDERS = {"First": ql.OperatorSplittingSpreadEngine.First, "Second": ql.OperatorSplittingSpreadEngine.Second}


def cases():
    out = []
    for order, value in ORDERS.items():
        setups = [(f"{name}, {order.lower()} order", option, dict(engine, order=order))
                  for name, option, engine in kirk.SETUPS]
        out += kirk.spread_cases(lambda p1, p2, rho, value=value: ql.OperatorSplittingSpreadEngine(p1, p2, rho, value),
                                 setups)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/operatorsplittingspreadengine.hpp", cases)
