# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's inner-value calculators (ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp,
.cpp): FdmLogInnerValue (the payoff at the exponential of the log-spot location), FdmCellAveragingInnerValue with its
identity mapping (the payoff at the location) and FdmZeroInnerValue, their innerValue and avgInnerValue (the payoff
averaged over each point's cell, by QuantLib's Simpson integration where the payoff isn't smooth inside it) at every
layout point at t = 0.5, for plain-vanilla calls and puts, cash-or-nothing, asset-or-nothing and gap payoffs, on a
Black-Scholes mesher, a uniform mesher in the spot and a two-dimensional mesher with the underlying in the second
direction; and FdmLogBasketInnerValue (a basket payoff at the exponentials of every direction's location, its average
the same) for minimum, maximum, average and spread payoffs on two Black-Scholes meshers and on three uniform log
meshers, at t = 0.5.

The specs are tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's calculators on
fdmmeshercomposite.py's composites. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: the three C++ constructors
(FdmCellAveragingInnerValue's without a mapping), FdmLogBasketInnerValue(payoff, mesher) on
tools/vectors/gen/instruments/basketoption.py's payoffs, and innerValue and avgInnerValue, the C++ members, at the
layout's iterator. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/FdmInnerValueCalculatorVectorTests.cs."""
import importlib.util
import math
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


fbs = _load("fdmbackwardsolver", "methods/finitedifferences/solvers/fdmbackwardsolver.py")
bo = _load("basketoption", "instruments/basketoption.py")
mc, bsm, bsp, bc = fbs.mc, fbs.bsm, fbs.bsp, fbs.bc
FLAT = bsp.PROCESSES[0][1]
LOG_MESHERS = [("Black-Scholes", mc.composite_spec(bsm.bs_spec(21, FLAT, 1.0, 100.0)), 0),
               ("uniform log spot", mc.composite_spec({"type": "Uniform", "start": math.log(60.0),
                                                        "end": math.log(160.0), "size": 12}), 0),
               ("two dimensions", mc.composite_spec(mc.U4, bsm.bs_spec(9, FLAT, 1.0, 100.0)), 1)]
SPOT_MESHERS = [("uniform spot", mc.composite_spec({"type": "Uniform", "start": 50.0, "end": 150.0, "size": 11}), 0),
                ("concentrating spot", mc.composite_spec({"type": "Concentrating", "start": 40.0, "end": 160.0,
                                                          "size": 15, "cPoint": [100.0, 0.1],
                                                          "requireCPoint": True}), 0)]
PAYOFFS = [("call", bc.payoff_spec("PlainVanilla", int(ql.Option.Call), 100.0)),
           ("put", bc.payoff_spec("PlainVanilla", int(ql.Option.Put), 95.0)),
           ("cash-or-nothing call", bc.payoff_spec("CashOrNothing", int(ql.Option.Call), 105.0)),
           ("asset-or-nothing put", bc.payoff_spec("AssetOrNothing", int(ql.Option.Put), 100.0)),
           ("gap call", bc.payoff_spec("Gap", int(ql.Option.Call), 100.0))]


def values(calc, mesher, t):
    layout = mesher.layout()
    inner, avg = [], []
    it = layout.begin()
    for _ in range(layout.size()):
        inner.append(calc.innerValue(it, t))
        avg.append(calc.avgInnerValue(it, t))
        it.increment()
    return v.enc(inner), v.enc(avg)


def cases():
    st = v.settings(fbs.R)
    out = []
    for kind, meshers in (("Log", LOG_MESHERS), ("CellAveraging", SPOT_MESHERS)):
        for mname, mspec, direction in meshers:
            mesher, keep = mc.composite(mspec)
            for pname, payoff in PAYOFFS:
                cspec = fbs.calculator_spec(kind, payoff, direction)
                inner, avg = values(fbs.calculator(cspec, mesher), mesher, 0.5)
                out.append({"name": f"{kind}, {mname}, {pname}",
                            "inputs": {"settings": st, "mesher": mspec, "calculator": cspec, "t": 0.5},
                            "expected": {"innerValue": inner, "avgInnerValue": avg}})
    mspec = LOG_MESHERS[0][1]
    mesher, keep = mc.composite(mspec)
    cspec = fbs.calculator_spec("Zero", PAYOFFS[0][1])
    inner, avg = values(fbs.calculator(cspec, mesher), mesher, 0.5)
    out.append({"name": "Zero", "inputs": {"settings": st, "mesher": mspec, "calculator": cspec, "t": 0.5},
                "expected": {"innerValue": inner, "avgInnerValue": avg}})
    two = mc.composite_spec(bsm.bs_spec(11, FLAT, 1.0, 100.0), bsm.bs_spec(9, bsp.PROCESSES[1][1], 1.0, 100.0))
    three = mc.composite_spec(*[{"type": "Uniform", "start": math.log(a), "end": math.log(b), "size": n}
                                for a, b, n in ((60.0, 150.0, 5), (70.0, 140.0, 4), (50.0, 160.0, 6))])
    call, put = PAYOFFS[0][1], PAYOFFS[1][1]
    baskets = [("two dimensions, minimum call", two, bo.payoff_spec("Min", call)),
               ("two dimensions, spread put", two, bo.payoff_spec("Spread", put)),
               ("two dimensions, average call", two, bo.payoff_spec("Average", call, weights=[0.7, 0.3])),
               ("three dimensions, maximum put", three, bo.payoff_spec("Max", put)),
               ("three dimensions, equal average call", three, bo.payoff_spec("Average", call, n=3))]
    for name, mspec, payoff in baskets:
        mesher, keep = mc.composite(mspec)
        cspec = {"type": "LogBasket", "payoff": payoff}
        p = bo.payoff(payoff)
        inner, avg = values(ql.FdmLogBasketInnerValue(p, mesher), mesher, 0.5)
        out.append({"name": f"LogBasket, {name}", "inputs": {"settings": st, "mesher": mspec, "calculator": cspec,
                                                             "t": 0.5},
                    "expected": {"innerValue": inner, "avgInnerValue": avg}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp", cases)
