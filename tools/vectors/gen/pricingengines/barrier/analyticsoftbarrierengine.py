# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticSoftBarrierEngine (ql/pricingengines/barrier/analyticsoftbarrierengine.hpp, .cpp), on
the evaluation date 15 October 2026: soft-barrier options, knocked in or out in proportion to the depth the spot
reaches into a band [L, U], by Haug's closed form.

Down-and-in and down-and-out calls under two bands below the spot (85-90% and 75-85% of it) and up-and-in and
up-and-out puts under two above it (110-115% and 115-125%), over six months, a year and two years, on the processes of
tools/vectors/gen/processes/blackscholesprocess.py whose carry is clear of Haug's singular points, at three strikes
(90, 100 and 110% of the spot): the NPV, the engine's one result. Then the degenerate branches, a knock-in already in
(the vanilla) and a knock-out already out (0), an expired option and the refusals: a cash-or-nothing payoff and
barriers out of order. Then the option's implied volatility at 105% of its value at the first process's 22%, for
down-and-in and down-and-out calls at 100 and 110 on [85, 90], at accuracies 1e-4 and 1e-10, its bracket [5%, 400%],
and its refusal on an expired option. QuantLib's default bracket starts at 1e-6, where Haug's form is NaN (BR5 (xvii)),
so QuantLib's solve refuses "root not bracketed" for every one of these options, where the port's band average solves:
left out; a case holding "impliedVolatility" {"target", "accuracy", "maxEvaluations", "minVol", "maxVol"} and
one payoff.

The outputs nanoql fixes are left out (the S-bar plan's §2.10): every process here has one day counter and reference
date (BR5 (i), tests/NanoQL.Tests/Divergences/BarrierClosedFormMismatchedTimesTests.cs); every case is in Haug's
domain, a down band at or below a call's strike or an up band at or above a put's, the spot outside it (BR5 (xlv),
SoftBarrierDomainTests.cs, and the directions #2883 refuses, BarrierUpstreamFixesTests.cs); its carry b and b + sigma^2
are at least 4e-3 from 0, twice the port's switch (BR5 (x), SoftBarrierSingularCarryTests.cs), so the Black processes,
of zero carry, aren't here; every band is at least 5% wide, above (U - L)/U of 3e-3 and U - L of 1e-4 (BR5 (xi),
SoftBarrierCoincidentBarriersTests.cs); every volatility is 10% or more, where Haug's form is finite (BR5 (xvii),
SoftBarrierLowVolatilityTests.cs); every rate is inside QuantLib's limits (BR8 (d), SoftBarrierRateLimitsTests.cs);
and every exercise is European, where QuantLib prices any other to its last date and nanoql refuses it
(SoftBarrierExerciseTests.cs).

