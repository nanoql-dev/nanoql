# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EscrowedDividendAdjustment and FdmEscrowedLogInnerValueCalculator
(ql/methods/finitedifferences/utilities/escroweddividendadjustment.hpp, .cpp, fdmescrowedloginnervaluecalculator.hpp,
.cpp), on the evaluation date 15 October 2026. The wheel binds neither, so they are read through
FdBlackScholesVanillaEngine's escrowed cash-dividend model, which adjusts the spot by the dividends' present value, adds
the dividends' dates as stopping times for an early exercise, and exercises on the adjusted underlying: the NPV, delta,
gamma and theta.

European, American and Bermudan calls and puts on a flat Merton process and on zero curves with a variance surface,
under schedules of two dividends, dividends outside the exercise (before the evaluation date and after the last
exercise date), a dividend on the exercise date and a large one; and the refusals of an adjusted spot that isn't
positive ("spot minus dividends becomes negative") and of a quanto helper with the escrowed model ("Escrowed dividend
model is not supported for Quanto-Options").

The specs are tools/vectors/gen/pricingengines/vanilla/fdblackscholesvanillaengine.py's pricings, with
cashDividendModel 1. The wheel's calls are that file's, with FdBlackScholesVanillaEngine.Escrowed. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/EscrowedDividendAdjustmentVectorTests.cs."""
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


eng = _load("fdblackscholesvanillaengine", "pricingengines/vanilla/fdblackscholesvanillaengine.py")
bsm, bsp, bc = eng.bsm, eng.bsp, eng.bc
CALL, PUT = eng.CALL, eng.PUT


def cases():
    st = v.settings(eng.R)
    out = []

    def add(name, spec):
        out.append({"name": name, "inputs": {"settings": st, "pricing": spec}, "expected": eng.results(spec)})

    schedules = [("two dividends", [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)]),
                 ("dividends outside", [bsm.fixed(1.0, -10), bsm.fixed(2.0, 120), bsm.fixed(4.0, 500)]),
                 ("a dividend on the exercise date", [bsm.fixed(2.0, 365)]),
                 ("a large dividend", [bsm.fixed(30.0, 150)])]
    exercises = [("European", eng.exercise_spec("European", [365])),
                 ("American", eng.exercise_spec("American", [365])),
                 ("Bermudan", eng.exercise_spec("Bermudan", [91, 182, 273, 365]))]
    for pname, p in (("flat", bsp.PROCESSES[0][1]), ("surface", bsp.PROCESSES[3][1])):
        for sname, divs in schedules:
            for ename, ex in exercises:
                for option_type in (CALL, PUT):
                    payoff = bc.payoff_spec("PlainVanilla", option_type, 100.0)
                    add(f"{pname}, {sname}, {ename} {'call' if option_type == CALL else 'put'}",
                        eng.pricing_spec(p, payoff, ex, divs, model=1))
    p = bsp.PROCESSES[0][1]
    put = bc.payoff_spec("PlainVanilla", PUT, 100.0)
    add("spot minus dividends negative", eng.pricing_spec(p, put, exercises[0][1], [bsm.fixed(120.0, 91)], model=1))
    add("quanto", eng.pricing_spec(p, put, exercises[0][1], [bsm.fixed(2.0, 91)], quanto=bsm.FLAT_QUANTO, model=1))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/escroweddividendadjustment.hpp", cases)
