# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdHestonRebateEngine (ql/pricingengines/barrier/fdhestonrebateengine.hpp, .cpp), on the
evaluation date 15 October 2026: the NPV, delta, gamma and theta it reports for a barrier option's rebate under Heston,
the rebate paid at the hit or at expiry, on a grid bounded at the barrier.

Each of the four barrier types at a barrier on each side of the spot (90% and 110% of it), with a rebate of 3% of the
spot, on tools/vectors/gen/processes/hestonprocess.py's three parameter sets on flat curves, a call at the spot expiring
in a year, on a 40 x 50 x 25 grid; then on the first set: QuantLib's default grid, damping steps, the Douglas scheme,
cash dividends, a constant leverage function, a mixing factor of 0.8 and an expired option.

The outputs nanoql fixes are left out (the S-bar plan's §2.10): every structure is on the risk-free curve's day counter
and reference date (BR5 (iv), tests/NanoQL.Tests/Divergences/FdHestonBarrierMismatchedTimesTests.cs); no spot is
beyond its barrier (BR5 (xliv), tests/NanoQL.Tests/Conventions/FdHestonBarrierTouchedTests.cs); and the parameter sets
are tools/vectors/gen/pricingengines/vanilla/fdhestonvanillaengine.py's, clear of the S-hes plan's HR8 and HR9.

The specs are tools/vectors/gen/pricingengines/barrier/fdhestonbarrierengine.py's. The wheel's calls, by QuantLib-SWIG
1.44's options.i: HestonModel(HestonProcess(...)) and FdHestonRebateEngine's two constructors (with and without
dividends), on a BarrierOption; NPV, delta, gamma and theta the option's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Barrier/FdHestonRebateEngineVectorTests.cs."""
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


fhb = _load("fdhestonbarrierengine", "pricingengines/barrier/fdhestonbarrierengine.py")
fhv, hp, bc, abe, fbv = fhb.fhv, fhb.hp, fhb.bc, fhb.abe, fhb.fbv
R = fhb.R
CALL = fhb.CALL


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec):
        out.append({"name": name, "inputs": {"settings": st, "pricing": spec},
                    "expected": fhb.results(spec, ql.FdHestonRebateEngine, fhb.barrier_option)})

    for pname, *params in hp.PARAMETERS:
        process = fhv.heston_spec((pname, *params))
        s0 = process["s0"]
        for tname, t in abe.TYPES:
            level = s0 * (0.9 if t in (abe.DOWN_IN, abe.DOWN_OUT) else 1.1)
            add(f"{pname}, {tname} at {level:g}",
                fhb.pricing_spec(process, bc.payoff_spec("PlainVanilla", CALL, s0), abe.barrier_spec(t, level, 0.03 * s0)))
    process = fhv.heston_spec(hp.PARAMETERS[0])
    call = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    barrier = abe.barrier_spec(abe.DOWN_IN, 90.0, 3.0)
    add("QuantLib's default grid", fhb.pricing_spec(process, call, barrier, t_grid=100, x_grid=100, v_grid=50))
    add("damping steps", fhb.pricing_spec(process, call, barrier, damping=5))
    add("Douglas", fhb.pricing_spec(process, call, barrier, scheme=ql.FdmSchemeDesc.Douglas()))
    add("cash dividends", fhb.pricing_spec(process, call, barrier, dividends=fhb.DIVIDENDS))
    add("a leverage function", fhb.pricing_spec(process, call, barrier, leverage=fhv.CONSTANT_LEVERAGE))
    add("a mixing factor", fhb.pricing_spec(process, call, barrier, mixing=0.8))
    add("expired", fhb.pricing_spec(process, call, barrier, ex=fbv.exercise_spec("European", [-1])))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/fdhestonrebateengine.hpp", cases)
