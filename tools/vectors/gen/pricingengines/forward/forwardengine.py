# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ForwardVanillaEngine as ForwardEuropeanEngine (ql/pricingengines/forward/forwardengine.hpp,
over AnalyticEuropeanEngine), on the evaluation date 15 October 2026: forward-start calls and puts at moneyness 0.9, 1
and 1.1, reset today, in three months or in six, expiring in six months, a year or two, on two flat markets and one of
zero curves under a flat volatility: the NPV, delta, gamma, theta, vega, rho and dividend rho.

Left out (the S-exo plan's §2.10): volatilities off flat, where QuantLib's vega is the derivative in the period's
forward volatility (tests/NanoQL.Tests/Divergences/ForwardVegaTermStructureTests.cs); payoffs that aren't plain
vanilla, which nanoql refuses (ForwardNonPlainPayoffTests.cs).

The setups, which mcforwardeuropeanbsengine.py, mcforwardeuropeanhestonengine.py and
tools/vectors/gen/pricingengines/quanto/quantoengine.py share: an option {"moneyness", "resetDate", "payoff" (a
tools/vectors/gen/pricingengines/blackcalculator.py plain-vanilla spec, its strike 0, which the engine ignores),
"exercise" (European)} and a tools/vectors/gen/processes/blackscholesprocess.py process spec. The wheel's calls, by
QuantLib-SWIG 1.44's options.i: ForwardVanillaOption(moneyness, resetDate, payoff, exercise),
ForwardEuropeanEngine(process), and the option's NPV and Greeks. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Forward/ForwardVanillaEngineVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
GREEKS = ["delta", "gamma", "theta", "vega", "rho", "dividendRho"]

MARKETS = [
    ("flat", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q)),
    ("flat, high volatility", bsp.process_spec("BlackScholesMertonProcess", 80.0, bsp.flat_spec(R, 0.06),
                                               bsp.bvts.vol_spec_constant(R, 0.45), bsp.flat_spec(R, 0.0))),
    ("zero curves", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.CONSTANT, bsp.ZERO_Q)),
]
PERIODS = [(0, 182), (91, 365), (182, 730)]
MONEYNESS = [0.9, 1.0, 1.1]


def option_spec(option_type, moneyness, reset, expiry):
    return {"moneyness": moneyness, "resetDate": (R + reset).serialNumber(),
            "payoff": bc.payoff_spec("PlainVanilla", option_type, 0.0),
            "exercise": {"type": "European", "date": (R + expiry).serialNumber()}}


def option(spec, cls=ql.ForwardVanillaOption):
    return cls(spec["moneyness"], ql.Date(spec["resetDate"]), bc.payoff(spec["payoff"]),
               ql.EuropeanExercise(ql.Date(spec["exercise"]["date"])))


def setups(markets=MARKETS, periods=PERIODS, moneyness=MONEYNESS):
    """Every case's name, option spec and process spec."""
    out = []
    for mname, pspec in markets:
        for reset, expiry in periods:
            for t in (CALL, PUT):
                for m in moneyness:
                    name = f"{mname}, reset {reset}, expiry {expiry}, {'call' if t == CALL else 'put'}, moneyness {m:g}"
                    out.append((name, option_spec(t, m, reset, expiry), pspec))
    return out


def cases():
    st = v.settings(R)
    out = []
    for name, ospec, pspec in setups():
        o = option(ospec)
        o.setPricingEngine(ql.ForwardEuropeanEngine(bsp.process(pspec)))
        expected = {"NPV": o.NPV()}
        for g in GREEKS:
            expected[g] = v.result(lambda: getattr(o, g)())
        out.append({"name": name, "inputs": {"settings": st, "option": ospec, "process": pspec}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/forward/forwardengine.hpp", cases)
