# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdHestonBarrierEngine (ql/pricingengines/barrier/fdhestonbarrierengine.hpp, .cpp), on the
evaluation date 15 October 2026: the NPV, delta, gamma and theta it reports for European barrier options under Heston
on a grid bounded at the barrier, knock-ins by in-out parity on the FD Heston vanilla and rebate engines.

Each of the four barrier types at a barrier on each side of the spot (90% and 110% of it), with and without a rebate
(3% of the spot), on tools/vectors/gen/processes/hestonprocess.py's three parameter sets on flat curves, a call and a
put at the spot expiring in a year, on a 40 x 50 x 25 grid; then on the first set: QuantLib's default grid (100 x 100 x
50), damping steps, the Douglas and modified Craig-Sneyd schemes, cash dividends, a constant leverage function and a
mixing factor of 0.8 on the knock-outs, and an expired option.

The outputs nanoql fixes are left out: every structure is on the risk-free curve's day counter and reference date
(tests/NanoQL.Tests/Divergences/FdHestonBarrierMismatchedTimesTests.cs); no knock-in has a leverage function or a mixing
factor, whose vanilla and rebate legs QuantLib prices on pure Heston (FdHestonKnockInLeverageTests.cs); no spot is
beyond its barrier, where QuantLib's interpolation fails and nanoql refuses "barrier touched"
(tests/NanoQL.Tests/Conventions/FdHestonBarrierTouchedTests.cs); and the parameter sets are
tools/vectors/gen/pricingengines/vanilla/fdhestonvanillaengine.py's, clear of the regions nanoql's Heston fixes reach.

