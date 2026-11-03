# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ChoiAsianEngine (ql/pricingengines/asian/choiasianengine.hpp, .cpp), on the evaluation date
15 October 2026: discrete arithmetic average-price Asian options by Choi's basket integration.

Arithmetic calls and puts at 90, 100 and 110, over six months, a year and two years, with 4 and 12 fixings, unseasoned
on the ten processes of tools/vectors/gen/processes/blackscholesprocess.py and with two past fixings at 95 and 105 on
the processes without a smile, at lambda 10 and 2 << 12 integration steps (QuantLib's own cases' settings): the NPV, the
engine's one result. Then a single fixing, the known payoff with no fixing to come, the refusals of a geometric average,
a payoff that isn't plain vanilla, an exercise that isn't European and a fixing after the exercise, and an expired
option. The outputs nanoql fixes are left out: seasoned options on a smile and an effective strike below zero (QuantLib
refuses).

Lambda and the steps go in as "lambda" and "maxNrIntegrationSteps". The wheel's calls, by QuantLib-SWIG 1.44's
options.i: ChoiAsianEngine(process, lambda, maxNrIntegrationSteps) and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Asian/ChoiAsianEngineVectorTests.cs."""
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
LAMBDA, STEPS = 10.0, 2 << 12


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in bsp.PROCESSES:
        engine = ql.ChoiAsianEngine(bsp.process(pspec), LAMBDA, STEPS)
        x0 = pspec["x0"]
        seasonings = [("unseasoned", None, 0)]
        if "surface" not in pname:
            seasonings.append(("two past fixings at 95 and 105", 2.0 * x0, 2))
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            for n in (4, 12):
                for sname, running, past in seasonings:
                    for tname, t in (("call", CALL), ("put", PUT)):
                        for k in (0.9, 1.0, 1.1):
                            spec = ao.option_spec(ARITHMETIC, ao.dates(days, n), bc.payoff_spec("PlainVanilla", t, k * x0),
                                                  ex, running=running, past=past)
                            out.append({"name": f"{pname}, {days} days, {n} fixings, {sname}, a {tname} at {k:g} x spot",
                                        "inputs": {"settings": st, "process": pspec, "option": spec, "lambda": LAMBDA,
                                                   "maxNrIntegrationSteps": STEPS},
                                        "expected": {"NPV": ao.npv(ao.option(spec), engine)}})
    pspec = ao.MERTON
    engine = ql.ChoiAsianEngine(bsp.process(pspec), LAMBDA, STEPS)
    pay = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    ex = abe.exercise_spec("European", 365)
    for name, spec in (("a single fixing", ao.option_spec(ARITHMETIC, ao.dates(365, 1), pay, ex)),
                       ("no fixing to come", ao.option_spec(ARITHMETIC, [], pay, ex, running=210.0, past=2)),
                       ("a geometric average", ao.option_spec(GEOMETRIC, ao.dates(365, 12), pay, ex)),
                       ("a cash-or-nothing payoff",
                        ao.option_spec(ARITHMETIC, ao.dates(365, 12), bc.payoff_spec("CashOrNothing", CALL, 100.0), ex)),
                       ("an American exercise",
                        ao.option_spec(ARITHMETIC, ao.dates(365, 12), pay, abe.exercise_spec("American", 365))),
                       ("a fixing after the exercise", ao.option_spec(ARITHMETIC, ao.dates(400, 12), pay, ex)),
                       ("expired", ao.option_spec(ARITHMETIC, ao.dates(-1, 1), pay, abe.exercise_spec("European", -1)))):
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "option": spec, "lambda": LAMBDA,
                               "maxNrIntegrationSteps": STEPS},
                    "expected": {"NPV": ao.npv(ao.option(spec), engine)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/asian/choiasianengine.hpp", cases)