A case holds one process, exercise and soft barrier {"barrierType", "barrierLo", "barrierHi"}, its outputs one per
payoff, the specs otherwise tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's. The wheel's calls, by
QuantLib-SWIG 1.44's options.i: SoftBarrierOption(barrierType, barrierLo, barrierHi, payoff, exercise),
AnalyticSoftBarrierEngine(process), the option's NPV and impliedVolatility(price, process, accuracy, maxEvaluations,
minVol, maxVol). Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Barrier/AnalyticSoftBarrierEngineVectorTests.cs."""
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
CARRY_MARGIN = 4e-3
SETUPS = [("down-and-in call", abe.DOWN_IN, CALL, [(0.85, 0.9), (0.75, 0.85)]),
          ("down-and-out call", abe.DOWN_OUT, CALL, [(0.85, 0.9), (0.75, 0.85)]),
          ("up-and-in put", abe.UP_IN, PUT, [(1.1, 1.15), (1.15, 1.25)]),
          ("up-and-out put", abe.UP_OUT, PUT, [(1.1, 1.15), (1.15, 1.25)])]


def barrier_spec(barrier_type, lo, hi):
    return {"barrierType": barrier_type, "barrierLo": lo, "barrierHi": hi}


def option(barrier, payoff, ex):
    return ql.SoftBarrierOption(barrier["barrierType"], barrier["barrierLo"], barrier["barrierHi"], bc.payoff(payoff),
                                abe.exercise(ex))


def clear_carry(p, last, strikes):
    """Whether b and b + sigma^2 are clear of 0 at every strike's volatility."""
    t = p.riskFreeRate().timeFromReference(last)
    b = (p.riskFreeRate().zeroRate(t, ql.Continuous, ql.NoFrequency).rate()
         - p.dividendYield().zeroRate(t, ql.Continuous, ql.NoFrequency).rate())
    for k in strikes:
        sigma = p.blackVolatility().blackVol(t, k)
        if abs(b) < CARRY_MARGIN or abs(b + sigma * sigma) < CARRY_MARGIN:
            return False
    return True


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, specs):
        engine = ql.AnalyticSoftBarrierEngine(bsp.process(pspec))
        values = []
        for spec in specs:
            o = option(barrier, spec, ex)
            o.setPricingEngine(engine)
            values.append(v.attempt(o.NPV))
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier, "payoffs": specs},
                    "expected": {"NPV": values}})

    for pname, pspec in bsp.PROCESSES:
        x0 = pspec["x0"]
        p = bsp.process(pspec)
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            strikes = [x0 * f for f in (0.9, 1.0, 1.1)]
            if not clear_carry(p, R + days, strikes):
                continue
            for sname, t, option_type, bands in SETUPS:
                for flo, fhi in bands:
                    specs = [bc.payoff_spec("PlainVanilla", option_type, k) for k in strikes]
                    add(f"{pname}, {days} days, {sname} on [{flo:g}, {fhi:g}] x spot", pspec, ex,
                        barrier_spec(t, x0 * flo, x0 * fhi), specs)
    pspec = bsp.PROCESSES[0][1]
    ex = abe.exercise_spec("European", 365)
    calls = [bc.payoff_spec("PlainVanilla", CALL, k) for k in (90.0, 100.0, 110.0)]
    puts = [bc.payoff_spec("PlainVanilla", PUT, k) for k in (90.0, 100.0, 110.0)]
    add("down-and-in, already in", pspec, ex, barrier_spec(abe.DOWN_IN, 100.0, 105.0), calls)
    add("down-and-out, already out", pspec, ex, barrier_spec(abe.DOWN_OUT, 100.0, 105.0), calls)
    add("up-and-in, already in", pspec, ex, barrier_spec(abe.UP_IN, 95.0, 100.0), puts)
    add("up-and-out, already out", pspec, ex, barrier_spec(abe.UP_OUT, 95.0, 100.0), puts)
    add("expired", pspec, abe.exercise_spec("European", -1), barrier_spec(abe.DOWN_IN, 85.0, 90.0), calls)
    for tname, t in (("down-and-in", abe.DOWN_IN), ("down-and-out", abe.DOWN_OUT)):
        for k in (100.0, 110.0):
            for accuracy in (1e-4, 1e-10):
                barrier = barrier_spec(t, 85.0, 90.0)
                payoff = bc.payoff_spec("PlainVanilla", CALL, k)
                o = option(barrier, payoff, ex)
                o.setPricingEngine(ql.AnalyticSoftBarrierEngine(bsp.process(pspec)))
                iv = {"target": o.NPV() * 1.05, "accuracy": accuracy, "maxEvaluations": 100, "minVol": 0.05,
                      "maxVol": 4.0}
                out.append({"name": f"implied volatility, {tname} call at {k:g}, accuracy {accuracy:g}",
                            "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier,
                                       "payoff": payoff, "impliedVolatility": iv},
                            "expected": {"impliedVolatility": v.attempt(lambda: o.impliedVolatility(
                                iv["target"], bsp.process(pspec), accuracy, 100, 0.05, 4.0))}})
    expired = option(barrier_spec(abe.DOWN_IN, 85.0, 90.0), calls[1], abe.exercise_spec("European", -1))
    iv = {"target": 1.0, "accuracy": 1e-4, "maxEvaluations": 100, "minVol": 1e-6, "maxVol": 4.0}
    out.append({"name": "implied volatility, expired", "inputs": {
        "settings": st, "process": pspec, "exercise": abe.exercise_spec("European", -1),
        "barrier": barrier_spec(abe.DOWN_IN, 85.0, 90.0), "payoff": calls[1], "impliedVolatility": iv},
        "expected": {"impliedVolatility": v.attempt(lambda: expired.impliedVolatility(1.0, bsp.process(pspec)))}})
    add("a cash-or-nothing payoff", pspec, ex, barrier_spec(abe.DOWN_IN, 85.0, 90.0),
        [bc.payoff_spec("CashOrNothing", CALL, 100.0)])
    add("barriers out of order", pspec, ex, barrier_spec(abe.DOWN_IN, 90.0, 85.0), calls[:1])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/analyticsoftbarrierengine.hpp", cases)
