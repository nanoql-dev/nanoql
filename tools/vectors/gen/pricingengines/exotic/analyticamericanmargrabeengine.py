# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticAmericanMargrabeEngine (ql/pricingengines/exotic/analyticamericanmargrabeengine.hpp,
.cpp), on the evaluation date 15 October 2026: the engine's NPV for American Margrabe options on two Black-Scholes
processes, by Bjerksund and Stensland's approximation on the transformed market, at quantities of 1 and others, at
correlations of either sign, with dividend yields of either size, on flat and zero curves, flat volatilities and
surfaces, and an expired option. Every structure is dated at the evaluation date, where nanoql's clock is QuantLib's,
and the two processes share a risk-free curve's values. The specs are analyticeuropeanmargrabeengine.py's, the exercise
American from the evaluation date. The wheel's calls, by QuantLib-SWIG 1.44's options.i and basketoptions.i:
MargrabeOption(Q1, Q2, exercise); AnalyticAmericanMargrabeEngine(process1, process2, correlation); NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Exotic/AnalyticAmericanMargrabeEngineVectorTests.cs."""
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
stulz = _load("stulzengine", "pricingengines/basket/stulzengine.py")
bo, fd2 = kirk.bo, kirk.fd2

SETUPS = [
    ("flat, correlation 0.5", 1, 1, fd2.FLAT_PAIR, 0.5),
    ("flat, correlation -0.6", 1, 1, fd2.FLAT_PAIR, -0.6),
    ("high dividend", 1, 1, stulz.HIGH_Q, 0.3),
    ("zero curves, a surface", 1, 1, fd2.ZERO_PAIR, 0.2),
    ("quantities 2 and 1", 2, 1, fd2.FLAT_PAIR, 0.5),
    ("quantities 1 and 2", 1, 2, stulz.HIGH_Q, -0.2),
]


def cases():
    st = v.settings(kirk.R)
    exercise_s = bo.exercise_spec("American", 365, earliest=0)
    out = []
    for name, q1, q2, processes, rho in SETUPS:
        option = ql.MargrabeOption(q1, q2, bo.exercise(exercise_s))
        p1, p2 = [kirk.bsp.process(p) for p in processes]
        option.setPricingEngine(ql.AnalyticAmericanMargrabeEngine(p1, p2, rho))
        out.append({"name": name,
                    "inputs": {"settings": st, "option": {"Q1": q1, "Q2": q2, "exercise": exercise_s},
                               "engine": kirk.engine_spec(processes, rho)},
                    "expected": {"NPV": option.NPV()}})
    expired_s = bo.exercise_spec("American", -1, earliest=-30)
    option = ql.MargrabeOption(2, 1, bo.exercise(expired_s))
    p1, p2 = [kirk.bsp.process(p) for p in fd2.FLAT_PAIR]
    option.setPricingEngine(ql.AnalyticAmericanMargrabeEngine(p1, p2, 0.5))
    out.append({"name": "expired",
                "inputs": {"settings": st, "option": {"Q1": 2, "Q2": 1, "exercise": expired_s},
                           "engine": kirk.engine_spec(fd2.FLAT_PAIR, 0.5)},
                "expected": {"NPV": option.NPV()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/exotic/analyticamericanmargrabeengine.hpp", cases)
