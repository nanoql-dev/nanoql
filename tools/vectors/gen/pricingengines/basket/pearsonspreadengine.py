# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PearsonSpreadEngine (ql/pricingengines/basket/pearsonspreadengine.hpp, .cpp), on the
evaluation date 15 October 2026: the engine's NPV, at its default quadrature, on kirkengine.py's setups but the put at a
negative strike, whose conditional strike falls below zero within the integral's range (QuantLib's put takes the
call's intrinsic value there, tests/NanoQL.Tests/Divergences/PearsonNegativeStrikePutTests.cs). The wheel's calls, by
QuantLib-SWIG 1.44's basketoptions.i: PearsonSpreadEngine(process1, process2, correlation); BasketOption's NPV. Read
back by tests/NanoQL.Tests/Vectors/PricingEngines/Basket/PearsonSpreadEngineVectorTests.cs."""
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
    setups = [s for s in kirk.SETUPS
              if not (s[1]["payoff"]["base"]["optionType"] == kirk.PUT and s[1]["payoff"]["base"]["strike"] < 0.0)]
    return kirk.spread_cases(ql.PearsonSpreadEngine, setups)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/pearsonspreadengine.hpp", cases)
