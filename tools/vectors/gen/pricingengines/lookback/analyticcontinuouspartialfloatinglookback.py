# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticContinuousPartialFloatingLookbackEngine
(ql/pricingengines/lookback/analyticcontinuouspartialfloatinglookback.hpp, .cpp), on the evaluation date 15 October
2026: continuous partial-time floating-strike lookback options by Heynen and Kat's formula.

Calls with the running minimum at the spot and at 90% of it, at lambda 1 and 1.2, and puts with the running maximum at
the spot and at 110% of it, at lambda 1 and 0.8, the lookback period ending at a quarter and at half of six months, a
year and two years, and at expiry (the formula's simpler branch), on the ten processes of
tools/vectors/gen/processes/blackscholesprocess.py: the NPV, the engine's one result. Then an expired option.

The outputs nanoql fixes are left out, as
tools/vectors/gen/pricingengines/lookback/analyticcontinuousfloatinglookback.py leaves them out: one clock per process,
no running extremum beyond the spot, no output's carry within the small-carry cuts and no zero volatility; and no option
nanoql's validation refuses where QuantLib's reads an exercise or payoff unchecked.

A case holds one process, exercise, option type, lambda and lookback end, its outputs one per running extremum. The
wheel's calls, by QuantLib-SWIG 1.44's options.i: ContinuousPartialFloatingLookbackOption(minmax, lambda,
lookbackPeriodEnd, payoff, exercise), AnalyticContinuousPartialFloatingLookbackEngine(process) and the option's NPV.
Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Lookback/AnalyticContinuousPartialFloatingLookbackEngineVectorTests.cs."""
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


lb = _load("analyticcontinuousfloatinglookback", "pricingengines/lookback/analyticcontinuousfloatinglookback.py")
abe, bsp = lb.abe, lb.bsp
R, CALL, PUT = lb.R, lb.CALL, lb.PUT


def option(minmax, lam, end, t, ex):
    return ql.ContinuousPartialFloatingLookbackOption(minmax, lam, ql.Date(end), ql.FloatingTypePayoff(t), abe.exercise(ex))


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in bsp.PROCESSES:
        p = bsp.process(pspec)
        x0 = pspec["x0"]
        engine = ql.AnalyticContinuousPartialFloatingLookbackEngine(p)
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            last = ql.Date(ex["date"])
            for end_days in (days // 4, days // 2, days):
                end = (R + end_days).serialNumber()
                for tname, t, levels, lambdas in (("call", CALL, (1.0, 0.9), (1.0, 1.2)),
                                                  ("put", PUT, (1.0, 1.1), (1.0, 0.8))):
                    for lam in lambdas:
                        kept = [x0 * f for f in levels if not lb.small_carry(p, last, x0 * f)]
                        if not kept:
                            continue
                        values = [lb.npv(option(m, lam, end, t, ex), engine) for m in kept]
                        out.append({"name": f"{pname}, {days} days, the lookback to day {end_days}, a {tname}, "
                                            f"lambda {lam:g}",
                                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "optionType": t,
                                               "lambda": lam, "lookbackPeriodEnd": end, "minmaxes": kept},
                                    "expected": {"NPV": values}})
    pspec = bsp.PROCESSES[0][1]
    engine = ql.AnalyticContinuousPartialFloatingLookbackEngine(bsp.process(pspec))
    ex = abe.exercise_spec("European", -1)
    end = (R - 30).serialNumber()
    out.append({"name": "expired",
                "inputs": {"settings": st, "process": pspec, "exercise": ex, "optionType": CALL, "lambda": 1.0,
                           "lookbackPeriodEnd": end, "minmaxes": [100.0]},
                "expected": {"NPV": [lb.npv(option(100.0, 1.0, end, CALL, ex), engine)]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/lookback/analyticcontinuouspartialfloatinglookback.hpp", cases)
