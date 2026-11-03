# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SuoWangDoubleBarrierEngine (ql/experimental/barrieroption/suowangdoublebarrierengine.hpp,
.cpp), on the evaluation date 15 October 2026: European double knock-ins and knock-outs priced by Suo and Wang's image
series.

The analytic double-barrier generator's markets (tools/vectors/gen/pricingengines/barrier/analyticdoublebarrierengine.py):
three corridors around the spot (80-120%, 90-115% and 70-140% of it), over six months, a year and two years, on the
ten processes of tools/vectors/gen/processes/blackscholesprocess.py, each knock-in and knock-out a call and a put at
three strikes (90, 100 and 110% of the spot): the NPV at QuantLib's default five terms a side. Then an expired option
and the refusals: a cash-or-nothing payoff, a spot on a barrier and outside the corridor ("barrier touched"), the KIKO
and KOKI types, and an American exercise, which the engine's analytic European refuses.

The outputs nanoql fixes are left out: every process has positive volatilities and one day counter and reference date,
so none reads structures timed apart or a vanishing variance; every rebate is 0, which QuantLib's engine never reads;
every strike is inside the corridor, where QuantLib's one-sided limits are the clipped ones; and an output whose
knock-out on QuantLib's ten terms isn't within 5e-13 of its value at 50 a side, on max(|value|, 1), is left out of its
case: nanoql keeps QuantLib's terms only where they agree with its converged series within 1e-12 on that scale, the
knock-in being the European less the knock-out, and the margin keeps a case clear of that switch. A case holds the
payoffs left, and none is empty.

A case's inputs are the analytic double-barrier generator's. The wheel's calls, by QuantLib-SWIG 1.44's
barrieroptions.i: DoubleBarrierOption(barrierType, barrierLo, barrierHi, rebate, payoff, exercise),
SuoWangDoubleBarrierEngine(process, series) and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/Experimental/BarrierOption/SuoWangDoubleBarrierEngineVectorTests.cs."""
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


adbe = _load("analyticdoublebarrierengine", "pricingengines/barrier/analyticdoublebarrierengine.py")
abe, bsp, bc = adbe.abe, adbe.bsp, adbe.bc
R = adbe.R
CALL, PUT = adbe.CALL, adbe.PUT
KNOCK_IN, KNOCK_OUT, KIKO, KOKI = adbe.KNOCK_IN, adbe.KNOCK_OUT, adbe.KIKO, adbe.KOKI
AGREED = 5e-13


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, specs, keep=None):
        engine = ql.SuoWangDoubleBarrierEngine(bsp.process(pspec))
        values = [adbe.npv(barrier, p, ex, engine) for p in specs]
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
            for lo, hi in adbe.CORRIDORS:
                for tname, t in adbe.TYPES:
                    barrier = adbe.barrier_spec(t, x0 * lo, x0 * hi)
                    knock_out = adbe.barrier_spec(KNOCK_OUT, x0 * lo, x0 * hi)
                    specs = [p for p in abe.payoffs(x0)
                             if (p["strike"] > x0 * lo if p["optionType"] == PUT else p["strike"] < x0 * hi)]
                    process = bsp.process(pspec)
                    ten = [adbe.npv(knock_out, p, ex, ql.SuoWangDoubleBarrierEngine(process, 5)) for p in specs]
                    fifty = [adbe.npv(knock_out, p, ex, ql.SuoWangDoubleBarrierEngine(process, 50)) for p in specs]
                    keep = [abs(a - b) <= AGREED * max(abs(b), 1.0) for a, b in zip(ten, fifty)]
                    add(f"{pname}, {days} days, {tname} on [{lo:g}, {hi:g}] x spot", pspec, ex, barrier, specs, keep)
    pspec = bsp.PROCESSES[0][1]
    ex = abe.exercise_spec("European", 365)
    add("expired", pspec, abe.exercise_spec("European", -1), adbe.barrier_spec(KNOCK_OUT, 80.0, 120.0),
        abe.payoffs(100.0))
    add("an American exercise", pspec, abe.exercise_spec("American", 365), adbe.barrier_spec(KNOCK_OUT, 80.0, 120.0),
        abe.payoffs(100.0)[:1])
    add("a cash-or-nothing payoff", pspec, ex, adbe.barrier_spec(KNOCK_OUT, 80.0, 120.0),
        [bc.payoff_spec("CashOrNothing", CALL, 100.0)])
    for name, lo, hi in [("the spot on the lower barrier", 100.0, 120.0), ("the spot on the upper barrier", 80.0, 100.0),
                         ("the spot below the corridor", 105.0, 120.0), ("the spot above the corridor", 80.0, 95.0)]:
        for tname, t in adbe.TYPES:
            add(f"{tname}, {name}", pspec, ex, adbe.barrier_spec(t, lo, hi), abe.payoffs(100.0)[:1])
    for tname, t in [("KIKO", KIKO), ("KOKI", KOKI)]:
        add(f"a {tname} type", pspec, ex, adbe.barrier_spec(t, 80.0, 120.0), abe.payoffs(100.0)[:1])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/barrieroption/suowangdoublebarrierengine.hpp", cases)
