# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticEuropeanMargrabeEngine (ql/pricingengines/exotic/analyticeuropeanmargrabeengine.hpp,
.cpp), on the evaluation date 15 October 2026: the engine's NPV, deltas, gammas, theta and rho for European Margrabe
options on two Black-Scholes processes, at correlations from -0.8 to 0.8, on flat and zero curves, flat volatilities, a
curve and a surface (the variance read at each spot), at quantities of 1; at other quantities the NPV, the deltas and
the rho, the gammas and the theta left out (QuantLib's drop the quantities or take them squared,
tests/NanoQL.Tests/Divergences/MargrabeGreeksQuantityTests.cs); and an expired option's NPV, theta and rho, its assets'
deltas and gammas left out (QuantLib's never initialized, MargrabeExpiredGreeksTests.cs). Every structure is dated at
the evaluation date and the two processes share a risk-free curve's values (two curves are read Kirk's way,
TwoAssetTwoRiskFreeCurvesTests.cs). The specs: an option {"Q1", "Q2", "exercise"} (basketoption.py's exercise) and an
engine {"processes", "correlation"} (kirkengine.py's). The wheel's calls, by QuantLib-SWIG 1.44's options.i and
basketoptions.i: MargrabeOption(Q1, Q2, exercise); AnalyticEuropeanMargrabeEngine(process1, process2, correlation); NPV,
delta1, delta2, gamma1, gamma2, theta and rho. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Exotic/AnalyticEuropeanMargrabeEngineVectorTests.cs."""
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
bo, fd2 = kirk.bo, kirk.fd2

SETUPS = [
    ("flat, correlation 0.5", 1, 1, fd2.FLAT_PAIR, 0.5),
    ("flat, correlation -0.8", 1, 1, fd2.FLAT_PAIR, -0.8),
    ("flat, correlation 0.8", 1, 1, fd2.FLAT_PAIR, 0.8),
    ("zero curves, a surface", 1, 1, fd2.ZERO_PAIR, 0.3),
    ("two surfaces", 1, 1, fd2.SURFACE_PAIR, -0.2),
    ("quantities 2 and 1", 2, 1, fd2.FLAT_PAIR, 0.5),
    ("quantities 1 and 3", 1, 3, fd2.FLAT_PAIR, 0.5),
    ("quantities 2 and 3", 2, 3, fd2.ZERO_PAIR, -0.4),
]


def cases():
    st = v.settings(kirk.R)
    exercise_s = bo.exercise_spec("European", 548)
    out = []
    for name, q1, q2, processes, rho in SETUPS:
        option = ql.MargrabeOption(q1, q2, bo.exercise(exercise_s))
        p1, p2 = [kirk.bsp.process(p) for p in processes]
        option.setPricingEngine(ql.AnalyticEuropeanMargrabeEngine(p1, p2, rho))
        expected = {"NPV": option.NPV(), "delta1": option.delta1(), "delta2": option.delta2(), "rho": option.rho()}
        if q1 == 1 and q2 == 1:
            expected.update({"gamma1": option.gamma1(), "gamma2": option.gamma2(), "theta": option.theta()})
        out.append({"name": name,
                    "inputs": {"settings": st, "option": {"Q1": q1, "Q2": q2, "exercise": exercise_s},
                               "engine": kirk.engine_spec(processes, rho)},
                    "expected": expected})
    expired_s = bo.exercise_spec("European", -1)
    option = ql.MargrabeOption(1, 1, bo.exercise(expired_s))
    p1, p2 = [kirk.bsp.process(p) for p in fd2.FLAT_PAIR]
    option.setPricingEngine(ql.AnalyticEuropeanMargrabeEngine(p1, p2, 0.5))
    out.append({"name": "expired",
                "inputs": {"settings": st, "option": {"Q1": 1, "Q2": 1, "exercise": expired_s},
                           "engine": kirk.engine_spec(fd2.FLAT_PAIR, 0.5)},
                "expected": {"NPV": option.NPV(), "theta": option.theta(), "rho": option.rho()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/exotic/analyticeuropeanmargrabeengine.hpp", cases)
