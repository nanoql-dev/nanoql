# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmAmericanStepCondition
(ql/methods/finitedifferences/stepconditions/fdmamericanstepcondition.hpp, .cpp), on the evaluation date 15 October
2026: applyTo(a, t), which raises each value to the calculator's inner value at its point, on a Black-Scholes, a
uniform and a two-dimensional mesher, with log and cell-averaging calculators of plain-vanilla puts and calls and a
cash-or-nothing put, each on two seeded arrays (one of zeros) at t = 0.5; and the refusal of an array of another size.

The specs are tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's calculators on
fdmmeshercomposite.py's composites. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmAmericanStepCondition(mesher,
calculator), the C++ constructor; applyTo the C++ member, which writes the wheel's Array in place. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/StepConditions/FdmAmericanStepConditionVectorTests.cs."""
import importlib.util
import math
import os
import random
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
mc, bsm, bsp, bc = fbs.mc, fbs.bsm, fbs.bsp, fbs.bc
PUT, CALL = int(ql.Option.Put), int(ql.Option.Call)

MESHERS = [
    ("Black-Scholes", mc.composite_spec(bsm.bs_spec(21, bsp.PROCESSES[0][1], 1.0, 100.0)), 0),
    ("uniform", mc.composite_spec({"type": "Uniform", "start": math.log(50.0), "end": math.log(200.0), "size": 11}),
     0),
    ("two dimensions", mc.composite_spec(mc.U4, bsm.bs_spec(9, bsp.PROCESSES[0][1], 1.0, 100.0)), 1),
]


def calculators(direction):
    return [("log put", fbs.calculator_spec("Log", bc.payoff_spec("PlainVanilla", PUT, 100.0), direction)),
            ("log call", fbs.calculator_spec("Log", bc.payoff_spec("PlainVanilla", CALL, 100.0), direction)),
            ("cell-averaged put", fbs.calculator_spec("CellAveraging", bc.payoff_spec("PlainVanilla", PUT, 100.0),
                                                      direction)),
            ("log cash-or-nothing put", fbs.calculator_spec("Log", bc.payoff_spec("CashOrNothing", PUT, 100.0),
                                                            direction))]


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def cases():
    st = v.settings(fbs.R)
    rng = random.Random(20261024)
    out = []
    for mname, mspec, direction in MESHERS:
        mesher, keep = mc.composite(mspec)
        n = mesher.layout().size()
        for cname, cspec in calculators(direction):
            calc = fbs.calculator(cspec, mesher)
            cond = ql.FdmAmericanStepCondition(mesher, calc)
            arrays = [[rng.uniform(0.0, 50.0) for _ in range(n)], [0.0] * n]
            applied = []
            for a in arrays:
                x = ql.Array(a)
                cond.applyTo(x, 0.5)
                applied.append(arr(x))
            short = ql.Array(arrays[0][:-1])
            out.append({"name": f"{mname}, {cname}", "inputs": {"settings": st, "mesher": mspec, "calculator": cspec,
                                                                "arrays": arrays, "t": 0.5},
                        "expected": {"applyTo": applied,
                                     "applyToShort": v.attempt(lambda: cond.applyTo(short, 0.5))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/stepconditions/fdmamericanstepcondition.hpp", cases)
