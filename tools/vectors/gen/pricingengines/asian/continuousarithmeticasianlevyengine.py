# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ContinuousArithmeticAsianLevyEngine
(ql/pricingengines/asian/continuousarithmeticasianlevyengine.hpp, .cpp), on the evaluation date 15 October 2026:
continuous arithmetic average-price Asian options by Levy's moment match.

Arithmetic calls and puts at 90, 100 and 110, over six months, a year and two years, starting today, and seasoned:
started 30 and 90 days ago at running averages of 95 and 105, on the flat Merton process of
tools/vectors/gen/instruments/asianoption.py: the NPV, the engine's one result. Then the refusals of a geometric
average, a payoff that isn't plain vanilla, an exercise that isn't European and a start after the evaluation date, and
an expired option. The outputs nanoql fixes are left out: markets off flat, carries within 1e-2 of 0, −σ²/2 and −σ² (the
flat Black process's zero carry among them), and effective strikes at or below zero.

The current average goes in as "currentAverage", the option as tools/vectors/gen/pricingengines/asian/
analytic_cont_geom_av_price.py's spec. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
ContinuousArithmeticAsianLevyEngine(process, currentAverage) and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Asian/ContinuousArithmeticAsianLevyEngineVectorTests.cs."""
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
cg = _load("analytic_cont_geom_av_price", "pricingengines/asian/analytic_cont_geom_av_price.py")
abe, bsp, bc = ao.abe, ao.bsp, ao.bc
R, CALL, PUT, ARITHMETIC, GEOMETRIC = ao.R, ao.CALL, ao.PUT, ao.ARITHMETIC, ao.GEOMETRIC


def engine(p, average):
    return ql.ContinuousArithmeticAsianLevyEngine(p, ql.QuoteHandle(ql.SimpleQuote(average)))


def cases():
    st = v.settings(R)
    out = []
    pspec = ao.MERTON
    p = bsp.process(pspec)
    for days in abe.EXPIRIES:
        ex = abe.exercise_spec("European", days)
        for sname, elapsed, average in (("starting today", 0, 100.0), ("30 days in at 95", 30, 95.0),
                                        ("30 days in at 105", 30, 105.0), ("90 days in at 95", 90, 95.0),
                                        ("90 days in at 105", 90, 105.0)):
            for tname, t in (("call", CALL), ("put", PUT)):
                for k in (90.0, 100.0, 110.0):
                    spec = cg.continuous_spec(ARITHMETIC, bc.payoff_spec("PlainVanilla", t, k), ex,
                                              (R - elapsed).serialNumber())
                    out.append({"name": f"{days} days, {sname}, a {tname} at {k:g}",
                                "inputs": {"settings": st, "process": pspec, "option": spec, "currentAverage": average},
                                "expected": {"NPV": ao.npv(cg.continuous(spec), engine(p, average))}})
    pay = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    ex = abe.exercise_spec("European", 365)
    start = R.serialNumber()
    for name, spec in (("a geometric average", cg.continuous_spec(GEOMETRIC, pay, ex, start)),
                       ("a cash-or-nothing payoff",
                        cg.continuous_spec(ARITHMETIC, bc.payoff_spec("CashOrNothing", CALL, 100.0), ex, start)),
                       ("an American exercise", cg.continuous_spec(ARITHMETIC, pay, abe.exercise_spec("American", 365), start)),
                       ("a start after today", cg.continuous_spec(ARITHMETIC, pay, ex, (R + 10).serialNumber())),
                       ("expired", cg.continuous_spec(ARITHMETIC, pay, abe.exercise_spec("European", -1), (R - 30).serialNumber()))):
        out.append({"name": name, "inputs": {"settings": st, "process": pspec, "option": spec, "currentAverage": 100.0},
                    "expected": {"NPV": ao.npv(cg.continuous(spec), engine(p, 100.0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/asian/continuousarithmeticasianlevyengine.hpp", cases)
