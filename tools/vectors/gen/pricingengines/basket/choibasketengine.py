# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ChoiBasketEngine (ql/pricingengines/basket/choibasketengine.hpp, .cpp), on the evaluation date
15 October 2026: Choi's engine's NPV for European spread and average basket options on two, three and four Black-Scholes
processes, with positive and negative weights and strikes, at the default scale (10) and others, with integration-step
limits, with the forward deltas computed and with their control variate (which moves the value).

Every structure is on Act/365 (Fixed) from the evaluation date, and every process shares one risk-free curve (the engine
refuses others). The forward deltas, which the wheel's options don't return, are QuantLib's
testGoldenChoiBasketEngineExample's (tests/NanoQL.Tests/TestSuite/BasketOptionTests.cs).

Every node prices by the single-factor engine's formula at the root of a sum of exponentials, found by Brent's solver,
so the stability filter applies: each case is refitted with each spot, the strike, each weight and each correlation (its
two halves together) moved one ulp up and down, and a value that moves by more than 1e-12 relative in any refit is
dropped, QuantLib's own cases covering the solve.

The specs are tools/vectors/gen/instruments/basketoption.py's options and an engine {"processes", "rho", "lambda",
"maxNrIntegrationSteps" (null for QuantLib's default, the largest Size), "calcFwdDelta", "controlVariate"}. The wheel's
calls, by QuantLib-SWIG 1.44's basketoptions.i: ChoiBasketEngine(processes, rho, lambda, maxNrIntegrationSteps,
calcFwdDelta, controlVariate), the C++ constructor; BasketOption's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Basket/ChoiBasketEngineVectorTests.cs."""
import copy
import importlib.util
import math
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
fd2, bo, bc, bsp, bvts = nd.fd2, nd.bo, nd.bc, nd.bsp, nd.bvts
R = bo.R
CALL, PUT = bo.CALL, bo.PUT

FOUR = nd.THREE + [bsp.process_spec("BlackScholesMertonProcess", 25.0, bsp.FLAT_R, bvts.vol_spec_constant(R, 0.2),
                                    bsp.flat_spec(R, 0.02))]
RHO4 = [[1.0, 0.2, 0.3, 0.0], [0.2, 1.0, -0.3, 0.1], [0.3, -0.3, 1.0, 0.7], [0.0, 0.1, 0.7, 1.0]]
ZERO3 = [bsp.process_spec("BlackScholesMertonProcess", s, bsp.ZERO_R, vol, bsp.ZERO_Q)
         for s, vol in ((100.0, bsp.CONSTANT), (90.0, bsp.SURFACE), (80.0, bsp.CURVE))]


def engine_spec(processes, rho, lam=10.0, max_steps=None, fwd_delta=False, control=False):
    return {"processes": processes, "rho": rho, "lambda": lam, "maxNrIntegrationSteps": max_steps,
            "calcFwdDelta": fwd_delta, "controlVariate": control}


def results(option_s, engine_s):
    option, keep = bo.option(option_s)
    processes = [bsp.process(p) for p in engine_s["processes"]]
    rho = ql.Matrix(engine_s["rho"])
    if engine_s["maxNrIntegrationSteps"] is None:
        engine = ql.ChoiBasketEngine(processes, rho, engine_s["lambda"])
        if engine_s["calcFwdDelta"] or engine_s["controlVariate"]:
            raise ValueError("the deltas' flags need a step limit")
    else:
        engine = ql.ChoiBasketEngine(processes, rho, engine_s["lambda"], engine_s["maxNrIntegrationSteps"],
                                     engine_s["calcFwdDelta"], engine_s["controlVariate"])
    option.setPricingEngine(engine)
    return {"NPV": option.NPV()}


