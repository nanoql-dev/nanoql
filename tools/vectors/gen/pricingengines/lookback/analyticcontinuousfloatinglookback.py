# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticContinuousFloatingLookbackEngine
(ql/pricingengines/lookback/analyticcontinuousfloatinglookback.hpp, .cpp), on the evaluation date 15 October 2026:
continuous floating-strike lookback options by Goldman, Sosin and Gatto's formula.

Calls with the running minimum at the spot and at 90% of it and puts with the running maximum at the spot and at 110%
of it, over six months, a year and two years, on the ten processes of tools/vectors/gen/processes/
blackscholesprocess.py: the NPV, the engine's one result. Then an expired option and the refusal of a plain-vanilla
payoff ("Non-floating payoff given").

The outputs nanoql fixes are left out: every process here has one day counter and reference date
(tests/NanoQL.Tests/Divergences/LookbackMismatchedTimesTests.cs); no running extremum lies beyond the spot
(LookbackExtremeBeyondSpotTests.cs); no output's carry is within the small-carry cuts, u = |2b sqrt(T)/sigma| below
2.5e-3 with the carry b and volatility sigma read as the engine reads them at expiry, which leaves out the Black
processes' zero carry (LookbackSmallCarryTests.cs); and no volatility is zero (LookbackZeroVolatilityTests.cs).

A case holds one process, exercise and option type, its outputs one per running extremum; the specs
tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's. The wheel's calls, by QuantLib-SWIG 1.44's
options.i: ContinuousFloatingLookbackOption(minmax, payoff, exercise), AnalyticContinuousFloatingLookbackEngine(process)
and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Lookback/AnalyticContinuousFloatingLookbackEngineVectorTests.cs."""
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


abe = _load("analyticbarrierengine", "pricingengines/barrier/analyticbarrierengine.py")
bsp, bc = abe.bsp, abe.bc
R = abe.R
CALL, PUT = abe.CALL, abe.PUT

# the small-carry cuts' bound: the full engines' 1e-3 and the partial engines' 6e-4 to 2e-3, with a margin
U_CUT = 2.5e-3


def small_carry(p, last, level):
    """Whether u = |2b sqrt(T)/sigma| at expiry, as the engines read b and sigma, lies within the cuts."""
    t = p.blackVolatility().timeFromReference(last)
    b = (p.riskFreeRate().zeroRate(last, p.riskFreeRate().dayCounter(), ql.Continuous, ql.NoFrequency).rate()
         - p.dividendYield().zeroRate(last, p.dividendYield().dayCounter(), ql.Continuous, ql.NoFrequency).rate())
    sigma = p.blackVolatility().blackVol(last, level)
    return abs(2.0 * b * math.sqrt(t) / sigma) < U_CUT


def npv(option, engine):
    option.setPricingEngine(engine)
    return v.attempt(option.NPV)


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in bsp.PROCESSES:
        p = bsp.process(pspec)
        x0 = pspec["x0"]
        engine = ql.AnalyticContinuousFloatingLookbackEngine(p)
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            last = ql.Date(ex["date"])
            for tname, t, levels in (("call", CALL, (1.0, 0.9)), ("put", PUT, (1.0, 1.1))):
                kept = [x0 * f for f in levels if not small_carry(p, last, x0 * f)]
                if not kept:
                    continue
                values = [npv(ql.ContinuousFloatingLookbackOption(m, ql.FloatingTypePayoff(t), abe.exercise(ex)), engine)
                          for m in kept]
                out.append({"name": f"{pname}, {days} days, a {tname}",
                            "inputs": {"settings": st, "process": pspec, "exercise": ex, "optionType": t,
                                       "minmaxes": kept},
                            "expected": {"NPV": values}})
    pspec = bsp.PROCESSES[0][1]
    p = bsp.process(pspec)
    engine = ql.AnalyticContinuousFloatingLookbackEngine(p)
    for name, ex, payoff in (("expired", abe.exercise_spec("European", -1), None),
                             ("a plain-vanilla payoff", abe.exercise_spec("European", 365),
                              bc.payoff_spec("PlainVanilla", CALL, 100.0))):
        pay = ql.FloatingTypePayoff(CALL) if payoff is None else bc.payoff(payoff)
        inputs = {"settings": st, "process": pspec, "exercise": ex, "optionType": CALL, "minmaxes": [100.0]}
        if payoff is not None:
            inputs["payoff"] = payoff
        out.append({"name": name, "inputs": inputs,
                    "expected": {"NPV": [npv(ql.ContinuousFloatingLookbackOption(100.0, pay, abe.exercise(ex)), engine)]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/lookback/analyticcontinuousfloatinglookback.hpp", cases)
