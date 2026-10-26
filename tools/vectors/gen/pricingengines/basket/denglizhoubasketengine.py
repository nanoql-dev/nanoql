# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DengLiZhouBasketEngine (ql/pricingengines/basket/denglizhoubasketengine.hpp, .cpp), on the
evaluation date 15 October 2026: the engine's NPV for European spread and average basket options on two, three and
four Black-Scholes processes, with one positive weight or several (lumped by Lo's approximation), at positive strikes
and at a negative strike with two negative weights (QuantLib's branch, its strike a zero-volatility asset). A negative
strike with exactly one negative weight is left out, where nanoql prices the payoff's identity
(tests/NanoQL.Tests/Divergences/DengLiZhouNegativeStrikeTests.cs), as is a zero volatility of the positive leg
(DengLiZhouZeroVolatilityTests.cs). Every process shares one risk-free curve (the engine refuses others). The specs are
choibasketengine.py's. The wheel's calls, by QuantLib-SWIG 1.44's basketoptions.i: DengLiZhouBasketEngine(processes,
rho); BasketOption's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Basket/DengLiZhouBasketEngineVectorTests.cs."""
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


choi = _load("choibasketengine", "pricingengines/basket/choibasketengine.py")
bo, bc, nd = choi.bo, choi.bc, choi.nd
CALL, PUT = choi.CALL, choi.PUT


def cases():
    st = v.settings(choi.R)
    european = bo.exercise_spec("European", 548)
    rho2 = [[1.0, 0.5], [0.5, 1.0]]
    plain = bc.payoff_spec
    setups = [
        ("spread call", bo.payoff_spec("Spread", plain("PlainVanilla", CALL, 5.0)), choi.fd2.FLAT_PAIR, rho2),
        ("spread put", bo.payoff_spec("Spread", plain("PlainVanilla", PUT, 5.0)), choi.fd2.FLAT_PAIR, rho2),
        ("one positive weight, three assets",
         bo.payoff_spec("Average", plain("PlainVanilla", PUT, 20.0), weights=[1.0, -0.5, -0.25]), nd.THREE, nd.RHO3),
        ("two positive weights, three assets",
         bo.payoff_spec("Average", plain("PlainVanilla", CALL, 10.0), weights=[0.6, 0.5, -0.8]), nd.THREE, nd.RHO3),
        ("negative strike, two negative weights",
         bo.payoff_spec("Average", plain("PlainVanilla", CALL, -10.0), weights=[1.0, -0.5, -0.25]), nd.THREE,
         nd.RHO3),
        ("four assets, two of each sign",
         bo.payoff_spec("Average", plain("PlainVanilla", PUT, 20.0), weights=[1.0, -2.0, -1.0, 4.0]), choi.FOUR,
         choi.RHO4),
        ("zero curves, three assets",
         bo.payoff_spec("Average", plain("PlainVanilla", CALL, 15.0), weights=[1.0, -0.4, -0.3]), choi.ZERO3,
         nd.RHO3),
    ]
    out = []
    for name, payoff, processes, rho in setups:
        option_s = bo.option_spec(payoff, european)
        option, keep = bo.option(option_s)
        ps = [choi.bsp.process(p) for p in processes]
        option.setPricingEngine(ql.DengLiZhouBasketEngine(ps, ql.Matrix(rho)))
        out.append({"name": name, "inputs": {"settings": st, "option": option_s,
                                             "engine": {"processes": processes, "rho": rho}},
                    "expected": {"NPV": option.NPV()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/denglizhoubasketengine.hpp", cases)
