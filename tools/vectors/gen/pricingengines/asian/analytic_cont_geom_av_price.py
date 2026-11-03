# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticContinuousGeometricAveragePriceAsianEngine
(ql/pricingengines/asian/analytic_cont_geom_av_price.hpp, .cpp), on the evaluation date 15 October 2026: continuous
geometric average-price Asian options in closed form.

Geometric calls and puts at 90, 100 and 110, over six months, a year and two years, on the flat, constant-volatility
processes of tools/vectors/gen/instruments/asianoption.py: the NPV and the Greeks the engine sets. Then the refusals of
a seasoned option, an arithmetic average, a payoff that isn't plain vanilla and an exercise that isn't European, and an
expired option. Off flat markets the outputs are left out: nanoql's form is exact there.

A continuous option spec is {"averageType", "payoff", "exercise" and, where seasoned, "startDate"}. The wheel's calls,
by QuantLib-SWIG 1.44's options.i: ContinuousAveragingAsianOption(averageType, payoff, exercise) and (averageType,
startDate, payoff, exercise), AnalyticContinuousGeometricAveragePriceAsianEngine(process) and the option's NPV, delta,
gamma, theta, vega, rho and dividendRho. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Asian/AnalyticContinuousGeometricAveragePriceAsianEngineVectorTests.cs."""
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
GREEKS = ["NPV", "delta", "gamma", "theta", "vega", "rho", "dividendRho"]


def continuous_spec(average, payoff, ex, start=None):
    spec = {"averageType": average, "payoff": payoff, "exercise": ex}
    if start is not None:
        spec["startDate"] = start
    return spec


def continuous(spec):
    payoff, ex = bc.payoff(spec["payoff"]), abe.exercise(spec["exercise"])
    if "startDate" in spec:
        return ql.ContinuousAveragingAsianOption(spec["averageType"], ql.Date(spec["startDate"]), payoff, ex)
    return ql.ContinuousAveragingAsianOption(spec["averageType"], payoff, ex)


def outputs(opt, engine, names=GREEKS):
    opt.setPricingEngine(engine)
    return {n: v.attempt(getattr(opt, n)) for n in names}


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in ao.FLAT:
        engine = ql.AnalyticContinuousGeometricAveragePriceAsianEngine(bsp.process(pspec))
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            for tname, t in (("call", CALL), ("put", PUT)):
                for k in (90.0, 100.0, 110.0):
                    spec = continuous_spec(GEOMETRIC, bc.payoff_spec("PlainVanilla", t, k), ex)
                    out.append({"name": f"{pname}, {days} days, a {tname} at {k:g}",
                                "inputs": {"settings": st, "process": pspec, "option": spec},
                                "expected": outputs(continuous(spec), engine)})
    pspec = ao.MERTON
    engine = ql.AnalyticContinuousGeometricAveragePriceAsianEngine(bsp.process(pspec))
    pay = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    ex = abe.exercise_spec("European", 365)
    for name, spec in (("seasoned", continuous_spec(GEOMETRIC, pay, ex, (R - 30).serialNumber())),
                       ("an arithmetic average", continuous_spec(ARITHMETIC, pay, ex)),
                       ("a cash-or-nothing payoff", continuous_spec(GEOMETRIC, bc.payoff_spec("CashOrNothing", CALL, 100.0), ex)),
                       ("an American exercise", continuous_spec(GEOMETRIC, pay, abe.exercise_spec("American", 365))),
                       ("expired", continuous_spec(GEOMETRIC, pay, abe.exercise_spec("European", -1)))):
        out.append({"name": name, "inputs": {"settings": st, "process": pspec, "option": spec},
                    "expected": outputs(continuous(spec), engine, ["NPV"])})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/asian/analytic_cont_geom_av_price.hpp", cases)
