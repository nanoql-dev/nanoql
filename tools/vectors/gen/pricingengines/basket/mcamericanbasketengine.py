# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCAmericanBasketEngine (ql/pricingengines/basket/mcamericanbasketengine.hpp, .cpp), on the
evaluation date 15 October 2026: the Longstaff-Schwartz engine's NPV and error estimate for American maximum calls,
minimum puts and average puts on two and three Black-Scholes processes, pseudo-random, with and without antithetic
variates, on monomial, Laguerre and Hermite bases of orders 2 and 3, on fixed seeds.
QuantLib's engine takes no calibration seed; its base's default is the narrowed null, 2147483647 through the wheel
(the S-van plan's VR4 (v)), which the reader passes, nanoql's default being the documented one. A zero or negative
strike is left out, where nanoql scales the regression by 1/|K| (tests/NanoQL.Tests/Divergences/
AmericanBasketStrikeScalingTests.cs). The specs are tools/vectors/gen/instruments/basketoption.py's options and an
engine {"processes", "rho", "timeSteps", "antitheticVariate", "requiredSamples", "seed", "nCalibrationSamples",
"polynomialOrder", "polynomialType"}. The wheel's calls, by QuantLib-SWIG 1.44's basketoptions.i:
StochasticProcessArray(processes, rho); MCAmericanBasketEngine(process, "pseudorandom", timeSteps=...,
antitheticVariate=..., requiredSamples=..., seed=..., nCalibrationSamples=..., polynomOrder=..., polynomType=...);
BasketOption's NPV and errorEstimate. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Basket/MCAmericanBasketEngineVectorTests.cs."""
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


nd = _load("fdndimblackscholesvanillaengine", "pricingengines/basket/fdndimblackscholesvanillaengine.py")
fd2, bo, bc, bsp = nd.fd2, nd.bo, nd.bc, nd.bsp
R = bo.R
CALL, PUT = bo.CALL, bo.PUT
TYPES = {"Monomial": ql.LsmBasisSystem.Monomial, "Laguerre": ql.LsmBasisSystem.Laguerre,
         "Hermite": ql.LsmBasisSystem.Hermite}


def engine_spec(processes, rho, steps, antithetic, samples, seed, calibration, order=2, kind="Monomial"):
    return {"processes": processes, "rho": rho, "timeSteps": steps, "antitheticVariate": antithetic,
            "requiredSamples": samples, "seed": seed, "nCalibrationSamples": calibration, "polynomialOrder": order,
            "polynomialType": kind}


def results(option_s, engine_s):
    option, keep = bo.option(option_s)
    ps = [bsp.process(p) for p in engine_s["processes"]]
    array = ql.StochasticProcessArray(ps, ql.Matrix(engine_s["rho"]))
    option.setPricingEngine(ql.MCAmericanBasketEngine(
        array, "pseudorandom", timeSteps=engine_s["timeSteps"], antitheticVariate=engine_s["antitheticVariate"],
        requiredSamples=engine_s["requiredSamples"], seed=engine_s["seed"],
        nCalibrationSamples=engine_s["nCalibrationSamples"], polynomOrder=engine_s["polynomialOrder"],
        polynomType=TYPES[engine_s["polynomialType"]]))
    return {"NPV": option.NPV(), "errorEstimate": option.errorEstimate()}


def cases():
    st = v.settings(R)
    american = bo.exercise_spec("American", 365, earliest=0)
    rho2 = [[1.0, 0.5], [0.5, 1.0]]
    max_call = bo.payoff_spec("Max", bc.payoff_spec("PlainVanilla", CALL, 100.0))
    min_put = bo.payoff_spec("Min", bc.payoff_spec("PlainVanilla", PUT, 100.0))
    avg_put = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", PUT, 100.0), n=3)
    setups = [
        ("maximum call, antithetic", max_call, engine_spec(fd2.FLAT_PAIR, rho2, 20, True, 2000, 42, 500)),
        ("maximum call, plain", max_call, engine_spec(fd2.FLAT_PAIR, rho2, 20, False, 2000, 7, 500)),
        ("minimum put, Laguerre order 3", min_put,
         engine_spec(fd2.FLAT_PAIR, [[1.0, -0.3], [-0.3, 1.0]], 16, True, 1500, 1234, 400, 3, "Laguerre")),
        ("minimum put, Hermite", min_put, engine_spec(fd2.ZERO_PAIR, rho2, 12, True, 1000, 99, 300, 2, "Hermite")),
        ("average put, three assets", avg_put, engine_spec(nd.THREE, nd.RHO3, 10, True, 1000, 42, 300)),
    ]
    out = []
    for name, payoff, engine in setups:
        option_s = bo.option_spec(payoff, american)
        out.append({"name": name, "inputs": {"settings": st, "option": option_s, "engine": engine},
                    "expected": results(option_s, engine)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/mcamericanbasketengine.hpp", cases)
