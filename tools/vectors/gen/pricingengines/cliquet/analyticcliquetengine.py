# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticCliquetEngine (ql/pricingengines/cliquet/analyticcliquetengine.hpp, .cpp), on the
evaluation date 15 October 2026: cliquet calls and puts at moneyness 0.9, 1 and 1.1 on three reset schedules (one
reset at six months; quarterly over a year; quarterly over two years from three months), on two flat markets and one
of zero curves under a flat volatility: the NPV, delta, gamma, theta, vega, rho and dividend rho.

The outputs nanoql fixes are left out: the theta on the zero curves, where QuantLib reads each period's dividend rate
over the period after its reset and nanoql the rate to it (tests/NanoQL.Tests/Divergences/CliquetThetaTests.cs);
volatilities off flat, where QuantLib's vega is the derivative in each period's forward volatility
(ForwardVegaTermStructureTests.cs).

The setups, which analyticperformanceengine.py and mcperformanceengine.py share: an option {"payoff" (a
tools/vectors/gen/pricingengines/blackcalculator.py percentage-strike spec), "exercise" (European, its date),
"resets" (serial dates)} and a tools/vectors/gen/processes/blackscholesprocess.py process spec. The wheel's calls, by
QuantLib-SWIG 1.44's options.i: CliquetOption(payoff, exercise, resetDates), AnalyticCliquetEngine(process), and the
option's NPV and Greeks. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Cliquet/AnalyticCliquetEngineVectorTests.cs."""
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
    ("flat", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q), True),
    ("flat, high volatility", bsp.process_spec("BlackScholesMertonProcess", 80.0, bsp.flat_spec(R, 0.06),
                                               bsp.bvts.vol_spec_constant(R, 0.45), bsp.flat_spec(R, 0.0)), True),
    ("zero curves", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.CONSTANT, bsp.ZERO_Q), False),
]
SCHEDULES = [
    ("one reset", [182], 365),
    ("quarterly", [91, 182, 273], 365),
    ("two years", [91, 182, 273, 365, 456, 547, 638], 730),
]
MONEYNESS = [90.0, 100.0, 110.0]


def option_spec(option_type, strike, resets, expiry):
    return {"payoff": bc.payoff_spec("PercentageStrike", option_type, strike),
            "exercise": {"type": "European", "date": (R + expiry).serialNumber()},
            "resets": [(R + d).serialNumber() for d in resets]}


def option(spec):
    return ql.CliquetOption(bc.payoff(spec["payoff"]), ql.EuropeanExercise(ql.Date(spec["exercise"]["date"])),
                            [ql.Date(d) for d in spec["resets"]])


def setups():
    """Every case's name, option spec, process spec and whether its curves are flat."""
    out = []
    for mname, pspec, flat in MARKETS:
        for sname, resets, expiry in SCHEDULES:
            for t in (CALL, PUT):
                for k in MONEYNESS:
                    name = f"{mname}, {sname}, {'call' if t == CALL else 'put'}, moneyness {k / 100.0:g}"
                    out.append((name, option_spec(t, k, resets, expiry), pspec, flat))
    return out


def cases():
    st = v.settings(R)
    out = []
    for name, ospec, pspec, flat in setups():
        o = option(ospec)
        o.setPricingEngine(ql.AnalyticCliquetEngine(bsp.process(pspec)))
        expected = {"NPV": o.NPV()}
        for g in GREEKS:
            if g == "theta" and not flat:
                continue
            expected[g] = v.result(lambda: getattr(o, g)())
        out.append({"name": name, "inputs": {"settings": st, "option": ospec, "process": pspec}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/cliquet/analyticcliquetengine.hpp", cases)
