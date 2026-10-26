# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdHestonDoubleBarrierEngine (ql/pricingengines/barrier/fdhestondoublebarrierengine.hpp,
.cpp), on the evaluation date 15 October 2026: the NPV, delta, gamma and theta it reports for European double knock-outs
under Heston on a grid bounded at both barriers.

Two corridors around the spot (80-120% and 90-115% of it), with and without a rebate (3% of the spot), on
tools/vectors/gen/processes/hestonprocess.py's three parameter sets on flat curves, a call and a put at the spot and a
strike-0 cash-or-nothing call (cash 10) expiring in a year, on a 40 x 50 x 25 grid; then on the first set: QuantLib's
default grid, damping steps, the Douglas scheme, a constant leverage function, a mixing factor of 0.8 and an expired
option; and the refusal of the KIKO type.

The outputs nanoql fixes are left out (the S-bar plan's §2.10): every structure is on the risk-free curve's day counter
and reference date (BR5 (iv), tests/NanoQL.Tests/Divergences/FdHestonBarrierMismatchedTimesTests.cs); the double
knock-ins QuantLib refuses aren't written (BR8 (b), FdHestonDoubleKnockInTests.cs); no spot is on or beyond a barrier
(BR5 (xliv), tests/NanoQL.Tests/Conventions/FdHestonBarrierTouchedTests.cs); and the parameter sets are
tools/vectors/gen/pricingengines/vanilla/fdhestonvanillaengine.py's, clear of the S-hes plan's HR8 and HR9.

The specs are tools/vectors/gen/pricingengines/barrier/fdhestonbarrierengine.py's pricing, its barrier
analyticdoublebarrierengine.py's double barrier. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
HestonModel(HestonProcess(...)) and FdHestonDoubleBarrierEngine(model, tGrid, xGrid, vGrid, dampingSteps, schemeDesc,
leverageFct, mixingFactor), on a DoubleBarrierOption; NPV, delta, gamma and theta the option's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Barrier/FdHestonDoubleBarrierEngineVectorTests.cs."""
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
fhv, hp, bc, dbe, fbv = fhb.fhv, fhb.hp, fhb.bc, fhb.dbe, fhb.fbv
R = fhb.R
CALL, PUT = fhb.CALL, fhb.PUT


def payoffs(s0):
    return fhb.payoffs(s0) + [{"type": "CashOrNothing", "optionType": CALL, "strike": 0.0, "cashPayoff": 10.0}]


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec):
        out.append({"name": name, "inputs": {"settings": st, "pricing": spec},
                    "expected": fhb.results(spec, ql.FdHestonDoubleBarrierEngine, fhb.double_barrier_option)})

    for pname, *params in hp.PARAMETERS:
        process = fhv.heston_spec((pname, *params))
        s0 = process["s0"]
        for lo, hi in ((0.8, 1.2), (0.9, 1.15)):
            for rebate in (0.0, 0.03 * s0):
                for payoff in payoffs(s0):
                    kind = {(CALL, "PlainVanilla"): "call", (PUT, "PlainVanilla"): "put"}.get(
                        (payoff["optionType"], payoff["type"]), "cash-or-nothing")
                    add(f"{pname}, knock-out on [{lo:g}, {hi:g}] x spot, rebate {rebate:g}, {kind}",
                        fhb.pricing_spec(process, payoff, dbe.barrier_spec(dbe.KNOCK_OUT, s0 * lo, s0 * hi, rebate)))
    process = fhv.heston_spec(hp.PARAMETERS[0])
    call = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    corridor = dbe.barrier_spec(dbe.KNOCK_OUT, 80.0, 120.0, 3.0)
    add("QuantLib's default grid", fhb.pricing_spec(process, call, corridor, t_grid=100, x_grid=100, v_grid=50))
    add("damping steps", fhb.pricing_spec(process, call, corridor, damping=5))
    add("Douglas", fhb.pricing_spec(process, call, corridor, scheme=ql.FdmSchemeDesc.Douglas()))
    add("a leverage function", fhb.pricing_spec(process, call, corridor, leverage=fhv.CONSTANT_LEVERAGE))
    add("a mixing factor", fhb.pricing_spec(process, call, corridor, mixing=0.8))
    add("expired", fhb.pricing_spec(process, call, corridor, ex=fbv.exercise_spec("European", [-1])))
    add("a KIKO type", fhb.pricing_spec(process, call, dbe.barrier_spec(dbe.KIKO, 80.0, 120.0, 0.0)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/fdhestondoublebarrierengine.hpp", cases)
