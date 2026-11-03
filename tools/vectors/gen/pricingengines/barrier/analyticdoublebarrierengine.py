# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticDoubleBarrierEngine (ql/pricingengines/barrier/analyticdoublebarrierengine.hpp,
.cpp), on the evaluation date 15 October 2026: European double knock-in and knock-out options priced by Ikeda and
Kunitomo's image series.

Three corridors around the spot (80-120%, 90-115% and 70-140% of it), over six months, a year and two years, on the
ten processes of tools/vectors/gen/processes/blackscholesprocess.py, each knock-in and knock-out a call and a put at
three strikes (90, 100 and 110% of the spot): the NPV, the engine's one result, at QuantLib's default five terms. Then
an expired option and the refusals: an American exercise, a cash-or-nothing payoff, a spot on a barrier and outside
the corridor ("barrier(s) already touched"), and the KIKO and KOKI types.

The outputs nanoql fixes are left out: every process here has positive volatilities and one day counter and reference
date, so none is on structures dated or day-counted apart
(tests/NanoQL.Tests/Divergences/BarrierClosedFormMismatchedTimesTests.cs) or at a vanishing variance
(DoubleBarrierLowVolatilityTests.cs); every rebate is 0, which QuantLib's engine never reads
(DoubleBarrierRebateTests.cs); every strike is inside the corridor, a put's above L and a call's below U, where #2881's
bounds are QuantLib 1.44's (it prices 0 a put at or below L and a call at or above U, where 1.44 leaves a rounding of up
to 4e-15; BarrierUpstreamFixesTests.cs); and an output whose knock-out at five terms isn't within 5e-13 of its value at
50 terms, on max(|value|, 1), is left out of its case: the port keeps QuantLib's five terms only where they agree with
the converged series within 1e-12 on that scale and sums on elsewhere (DoubleBarrierSeriesTests.cs), the knock-in being
the vanilla less the knock-out, and the margin keeps a case clear of that switch. A case holds the payoffs left, and
none is empty.

A case holds one process, exercise and double barrier, its outputs one per payoff: a double barrier is
{"barrierType" (QuantLib's DoubleBarrier::Type, KnockIn 0, KnockOut 1, KIKO 2, KOKI 3), "barrierLo", "barrierHi",
"rebate"}; the process, payoff and exercise specs are
tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's, which the other double-barrier generators share
through this file. The wheel's calls, by QuantLib-SWIG 1.44's options.i: DoubleBarrierOption(barrierType, barrierLo,
barrierHi, rebate, payoff, exercise), AnalyticDoubleBarrierEngine(process, series) and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Barrier/AnalyticDoubleBarrierEngineVectorTests.cs."""
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
KNOCK_IN, KNOCK_OUT, KIKO, KOKI = (int(ql.DoubleBarrier.KnockIn), int(ql.DoubleBarrier.KnockOut),
                                   int(ql.DoubleBarrier.KIKO), int(ql.DoubleBarrier.KOKI))
TYPES = [("knock-in", KNOCK_IN), ("knock-out", KNOCK_OUT)]
CORRIDORS = [(0.8, 1.2), (0.9, 1.15), (0.7, 1.4)]
AGREED = 5e-13


def barrier_spec(barrier_type, lo, hi, rebate=0.0):
    return {"barrierType": barrier_type, "barrierLo": lo, "barrierHi": hi, "rebate": rebate}


def option(barrier, payoff, ex):
    return ql.DoubleBarrierOption(barrier["barrierType"], barrier["barrierLo"], barrier["barrierHi"],
                                  barrier["rebate"], bc.payoff(payoff), abe.exercise(ex))


def npv(barrier, payoff, ex, engine):
    o = option(barrier, payoff, ex)
    o.setPricingEngine(engine)
    return v.attempt(o.NPV)


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, specs, keep=None):
        engine = ql.AnalyticDoubleBarrierEngine(bsp.process(pspec))
        values = [npv(barrier, p, ex, engine) for p in specs]
        if keep is not None:
            specs, values = [p for p, k in zip(specs, keep) if k], [x for x, k in zip(values, keep) if k]
        assert specs, name
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier, "series": 5,
                               "payoffs": specs},
                    "expected": {"NPV": values}})

    for pname, pspec in bsp.PROCESSES:
        x0 = pspec["x0"]
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            for lo, hi in CORRIDORS:
                for tname, t in TYPES:
                    barrier = barrier_spec(t, x0 * lo, x0 * hi)
                    knock_out = barrier_spec(KNOCK_OUT, x0 * lo, x0 * hi)
                    specs = [p for p in abe.payoffs(x0)
                             if (p["strike"] > x0 * lo if p["optionType"] == PUT else p["strike"] < x0 * hi)]
                    process = bsp.process(pspec)
                    five = [npv(knock_out, p, ex, ql.AnalyticDoubleBarrierEngine(process, 5)) for p in specs]
                    fifty = [npv(knock_out, p, ex, ql.AnalyticDoubleBarrierEngine(process, 50)) for p in specs]
                    keep = [abs(a - b) <= AGREED * max(abs(b), 1.0) for a, b in zip(five, fifty)]
                    add(f"{pname}, {days} days, {tname} on [{lo:g}, {hi:g}] x spot", pspec, ex, barrier, specs, keep)
    pspec = bsp.PROCESSES[0][1]
    ex = abe.exercise_spec("European", 365)
    add("expired", pspec, abe.exercise_spec("European", -1), barrier_spec(KNOCK_OUT, 80.0, 120.0), abe.payoffs(100.0))
    add("an American exercise", pspec, abe.exercise_spec("American", 365), barrier_spec(KNOCK_OUT, 80.0, 120.0),
        abe.payoffs(100.0)[:1])
    add("a cash-or-nothing payoff", pspec, ex, barrier_spec(KNOCK_OUT, 80.0, 120.0),
        [bc.payoff_spec("CashOrNothing", CALL, 100.0)])
    for name, lo, hi in [("the spot on the lower barrier", 100.0, 120.0), ("the spot on the upper barrier", 80.0, 100.0),
                         ("the spot below the corridor", 105.0, 120.0), ("the spot above the corridor", 80.0, 95.0)]:
        for tname, t in TYPES:
            add(f"{tname}, {name}", pspec, ex, barrier_spec(t, lo, hi), abe.payoffs(100.0)[:1])
    for tname, t in [("KIKO", KIKO), ("KOKI", KOKI)]:
        add(f"a {tname} type", pspec, ex, barrier_spec(t, 80.0, 120.0), abe.payoffs(100.0)[:1])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/barrier/analyticdoublebarrierengine.hpp", cases)
