# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's KirkEngine (ql/pricingengines/basket/kirkengine.hpp, .cpp), on the evaluation date 15 October
2026: Kirk's NPV for European spread calls and puts on two Black-Scholes processes, at positive, zero and negative
strikes (the second forward and the strike summing above zero), at correlations from -0.95 to 0.95, on flat and zero
curves, a flat volatility, a curve and a surface (the variance read at each forward).
Every structure is dated at the evaluation date. The setups, which the other spread engines' generators share
(bjerksundstenslandspreadengine.py, operatorsplittingspreadengine.py, pearsonspreadengine.py): a basket option spec of
tools/vectors/gen/instruments/basketoption.py on a spread payoff and an engine {"processes" (two
tools/vectors/gen/processes/blackscholesprocess.py specs), "correlation"}. The wheel's calls, by QuantLib-SWIG 1.44's
basketoptions.i: KirkEngine(process1, process2, correlation), the C++ constructor; BasketOption's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Basket/KirkEngineVectorTests.cs."""
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


fd2 = _load("fd2dblackscholesvanillaengine", "pricingengines/basket/fd2dblackscholesvanillaengine.py")
bo, bc, bsp = fd2.bo, fd2.bc, fd2.bsp
R = bo.R
CALL, PUT = bo.CALL, bo.PUT


def spread(option_type, strike):
    return bo.option_spec(bo.payoff_spec("Spread", bc.payoff_spec("PlainVanilla", option_type, strike)),
                          bo.exercise_spec("European", 548))


def engine_spec(processes, correlation):
    return {"processes": processes, "correlation": correlation}


SETUPS = [
    ("flat, call, strike 5", spread(CALL, 5.0), engine_spec(fd2.FLAT_PAIR, 0.5)),
    ("flat, put, strike 5", spread(PUT, 5.0), engine_spec(fd2.FLAT_PAIR, 0.5)),
    ("flat, call, strike 0", spread(CALL, 0.0), engine_spec(fd2.FLAT_PAIR, -0.3)),
    ("flat, put, strike -20", spread(PUT, -20.0), engine_spec(fd2.FLAT_PAIR, 0.8)),
    ("flat, call, correlation 0.95", spread(CALL, 5.0), engine_spec(fd2.FLAT_PAIR, 0.95)),
    ("flat, put, correlation -0.95", spread(PUT, 5.0), engine_spec(fd2.FLAT_PAIR, -0.95)),
    ("zero curves, a surface, call, strike 3", spread(CALL, 3.0), engine_spec(fd2.ZERO_PAIR, 0.3)),
    ("two surfaces, put, strike 10", spread(PUT, 10.0), engine_spec(fd2.SURFACE_PAIR, -0.6)),
]


def processes(engine_s):
    return [bsp.process(p) for p in engine_s["processes"]]


def spread_cases(make_engine, setups=None, outputs=("NPV",)):
    """Each setup priced by the engine make_engine(process1, process2, correlation) builds."""
    st = v.settings(R)
    out = []
    for name, option_s, engine_s in (setups or SETUPS):
        option, keep = bo.option(option_s)
        p1, p2 = processes(engine_s)
        option.setPricingEngine(make_engine(p1, p2, engine_s["correlation"]))
        out.append({"name": name, "inputs": {"settings": st, "option": option_s, "engine": engine_s},
                    "expected": {"NPV": option.NPV()}})
    return out


def cases():
    return spread_cases(ql.KirkEngine)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/kirkengine.hpp", cases)
