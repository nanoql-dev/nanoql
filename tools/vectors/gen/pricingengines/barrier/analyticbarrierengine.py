# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticBarrierEngine (ql/pricingengines/barrier/analyticbarrierengine.hpp, .cpp), on the
evaluation date 15 October 2026: European barrier options priced by Haug's closed forms.

Each of the four barrier types at two barriers on each side of the spot, with and without a rebate (3% of the spot),
over six months, a year and two years, on the ten processes of tools/vectors/gen/processes/blackscholesprocess.py
(flat and zero curves; constant, variance-curve and surface volatilities; the Merton, generalized, Black-Scholes,
Black and Garman-Kohlhagen processes), each a call and a put at three strikes (90, 100 and 110% of the spot): the NPV,
the engine's one result. An expired option's NPV is QuantLib's, 0. Then the refusals: an American exercise, a
cash-or-nothing payoff and a spot beyond the barrier ("barrier touched").

The outputs nanoql fixes are left out: every process here has positive rates and volatilities and one day counter and
reference date, so none is at zero volatility, on structures dated or day-counted apart or a knock-out whose rebate has
no real λ, and no rebate is negative. Their divergence facts are
tests/NanoQL.Tests/Divergences/BarrierZeroVolatilityTests.cs, BarrierNegativeRebateTests.cs,
BarrierMismatchedTimesTests.cs and BarrierNegativeRateRebateTests.cs.

A case holds one process, exercise, barrier type, barrier and rebate, its outputs one per payoff. The process specs
are tools/vectors/gen/processes/blackscholesprocess.py's; a payoff spec is
tools/vectors/gen/pricingengines/blackcalculator.py's; an exercise is {"type": "European" | "American", "date"}; a
barrier is {"barrierType" (QuantLib's Barrier::Type, DownIn 0, UpIn 1, DownOut 2, UpOut 3), "barrier", "rebate"}. The
specs and the option are shared by the other barrier generators, which import this file. The wheel's calls, by
QuantLib-SWIG 1.44's options.i: BarrierOption(barrierType, barrier, rebate, payoff, exercise), AnalyticBarrierEngine
(process) and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticBarrierEngineVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
DOWN_IN, UP_IN, DOWN_OUT, UP_OUT = (int(ql.Barrier.DownIn), int(ql.Barrier.UpIn), int(ql.Barrier.DownOut),
                                    int(ql.Barrier.UpOut))
TYPES = [("down-and-in", DOWN_IN), ("up-and-in", UP_IN), ("down-and-out", DOWN_OUT), ("up-and-out", UP_OUT)]
EXPIRIES = [182, 365, 730]


def exercise_spec(kind, days):
    return {"type": kind, "date": (R + days).serialNumber()}


def exercise(spec):
    date = ql.Date(spec["date"])
    if spec["type"] == "European":
        return ql.EuropeanExercise(date)
    return ql.AmericanExercise(R, date)


def barrier_spec(barrier_type, barrier, rebate):
    return {"barrierType": barrier_type, "barrier": barrier, "rebate": rebate}


def option(barrier, payoff, ex):
    return ql.BarrierOption(barrier["barrierType"], barrier["barrier"], barrier["rebate"], bc.payoff(payoff),
                            exercise(ex))


def barriers(x0, barrier_type):
    """The type's two barriers, 85 and 95% of the spot below it, 105 and 115% above."""
    down = barrier_type in (DOWN_IN, DOWN_OUT)
    return [x0 * f for f in ((0.85, 0.95) if down else (1.05, 1.15))]


def payoffs(x0):
    return [bc.payoff_spec("PlainVanilla", t, x0 * f) for t in (CALL, PUT) for f in (0.9, 1.0, 1.1)]


def npv(barrier, payoff, ex, engine):
    o = option(barrier, payoff, ex)
    o.setPricingEngine(engine)
    return v.attempt(o.NPV)


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, specs):
        engine = ql.AnalyticBarrierEngine(bsp.process(pspec))
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier, "payoffs": specs},
                    "expected": {"NPV": [npv(barrier, p, ex, engine) for p in specs]}})

    for pname, pspec in bsp.PROCESSES:
        x0 = pspec["x0"]
        for days in EXPIRIES:
            ex = exercise_spec("European", days)
            for tname, t in TYPES:
                for level in barriers(x0, t):
                    for rebate in (0.0, 0.03 * x0):
                        add(f"{pname}, {days} days, {tname} at {level:g}, rebate {rebate:g}", pspec, ex,
                            barrier_spec(t, level, rebate), payoffs(x0))
    pspec = bsp.PROCESSES[0][1]
    add("expired", pspec, exercise_spec("European", -1), barrier_spec(DOWN_OUT, 85.0, 3.0), payoffs(100.0))
    add("an American exercise", pspec, exercise_spec("American", 365), barrier_spec(DOWN_OUT, 85.0, 3.0),
        payoffs(100.0))
    add("a cash-or-nothing payoff", pspec, exercise_spec("European", 365), barrier_spec(DOWN_OUT, 85.0, 3.0),
        [bc.payoff_spec("CashOrNothing", CALL, 100.0)])
    for tname, t in TYPES:
        level = 105.0 if t in (DOWN_IN, DOWN_OUT) else 95.0
        add(f"{tname}, the spot beyond the barrier", pspec, exercise_spec("European", 365),
            barrier_spec(t, level, 3.0), payoffs(100.0))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/analyticbarrierengine.hpp", cases)