def refits(option_s, engine_s):
    out = []
    for d in (math.inf, -math.inf):
        for i in range(len(engine_s["processes"])):
            e = copy.deepcopy(engine_s)
            e["processes"][i]["x0"] = math.nextafter(e["processes"][i]["x0"], d)
            out.append((option_s, e))
        o = copy.deepcopy(option_s)
        o["payoff"]["base"]["strike"] = math.nextafter(o["payoff"]["base"]["strike"], d)
        out.append((o, engine_s))
        for i in range(len(option_s["payoff"].get("weights", []))):
            o = copy.deepcopy(option_s)
            o["payoff"]["weights"][i] = math.nextafter(o["payoff"]["weights"][i], d)
            out.append((o, engine_s))
        n = len(engine_s["rho"])
        for i in range(n):
            for j in range(i + 1, n):
                e = copy.deepcopy(engine_s)
                e["rho"][i][j] = e["rho"][j][i] = math.nextafter(e["rho"][i][j], d)
                out.append((option_s, e))
    return out


def stable(a, b):
    return abs(a - b) <= 1e-12 * max(abs(a), abs(b), 1.0)


def cases():
    st = v.settings(R)
    european = bo.exercise_spec("European", 548)
    rho2 = [[1.0, 0.5], [0.5, 1.0]]
    rho3 = nd.RHO3
    spread_call = bo.payoff_spec("Spread", bc.payoff_spec("PlainVanilla", CALL, 5.0))
    spread_put = bo.payoff_spec("Spread", bc.payoff_spec("PlainVanilla", PUT, 5.0))
    spread_negative = bo.payoff_spec("Spread", bc.payoff_spec("PlainVanilla", CALL, -10.0))
    avg2 = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", CALL, 100.0), weights=[0.6, 0.4])
    avg3_call = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", CALL, 100.0), n=3)
    avg3_mixed = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", PUT, 20.0), weights=[1.0, -0.5, -0.25])
    golden_put = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", PUT, 20.0), weights=[1.0, -2.0, -1.0, 4.0])
    golden_call = bo.payoff_spec("Average", bc.payoff_spec("PlainVanilla", CALL, 20.0), weights=[1.0, -2.0, -1.0, 4.0])
    flat2 = fd2.FLAT_PAIR
    setups = [
        ("spread call", spread_call, engine_spec(flat2, rho2)),
        ("spread put", spread_put, engine_spec(flat2, rho2)),
        ("spread call, negative strike", spread_negative, engine_spec(flat2, rho2)),
        ("spread call, lambda 5", spread_call, engine_spec(flat2, [[1.0, -0.4], [-0.4, 1.0]], lam=5.0)),
        ("average call, two assets", avg2, engine_spec(flat2, rho2)),
        ("average call, three assets", avg3_call, engine_spec(nd.THREE, rho3)),
        ("mixed weights put, three assets", avg3_mixed, engine_spec(nd.THREE, rho3, lam=8.0)),
        ("zero curves, average call", avg3_call, engine_spec(ZERO3, rho3)),
        ("four assets, put, deltas and control variate", golden_put,
         engine_spec(FOUR, RHO4, lam=7.0, max_steps=10000, fwd_delta=True, control=True)),
        ("four assets, call, deltas and control variate", golden_call,
         engine_spec(FOUR, RHO4, lam=7.0, max_steps=10000, fwd_delta=True, control=True)),
        ("four assets, call, deltas", golden_call, engine_spec(FOUR, RHO4, lam=7.0, max_steps=10000, fwd_delta=True)),
        ("four assets, call, at most 500 steps", golden_call, engine_spec(FOUR, RHO4, max_steps=500)),
    ]
    out = []
    for name, payoff, engine in setups:
        option = bo.option_spec(payoff, european)
        base = results(option, engine)
        moved = [results(o, e) for o, e in refits(option, engine)]
        expected = {k: x for k, x in base.items() if all(stable(x, m[k]) for m in moved)}
        out.append({"name": name, "inputs": {"settings": st, "option": option, "engine": engine},
                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/choibasketengine.hpp", cases)
