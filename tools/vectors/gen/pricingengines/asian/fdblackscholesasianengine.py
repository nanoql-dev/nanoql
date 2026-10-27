# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdBlackScholesAsianEngine (ql/pricingengines/asian/fdblackscholesasianengine.hpp, .cpp), on
the evaluation date 15 October 2026: discrete arithmetic average-price Asian options by finite differences on the log
spot and the log average.

Arithmetic calls and puts at 90, 100 and 110, over six months and a year, with 4 and 12 fixings, unseasoned and with
two past fixings at 95 and 105, on the ten processes of tools/vectors/gen/processes/blackscholesprocess.py, on a grid
of 50 time steps, 50 spot points and 25 average points: the NPV, delta and gamma. Then the refusals of a geometric
average, an exercise that isn't European, a running sum without a past fixing and a fixing in the past, and an expired
option. Fixing dates given twice are left out (the S-bar plan's §2.10: BR5 (xxviii)).

The grid goes in as "tGrid", "xGrid" and "aGrid". The wheel's calls, by QuantLib-SWIG 1.44's options.i:
FdBlackScholesAsianEngine(process, tGrid, xGrid, aGrid) and the option's NPV, delta and gamma. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Asian/FdBlackScholesAsianEngineVectorTests.cs."""
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


ao = _load("asianoption", "instruments/asianoption.py")
abe, bsp, bc = ao.abe, ao.bsp, ao.bc
R, CALL, PUT, ARITHMETIC, GEOMETRIC = ao.R, ao.CALL, ao.PUT, ao.ARITHMETIC, ao.GEOMETRIC
T_GRID, X_GRID, A_GRID = 50, 50, 25


def outputs(opt, engine, names=("NPV", "delta", "gamma")):
    opt.setPricingEngine(engine)
    return {n: v.attempt(getattr(opt, n)) for n in names}


def inputs(st, pspec, spec):
    return {"settings": st, "process": pspec, "option": spec, "tGrid": T_GRID, "xGrid": X_GRID, "aGrid": A_GRID}


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in bsp.PROCESSES:
        engine = ql.FdBlackScholesAsianEngine(bsp.process(pspec), T_GRID, X_GRID, A_GRID)
        x0 = pspec["x0"]
        for days in (182, 365):
            ex = abe.exercise_spec("European", days)
            for n in (4, 12):
                for sname, running, past in (("unseasoned", None, 0), ("two past fixings at 95 and 105", 2.0 * x0, 2)):
                    for tname, t in (("call", CALL), ("put", PUT)):
                        for k in (0.9, 1.0, 1.1):
                            spec = ao.option_spec(ARITHMETIC, ao.dates(days, n), bc.payoff_spec("PlainVanilla", t, k * x0),
                                                  ex, running=running, past=past)
                            out.append({"name": f"{pname}, {days} days, {n} fixings, {sname}, a {tname} at {k:g} x spot",
                                        "inputs": inputs(st, pspec, spec), "expected": outputs(ao.option(spec), engine)})
    pspec = ao.MERTON
    engine = ql.FdBlackScholesAsianEngine(bsp.process(pspec), T_GRID, X_GRID, A_GRID)
    pay = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    ex = abe.exercise_spec("European", 365)
    for name, spec in (("a geometric average", ao.option_spec(GEOMETRIC, ao.dates(365, 12), pay, ex)),
                       ("an American exercise",
                        ao.option_spec(ARITHMETIC, ao.dates(365, 12), pay, abe.exercise_spec("American", 365))),
                       ("a running sum without a past fixing",
                        ao.option_spec(ARITHMETIC, ao.dates(365, 12), pay, ex, running=100.0, past=0)),
                       ("a fixing in the past",
                        ao.option_spec(ARITHMETIC, [(R - 10).serialNumber()] + ao.dates(365, 12), pay, ex)),
                       ("expired", ao.option_spec(ARITHMETIC, ao.dates(-1, 1), pay, abe.exercise_spec("European", -1)))):
        out.append({"name": name, "inputs": inputs(st, pspec, spec), "expected": outputs(ao.option(spec), engine, ("NPV",))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/asian/fdblackscholesasianengine.hpp", cases)
