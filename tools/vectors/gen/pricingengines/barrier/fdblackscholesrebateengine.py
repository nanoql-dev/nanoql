# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdBlackScholesRebateEngine (ql/pricingengines/barrier/fdblackscholesrebateengine.hpp,
.cpp), on the evaluation date 15 October 2026: the value of a barrier option's rebate, with its delta, gamma and theta.

The four barrier types (at 85% of the spot below it, 115% above) with rebates of 3% and 7% of the spot, on a call and a
put at the money, a year out, on the five processes of
tools/vectors/gen/pricingengines/barrier/fdblackscholesbarrierengine.py, on the default grid (100 × 100, Douglas);
then on the flat process: other grids and damping steps, three named scheme descriptions, the local volatility, cash
dividends, an expired option, whose results are zero, and an American exercise, which QuantLib refuses.

Every structure is on one day counter and the risk-free curve's reference date (the S-rx plan's RP15's inputs left
out, as the barrier engine's generator states). The pricing spec is that generator's, which this file imports. The
wheel's calls, by QuantLib-SWIG 1.44's options.i: FdBlackScholesRebateEngine's two constructors (with and without
dividends) on a BarrierOption; NPV, delta, gamma and theta the option's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/FdBlackScholesRebateEngineVectorTests.cs."""
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


fb = _load("fdblackscholesbarrierengine", "pricingengines/barrier/fdblackscholesbarrierengine.py")
ab, bc, bsm, fbs = fb.ab, fb.bc, fb.bsm, fb.fbs
R = fb.R


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec):
        out.append({"name": name, "inputs": {"settings": st, "pricing": spec},
                    "expected": fb.results(spec, ql.FdBlackScholesRebateEngine)})

    year = ab.exercise_spec("European", 365)
    for pname, p in fb.PROCESSES:
        x0 = p["x0"]
        for tname, t in ab.TYPES:
            level = x0 * (0.85 if t in (ab.DOWN_IN, ab.DOWN_OUT) else 1.15)
            for rebate in (0.03 * x0, 0.07 * x0):
                for option_type in (fb.CALL, fb.PUT):
                    payoff = bc.payoff_spec("PlainVanilla", option_type, x0)
                    add(f"{pname}, {tname} at {level:g}, rebate {rebate:g}, "
                        f"{'call' if option_type == fb.CALL else 'put'}",
                        fb.pricing_spec(p, ab.barrier_spec(t, level, rebate), payoff, year))
    p = fb.PROCESSES[0][1]
    put = bc.payoff_spec("PlainVanilla", fb.PUT, 100.0)
    down_out, up_in = ab.barrier_spec(ab.DOWN_OUT, 85.0, 3.0), ab.barrier_spec(ab.UP_IN, 115.0, 3.0)
    add("grid 50 × 200, damped", fb.pricing_spec(p, down_out, put, year, t_grid=50, x_grid=200, damping=2))
    add("knock-in, grid 200 × 51", fb.pricing_spec(p, up_in, put, year, t_grid=200, x_grid=51))
    for dname in ("CrankNicolson", "CraigSneyd", "TrBDF2"):
        add(f"down-and-out put, {dname}", fb.pricing_spec(p, down_out, put, year, t_grid=60, x_grid=60,
                                                          scheme=getattr(ql.FdmSchemeDesc, dname)()))
    surface = fb.PROCESSES[2][1]
    add("local volatility, down-and-out put", fb.pricing_spec(surface, down_out, put, year, local_vol=True))
    divs = [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)]
    add("dividends, down-and-out put", fb.pricing_spec(p, down_out, put, year, divs))
    add("dividends, up-and-in put", fb.pricing_spec(p, up_in, put, year, divs))
    add("expired", fb.pricing_spec(p, down_out, put, ab.exercise_spec("European", -1)))
    add("an American exercise", fb.pricing_spec(p, down_out, put, ab.exercise_spec("American", 365)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/fdblackscholesrebateengine.hpp", cases)
