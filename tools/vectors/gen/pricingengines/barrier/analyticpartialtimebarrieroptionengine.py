# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticPartialTimeBarrierOptionEngine
(ql/pricingengines/barrier/analyticpartialtimebarrieroptionengine.hpp, .cpp), on the evaluation date 15 October 2026:
partial-time barrier options by Heynen and Kat's formulas, the barrier monitored up to a cover event (the start window)
or from it to expiry (the end windows B1 and B2).

The knock-outs in each window and the start window's knock-ins, at two barriers on each side of the spot, the cover
event at half of six months, a year and two years, on the ten processes of tools/vectors/gen/processes/
blackscholesprocess.py, calls and puts at three strikes (90, 100 and 110% of the spot), the knock-ins calls only: the
NPV, the engine's one result. B2 with the strike beyond the barrier is QuantLib's refusal. Then an expired option and
the refusal of a cash-or-nothing payoff.

QuantLib's engine reads Drezner's bivariate normal, nanoql's West's (the S-bar plan's BR4 (e), the S-exo plan's ER7),
so every value carries §3.3's override: Drezner's accuracy, 6e-7 (EP39) times the sum of the bivariate terms'
coefficients, three pairs of S e^((b-r)T)(1 + (H/S)^(2mu+2)) and X e^(-rT)(1 + (H/S)^(2mu)) on the call the engine
prices (a put's put-call symmetric call, K/S times), relative to the value.

The outputs nanoql fixes are left out (the S-bar plan's §2.10): every process here has one day counter and reference
date (BR5 (i), tests/NanoQL.Tests/Divergences/BarrierClosedFormMismatchedTimesTests.cs); every rebate is 0, which
QuantLib's engine never reads (BR5 (xii), PartialTimeRebateTests.cs); no cover event is today (BR5 (xiii),
PartialTimeCoverTodayTests.cs) and no spot is beyond its barrier (BR5 (xiv), PartialTimeTouchedTests.cs); the
end-window knock-ins QuantLib refuses aren't written (BR8 (a), PartialTimeEndKnockInTests.cs); nor are the
start-window knock-in puts, whose European #2876 reads on the symmetric call (BarrierUpstreamFixesTests.cs); and a
value Drezner's algorithm returns as NaN is left out of its case (#2880, BR4 (e)): a case holds the payoffs left; and
every exercise is European, where QuantLib prices an American one to its last date and nanoql refuses it
(PartialTimeExerciseTests.cs).

A case holds one process, exercise, barrier, barrier range and cover event, its outputs one per payoff: the specs
tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's, the range QuantLib's PartialBarrier::Range (Start
0, EndB1 2, EndB2 3) and the cover event a serial number. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
PartialTimeBarrierOption(barrierType, barrierRange, barrier, rebate, coverEventDate, payoff, exercise),
AnalyticPartialTimeBarrierOptionEngine(process) and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Barrier/AnalyticPartialTimeBarrierOptionEngineVectorTests.cs."""
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
START, END_B1, END_B2 = int(ql.PartialBarrier.Start), int(ql.PartialBarrier.EndB1), int(ql.PartialBarrier.EndB2)
SETUPS = [("down-and-out, start", abe.DOWN_OUT, START), ("down-and-out, end B1", abe.DOWN_OUT, END_B1),
          ("down-and-out, end B2", abe.DOWN_OUT, END_B2), ("up-and-out, start", abe.UP_OUT, START),
          ("up-and-out, end B1", abe.UP_OUT, END_B1), ("up-and-out, end B2", abe.UP_OUT, END_B2),
          ("down-and-in, start", abe.DOWN_IN, START), ("up-and-in, start", abe.UP_IN, START)]
DRZ = 6e-7
REASON = "ER7: QuantLib's engine reads Dr78, nanoql's We04DP; 6e-7 times the bivariate terms' coefficients (EP39)"


def option(barrier, barrier_range, cover, payoff, ex):
    return ql.PartialTimeBarrierOption(barrier["barrierType"], barrier_range, barrier["barrier"], barrier["rebate"],
                                       ql.Date(cover), bc.payoff(payoff), abe.exercise(ex))


def coefficients(p, barrier, cover, payoff, last):
    """The sum of the bivariate terms' coefficients on the call the engine prices."""
    s = p.x0()
    k, h = payoff["strike"], barrier
    t2 = p.riskFreeRate().timeFromReference(last)
    t1 = p.riskFreeRate().timeFromReference(ql.Date(cover))
    r = p.riskFreeRate().zeroRate(t2, ql.Continuous, ql.NoFrequency).rate()
    q = p.dividendYield().zeroRate(t2, ql.Continuous, ql.NoFrequency).rate()
    scale = 1.0
    if payoff["optionType"] == PUT:
        k, h, r, q, scale = s * s / k, s * s / h, q, r, k / s
    vol = p.blackVolatility().blackVol(t1, k)
    b = r - q
    mu = (b - vol * vol / 2) / (vol * vol)
    hs = h / s
    return scale * 3 * (s * math.exp((b - r) * t2) * (1 + hs ** (2 * (mu + 1))) + k * math.exp(-r * t2) * (1 + hs ** (2 * mu)))


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, barrier_range, cover, specs, overridden=True):
        p = bsp.process(pspec)
        engine = ql.AnalyticPartialTimeBarrierOptionEngine(p)
        kept, values = [], []
        for spec in specs:
            o = option(barrier, barrier_range, cover, spec, ex)
            o.setPricingEngine(engine)
            value = v.attempt(o.NPV)
            if value == "NaN":
                continue
            if overridden and isinstance(value, float):
                tolerance = DRZ * coefficients(p, barrier["barrier"], cover, spec, ql.Date(ex["date"])) / max(abs(value), 1.0)
                value = {"value": value, "tolerance": tolerance, "reason": REASON}
            kept.append(spec)
            values.append(value)
        assert kept, name
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier,
                               "barrierRange": barrier_range, "coverEventDate": cover, "payoffs": kept},
                    "expected": {"NPV": values}})

    for pname, pspec in bsp.PROCESSES:
        x0 = pspec["x0"]
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            cover = (R + days // 2).serialNumber()
            for sname, t, barrier_range in SETUPS:
                knock_in = t in (abe.DOWN_IN, abe.UP_IN)
                for level in abe.barriers(x0, t):
                    specs = [p for p in abe.payoffs(x0) if not knock_in or p["optionType"] == CALL]
                    add(f"{pname}, {days} days, {sname} at {level:g}", pspec, ex, abe.barrier_spec(t, level, 0.0),
                        barrier_range, cover, specs)
    pspec = bsp.PROCESSES[0][1]
    cover = (R + 120).serialNumber()
    add("expired", pspec, abe.exercise_spec("European", -1), abe.barrier_spec(abe.DOWN_OUT, 85.0, 0.0), START,
        (R - 30).serialNumber(), abe.payoffs(100.0), overridden=False)
    add("a cash-or-nothing payoff", pspec, abe.exercise_spec("European", 365),
        abe.barrier_spec(abe.DOWN_OUT, 85.0, 0.0), START, cover, [bc.payoff_spec("CashOrNothing", CALL, 100.0)],
        overridden=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/analyticpartialtimebarrieroptionengine.hpp", cases)
