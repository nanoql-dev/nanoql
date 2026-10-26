# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticContinuousPartialFixedLookbackEngine
(ql/pricingengines/lookback/analyticcontinuouspartialfixedlookback.hpp, .cpp), on the evaluation date 15 October 2026:
continuous partial-time fixed-strike lookback options by Heynen and Kat's formula.

Calls and puts at three strikes (90, 100 and 110% of the spot), the lookback period starting at a quarter and at half
of six months, a year and two years, and at expiry, on the ten processes of tools/vectors/gen/processes/
blackscholesprocess.py: the NPV, the engine's one result. Then an expired option and the refusal of a cash-or-nothing
payoff ("Non-plain payoff given").

The outputs nanoql fixes are left out (the S-bar plan's §2.10), as
tools/vectors/gen/pricingengines/lookback/analyticcontinuousfloatinglookback.py leaves them out: one clock per
process (BR5 (xix)), no output's carry within the small-carry cuts (BR5 (xxiii)) and no zero volatility (BR5 (xxiv));
and no option nanoql's validation refuses where QuantLib's reads an exercise unchecked (BR5 (xxv)).

A case holds one process, exercise and lookback start, its outputs one per payoff. The wheel's calls, by QuantLib-SWIG
1.44's options.i: ContinuousPartialFixedLookbackOption(lookbackPeriodStart, payoff, exercise),
AnalyticContinuousPartialFixedLookbackEngine(process) and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Lookback/AnalyticContinuousPartialFixedLookbackEngineVectorTests.cs."""
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
abe, bsp, bc = lb.abe, lb.bsp, lb.bc
R, CALL, PUT = lb.R, lb.CALL, lb.PUT


def option(start, spec, ex):
    return ql.ContinuousPartialFixedLookbackOption(ql.Date(start), bc.payoff(spec), abe.exercise(ex))


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in bsp.PROCESSES:
        p = bsp.process(pspec)
        x0 = pspec["x0"]
        engine = ql.AnalyticContinuousPartialFixedLookbackEngine(p)
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            last = ql.Date(ex["date"])
            for start_days in (days // 4, days // 2, days):
                start = (R + start_days).serialNumber()
                kept = [s for s in abe.payoffs(x0) if not lb.small_carry(p, last, s["strike"])]
                if not kept:
                    continue
                values = [lb.npv(option(start, s, ex), engine) for s in kept]
                out.append({"name": f"{pname}, {days} days, the lookback from day {start_days}",
                            "inputs": {"settings": st, "process": pspec, "exercise": ex, "lookbackPeriodStart": start,
                                       "payoffs": kept},
                            "expected": {"NPV": values}})
    pspec = bsp.PROCESSES[0][1]
    engine = ql.AnalyticContinuousPartialFixedLookbackEngine(bsp.process(pspec))
    for name, ex, start, spec in (("expired", abe.exercise_spec("European", -1), (R - 30).serialNumber(),
                                   bc.payoff_spec("PlainVanilla", CALL, 100.0)),
                                  ("a cash-or-nothing payoff", abe.exercise_spec("European", 365),
                                   (R + 180).serialNumber(), bc.payoff_spec("CashOrNothing", CALL, 100.0))):
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "lookbackPeriodStart": start,
                               "payoffs": [spec]},
                    "expected": {"NPV": [lb.npv(option(start, spec, ex), engine)]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/lookback/analyticcontinuouspartialfixedlookback.hpp", cases)