The specs are a pricing {"process", "payoff", "exercise", "barrier", "dividends", "tGrid", "xGrid", "vGrid",
"dampingSteps", "scheme", "leverage", "mixingFactor"}: fdhestonvanillaengine.py's, the exercise
tools/vectors/gen/pricingengines/vanilla/fdblackscholesvanillaengine.py's, the barrier
tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's, or for the double-barrier engine
analyticdoublebarrierengine.py's; the FD Heston double-barrier and rebate generators share them through this file. The
wheel's calls, by QuantLib-SWIG 1.44's options.i: HestonModel(HestonProcess(...)) and FdHestonBarrierEngine's two
constructors (with and without dividends), on a BarrierOption; NPV, delta, gamma and theta the option's accessors.
Read back by tests/NanoQL.Tests/Vectors/PricingEngines/Barrier/FdHestonBarrierEngineVectorTests.cs."""
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


fhv = _load("fdhestonvanillaengine", "pricingengines/vanilla/fdhestonvanillaengine.py")
abe = _load("analyticbarrierengine", "pricingengines/barrier/analyticbarrierengine.py")
dbe = _load("analyticdoublebarrierengine", "pricingengines/barrier/analyticdoublebarrierengine.py")
hp, bsm, fbs, bc, fbv = fhv.hp, fhv.bsm, fhv.fbs, fhv.bc, fhv.fbv
R = fhv.R
CALL, PUT = fhv.CALL, fhv.PUT
RESULTS = fhv.RESULTS
DIVIDENDS = [{"type": "Fixed", "amount": 2.0, "date": (R + 120).serialNumber()},
             {"type": "Fixed", "amount": 2.0, "date": (R + 300).serialNumber()}]


def pricing_spec(process, payoff, barrier, ex=None, dividends=(), t_grid=40, x_grid=50, v_grid=25, damping=0,
                 scheme=None, leverage=None, mixing=1.0):
    spec = fhv.pricing_spec(process, payoff, ex or fbv.exercise_spec("European", [365]), dividends, None, t_grid, x_grid,
                            v_grid, damping, scheme, leverage, mixing)
    del spec["quanto"]
    spec["barrier"] = barrier
    return spec


def results(spec, engine_class, option):
    """The engine's results for a spec, each null where QuantLib doesn't provide it, or the refusal."""
    model = ql.HestonModel(hp.process(spec["process"]))
    head = [model]
    if spec["dividends"]:
        head.append(bsm.dividends(spec["dividends"]))
    tail = [spec["tGrid"], spec["xGrid"], spec["vGrid"], spec["dampingSteps"], fbs.scheme(spec["scheme"])]
    lev = fhv.leverage(spec["leverage"])
    if lev is not None or spec["mixingFactor"] != 1.0:
        tail += [lev, spec["mixingFactor"]]
    o = option(spec)
    o.setPricingEngine(engine_class(*head, *tail))
    try:
        o.NPV()
    except RuntimeError:
        return {r: dict(v.THROWS) for r in RESULTS}
    return {r: v.result(lambda r=r: getattr(o, r)()) for r in RESULTS}


def barrier_option(spec):
    b = spec["barrier"]
    return ql.BarrierOption(b["barrierType"], b["barrier"], b["rebate"], bc.payoff(spec["payoff"]),
                            fbv.exercise(spec["exercise"]))


def double_barrier_option(spec):
    b = spec["barrier"]
    return ql.DoubleBarrierOption(b["barrierType"], b["barrierLo"], b["barrierHi"], b["rebate"],
                                  bc.payoff(spec["payoff"]), fbv.exercise(spec["exercise"]))


def payoffs(s0):
    return [bc.payoff_spec("PlainVanilla", t, s0) for t in (CALL, PUT)]


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec):
        out.append({"name": name, "inputs": {"settings": st, "pricing": spec},
                    "expected": results(spec, ql.FdHestonBarrierEngine, barrier_option)})

    for pname, *params in hp.PARAMETERS:
        process = fhv.heston_spec((pname, *params))
        s0 = process["s0"]
        for tname, t in abe.TYPES:
            level = s0 * (0.9 if t in (abe.DOWN_IN, abe.DOWN_OUT) else 1.1)
            for rebate in (0.0, 0.03 * s0):
                for payoff in payoffs(s0):
                    kind = "call" if payoff["optionType"] == CALL else "put"
                    add(f"{pname}, {tname} at {level:g}, rebate {rebate:g}, {kind}",
                        pricing_spec(process, payoff, abe.barrier_spec(t, level, rebate)))
    process = fhv.heston_spec(hp.PARAMETERS[0])
    call = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    down_out = abe.barrier_spec(abe.DOWN_OUT, 90.0, 3.0)
    down_in = abe.barrier_spec(abe.DOWN_IN, 90.0, 3.0)
    for bname, barrier in (("down-and-out", down_out), ("down-and-in", down_in)):
        add(f"{bname}, QuantLib's default grid", pricing_spec(process, call, barrier, t_grid=100, x_grid=100, v_grid=50))
        add(f"{bname}, damping steps", pricing_spec(process, call, barrier, damping=5))
        add(f"{bname}, Douglas", pricing_spec(process, call, barrier, scheme=ql.FdmSchemeDesc.Douglas()))
        add(f"{bname}, modified Craig-Sneyd", pricing_spec(process, call, barrier,
                                                          scheme=ql.FdmSchemeDesc.ModifiedCraigSneyd()))
        add(f"{bname}, cash dividends", pricing_spec(process, call, barrier, dividends=DIVIDENDS))
    for tname, t in (("down-and-out", abe.DOWN_OUT), ("up-and-out", abe.UP_OUT)):
        level = 90.0 if t == abe.DOWN_OUT else 110.0
        add(f"{tname}, a leverage function", pricing_spec(process, call, abe.barrier_spec(t, level, 3.0),
                                                          leverage=fhv.CONSTANT_LEVERAGE))
        add(f"{tname}, a mixing factor", pricing_spec(process, call, abe.barrier_spec(t, level, 3.0), mixing=0.8))
    add("expired", pricing_spec(process, call, down_out, ex=fbv.exercise_spec("European", [-1])))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/fdhestonbarrierengine.hpp", cases)
