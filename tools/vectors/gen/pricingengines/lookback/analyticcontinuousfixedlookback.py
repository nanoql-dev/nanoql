# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticContinuousFixedLookbackEngine
(ql/pricingengines/lookback/analyticcontinuousfixedlookback.hpp, .cpp), on the evaluation date 15 October 2026:
continuous fixed-strike lookback options by Conze and Viswanathan's formula.

Calls with the running maximum at the spot and at 105% of it and puts with the running minimum at the spot and at 95%
of it, each at three strikes (90, 100 and 110% of the spot), over six months, a year and two years, on the ten
processes of tools/vectors/gen/processes/blackscholesprocess.py: the NPV, the engine's one result. Then an expired
option and the refusal of a cash-or-nothing payoff ("Non-plain payoff given").

The outputs nanoql fixes are left out, as
tools/vectors/gen/pricingengines/lookback/analyticcontinuousfloatinglookback.py leaves them out: one clock per process,
no running extremum beyond the spot, no output's carry within the small-carry cuts and no zero volatility.

A case holds one process, exercise and running extremum, its outputs one per payoff. The wheel's calls, by
QuantLib-SWIG 1.44's options.i: ContinuousFixedLookbackOption(minmax, payoff, exercise),
AnalyticContinuousFixedLookbackEngine(process) and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Lookback/AnalyticContinuousFixedLookbackEngineVectorTests.cs."""
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


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in bsp.PROCESSES:
        p = bsp.process(pspec)
        x0 = pspec["x0"]
        engine = ql.AnalyticContinuousFixedLookbackEngine(p)
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            last = ql.Date(ex["date"])
            for tname, t, levels in (("call", CALL, (1.0, 1.05)), ("put", PUT, (1.0, 0.95))):
                for f in levels:
                    minmax = x0 * f
                    specs = [bc.payoff_spec("PlainVanilla", t, x0 * k) for k in (0.9, 1.0, 1.1)]
                    kept = [s for s in specs if not lb.small_carry(p, last, s["strike"])]
                    if not kept:
                        continue
                    values = [lb.npv(ql.ContinuousFixedLookbackOption(minmax, bc.payoff(s), abe.exercise(ex)), engine)
                              for s in kept]
                    out.append({"name": f"{pname}, {days} days, {tname}s, the running extremum at {f:g} x spot",
                                "inputs": {"settings": st, "process": pspec, "exercise": ex, "minmax": minmax,
                                           "payoffs": kept},
                                "expected": {"NPV": values}})
    pspec = bsp.PROCESSES[0][1]
    engine = ql.AnalyticContinuousFixedLookbackEngine(bsp.process(pspec))
    for name, ex, spec in (("expired", abe.exercise_spec("European", -1), bc.payoff_spec("PlainVanilla", CALL, 100.0)),
                           ("a cash-or-nothing payoff", abe.exercise_spec("European", 365),
                            bc.payoff_spec("CashOrNothing", CALL, 100.0))):
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "minmax": 100.0, "payoffs": [spec]},
                    "expected": {"NPV": [lb.npv(ql.ContinuousFixedLookbackOption(100.0, bc.payoff(spec), abe.exercise(ex)),
                                                engine)]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/lookback/analyticcontinuousfixedlookback.hpp", cases)
