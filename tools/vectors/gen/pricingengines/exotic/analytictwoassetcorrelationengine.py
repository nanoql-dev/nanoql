# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticTwoAssetCorrelationEngine
(ql/pricingengines/exotic/analytictwoassetcorrelationengine.hpp, .cpp), on the evaluation date 15 October 2026: the
engine's NPV for European two-asset correlation calls and puts on two Black-Scholes processes, at first and second
strikes above zero, at correlations from -0.75 to 0.75, on flat and zero curves, flat volatilities and a curve.
QuantLib's engine reads Drezner's bivariate normal, nanoql's West's, so every value carries an override
(docs/porting-playbook.md §7.3): Drezner's accuracy, |Dr78 − We04DP| ≤ 6e-7 times the bivariate terms' coefficients,
relative to the value; 11 cases, 11 overridden outputs. Smiles are left out, where nanoql reads the second volatility at
X₂ and QuantLib at X₁ (tests/NanoQL.Tests/Divergences/TwoAssetCorrelationSmileTests.cs), as are negative second strikes
(TwoAssetCorrelationNegativeStrikeTests.cs). The specs: an option {"type", "strike1", "strike2", "exercise"} and
kirkengine.py's engine. The wheel's calls, by QuantLib-SWIG 1.44's options.i and basketoptions.i:
TwoAssetCorrelationOption(type, strike1, strike2, exercise); AnalyticTwoAssetCorrelationEngine(p1, p2,
QuoteHandle(SimpleQuote(correlation))); NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Exotic/AnalyticTwoAssetCorrelationEngineVectorTests.cs."""
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
bo, bsp = kirk.bo, kirk.bsp
R = kirk.R
CALL, PUT = kirk.CALL, kirk.PUT
REASON = ("QuantLib's engine reads Drezner's bivariate normal, nanoql's West's; |Dr78 - We04DP| <= 6e-7 times the "
          "bivariate terms' coefficients")

PAIR = [bsp.process_spec("BlackScholesMertonProcess", 52.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q),
        bsp.process_spec("BlackScholesMertonProcess", 65.0, bsp.FLAT_R, bsp.bvts.vol_spec_constant(R, 0.3),
                         bsp.flat_spec(R, 0.01))]
ZERO = [bsp.process_spec("BlackScholesMertonProcess", 52.0, bsp.ZERO_R, bsp.CURVE, bsp.ZERO_Q),
        bsp.process_spec("BlackScholesMertonProcess", 65.0, bsp.ZERO_R, bsp.CONSTANT, bsp.ZERO_Q)]

SETUPS = [
    ("call, 50 and 70", CALL, 50.0, 70.0, PAIR, 0.75),
    ("call, 50 and 60", CALL, 50.0, 60.0, PAIR, 0.3),
    ("call, 55 and 70, negative correlation", CALL, 55.0, 70.0, PAIR, -0.5),
    ("call, 45 and 80", CALL, 45.0, 80.0, PAIR, 0.0),
    ("put, 50 and 70", PUT, 50.0, 70.0, PAIR, 0.75),
    ("put, 55 and 60", PUT, 55.0, 60.0, PAIR, -0.75),
    ("put, 48 and 66", PUT, 48.0, 66.0, PAIR, 0.2),
    ("zero curves, call", CALL, 50.0, 70.0, ZERO, 0.6),
    ("zero curves, put", PUT, 50.0, 70.0, ZERO, 0.6),
    ("zero curves, call, low strikes", CALL, 30.0, 40.0, ZERO, -0.3),
    ("zero curves, put, high strikes", PUT, 70.0, 90.0, ZERO, 0.4),
]


def cases():
    st = v.settings(R)
    exercise_s = bo.exercise_spec("European", 548)
    out = []
    for name, t, x1, x2, processes, rho in SETUPS:
        option = ql.TwoAssetCorrelationOption(t, x1, x2, bo.exercise(exercise_s))
        p1, p2 = [bsp.process(p) for p in processes]
        option.setPricingEngine(ql.AnalyticTwoAssetCorrelationEngine(p1, p2, ql.QuoteHandle(ql.SimpleQuote(rho))))
        value = option.NPV()
        coefficients = p2.x0() + x2
        tolerance = 6e-7 * coefficients / max(abs(value), 1.0)
        out.append({"name": name,
                    "inputs": {"settings": st,
                               "option": {"type": t, "strike1": x1, "strike2": x2, "exercise": exercise_s},
                               "engine": kirk.engine_spec(processes, rho)},
                    "expected": {"NPV": {"value": value, "tolerance": tolerance, "reason": REASON}}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/exotic/analytictwoassetcorrelationengine.hpp", cases)
