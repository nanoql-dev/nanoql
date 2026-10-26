# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's StulzEngine (ql/pricingengines/basket/stulzengine.hpp, .cpp), on the evaluation date 15 October
2026: Stulz's NPV for European calls and puts on the minimum and the maximum of two Black-Scholes processes, at
positive and zero strikes, at correlations from -0.9 to 0.9, with dividend yields of either size (QuantLib's comment
"cannot handle non zero dividends" notwithstanding), on flat and zero curves, flat volatilities and surfaces (the
variance read at the strike). The degenerate inputs, a zero volatility or assets moving together, and negative strikes
are left out, where nanoql takes QuantLib's NaN or refusal at its limit
(tests/NanoQL.Tests/Divergences/StulzDegenerateTests.cs, StulzNegativeStrikeTests.cs). The specs are kirkengine.py's,
the payoff a "Min" or "Max" basket. The wheel's calls, by QuantLib-SWIG 1.44's basketoptions.i: StulzEngine(process1,
process2, correlation); BasketOption's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Basket/StulzEngineVectorTests.cs."""
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
bo, bc, bsp, fd2 = kirk.bo, kirk.bc, kirk.bsp, kirk.fd2
CALL, PUT = kirk.CALL, kirk.PUT
R = kirk.R

HIGH_Q = [bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT, bsp.flat_spec(R, 0.08)),
          bsp.process_spec("BlackScholesMertonProcess", 95.0, bsp.FLAT_R, bsp.bvts.vol_spec_constant(R, 0.3),
                           bsp.flat_spec(R, 0.01))]


def option(kind, option_type, strike):
    return bo.option_spec(bo.payoff_spec(kind, bc.payoff_spec("PlainVanilla", option_type, strike)),
                          bo.exercise_spec("European", 548))


def cases():
    setups = []
    for kind in ("Min", "Max"):
        for t, tname in ((CALL, "call"), (PUT, "put")):
            k = kind.lower()
            setups += [
                (f"{k} {tname}, flat, strike 100", option(kind, t, 100.0), kirk.engine_spec(fd2.FLAT_PAIR, 0.5)),
                (f"{k} {tname}, flat, strike 0", option(kind, t, 0.0), kirk.engine_spec(fd2.FLAT_PAIR, -0.3)),
                (f"{k} {tname}, high dividend, correlation 0.9", option(kind, t, 90.0), kirk.engine_spec(HIGH_Q, 0.9)),
                (f"{k} {tname}, zero curves, a surface, correlation -0.9", option(kind, t, 105.0),
                 kirk.engine_spec(fd2.ZERO_PAIR, -0.9)),
                (f"{k} {tname}, two surfaces", option(kind, t, 100.0), kirk.engine_spec(fd2.SURFACE_PAIR, 0.2)),
            ]
    return kirk.spread_cases(ql.StulzEngine, setups)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/stulzengine.hpp", cases)
