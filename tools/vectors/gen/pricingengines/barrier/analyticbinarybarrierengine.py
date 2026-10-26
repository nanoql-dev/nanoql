# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticBinaryBarrierEngine (ql/pricingengines/barrier/analyticbinarybarrierengine.hpp,
.cpp), on the evaluation date 15 October 2026: binary barrier options, cash-or-nothing and asset-or-nothing payoffs at
expiry knocked in or out at a barrier, by Haug's formulas.

Each of the four barrier types at two barriers on each side of the spot, over six months, a year and two years, on the
ten processes of tools/vectors/gen/processes/blackscholesprocess.py, each a cash-or-nothing (cash 10) and an
asset-or-nothing call and put at three strikes (90, 100 and 110% of the spot): the NPV, the engine's one result off its
degenerate branches. Then the degenerate branches, where QuantLib also sets the Greeks: a knock-out with the spot beyond
its barrier (0) and a knock-in with it (the European digital, by QuantLib's analytic European engine): the NPV, delta,
gamma, vega, theta, rho and dividend rho. Then a plain-vanilla payoff, which QuantLib's formulas price at 0, an expired
option and the refusals: a European exercise, an American one paying at the hit, and a negative barrier.

The outputs nanoql fixes are left out (the S-bar plan's §2.10): every process here has positive volatilities over
expiries of six months or more and one day counter and reference date, so none is below machine-epsilon variance
(BR5 (xv), tests/NanoQL.Tests/Divergences/BinaryBarrierZeroVolatilityTests.cs) or on structures dated or day-counted
apart (BR5 (i), BarrierClosedFormMismatchedTimesTests.cs); and every rebate is 0, which QuantLib's engine never reads
(BinaryBarrierRebateTests.cs).

A case holds one process, exercise and barrier, its outputs one per payoff, the specs
tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's but for the exercise, which adds "payoffAtExpiry"
and, for the expired option, "earliestDate" (QuantLib's AmericanExercise(earliest, latest, payoffAtExpiry), the
earliest date otherwise the evaluation date). The wheel's
calls, by QuantLib-SWIG 1.44's options.i: BarrierOption(barrierType, barrier, rebate, payoff, exercise),
AnalyticBinaryBarrierEngine(process) and the option's NPV and Greeks. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticBinaryBarrierEngineVectorTests.cs."""
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


abe = _load("analyticbarrierengine", "pricingengines/barrier/analyticbarrierengine.py")
bsp, bc = abe.bsp, abe.bc
R = abe.R
CALL, PUT = abe.CALL, abe.PUT
GREEKS = ["delta", "gamma", "vega", "theta", "rho", "dividendRho"]


def exercise_spec(kind, days, payoff_at_expiry=True, earliest=None):
    spec = abe.exercise_spec(kind, days)
    if kind == "American":
        spec["payoffAtExpiry"] = payoff_at_expiry
        if earliest is not None:
            spec["earliestDate"] = (R + earliest).serialNumber()
    return spec


def exercise(spec):
    date = ql.Date(spec["date"])
    if spec["type"] == "European":
        return ql.EuropeanExercise(date)
    earliest = ql.Date(spec["earliestDate"]) if "earliestDate" in spec else R
    return ql.AmericanExercise(earliest, date, spec["payoffAtExpiry"])


def option(barrier, payoff, ex):
    return ql.BarrierOption(barrier["barrierType"], barrier["barrier"], barrier["rebate"], bc.payoff(payoff),
                            exercise(ex))


def payoffs(x0):
    return [bc.payoff_spec(kind, t, x0 * f) for kind in ("CashOrNothing", "AssetOrNothing") for t in (CALL, PUT)
            for f in (0.9, 1.0, 1.1)]


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, specs, greeks=False):
        engine = ql.AnalyticBinaryBarrierEngine(bsp.process(pspec))
        options = []
        for p in specs:
            o = option(barrier, p, ex)
            o.setPricingEngine(engine)
            options.append(o)
        expected = {"NPV": [v.attempt(o.NPV) for o in options]}
        if greeks:
            for g in GREEKS:
                expected[g] = [v.result(getattr(o, g)) for o in options]
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier, "payoffs": specs},
                    "expected": expected})

    for pname, pspec in bsp.PROCESSES:
        x0 = pspec["x0"]
        for days in abe.EXPIRIES:
            ex = exercise_spec("American", days)
            for tname, t in abe.TYPES:
                for level in abe.barriers(x0, t):
                    add(f"{pname}, {days} days, {tname} at {level:g}", pspec, ex, abe.barrier_spec(t, level, 0.0),
                        payoffs(x0))
    pspec = bsp.PROCESSES[0][1]
    ex = exercise_spec("American", 365)
    for tname, t in abe.TYPES:
        level = 105.0 if t in (abe.DOWN_IN, abe.DOWN_OUT) else 95.0
        add(f"{tname}, the spot beyond the barrier", pspec, ex, abe.barrier_spec(t, level, 0.0), payoffs(100.0),
            greeks=True)
    add("a plain-vanilla payoff", pspec, ex, abe.barrier_spec(abe.DOWN_OUT, 85.0, 0.0),
        [bc.payoff_spec("PlainVanilla", t, 100.0) for t in (CALL, PUT)])
    add("expired", pspec, exercise_spec("American", -1, earliest=-30), abe.barrier_spec(abe.DOWN_OUT, 85.0, 0.0), payoffs(100.0))
    add("a European exercise", pspec, exercise_spec("European", 365), abe.barrier_spec(abe.DOWN_OUT, 85.0, 0.0),
        payoffs(100.0)[:1])
    add("an American exercise paying at the hit", pspec, exercise_spec("American", 365, False),
        abe.barrier_spec(abe.DOWN_OUT, 85.0, 0.0), payoffs(100.0)[:1])
    add("a negative barrier", pspec, ex, abe.barrier_spec(abe.DOWN_IN, -5.0, 0.0), payoffs(100.0)[:1])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/analyticbinarybarrierengine.hpp", cases)
