# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's QuantoBarrierOption (ql/instruments/quantobarrieroption.hpp, .cpp) priced by its
QuantoEngine over AnalyticBarrierEngine (QuantoBarrierEngine), on the evaluation date 15 October 2026: barrier options
quantoed by the FX volatility and correlation, the domestic curve the process's risk-free one.

Each of the four barrier types at a barrier on each side of the spot (90% and 110% of it), with and without a rebate
(3% of the spot), over six months and two years, on the four processes of tools/vectors/gen/processes/
blackscholesprocess.py under a constant volatility, a call and a put at the spot, under
tools/vectors/gen/pricingengines/quanto/quantoengine.py's two quanto specs: the NPV and the Greeks, which QuantLib's
analytic barrier engine leaves unset (null; the wheel's QuantoBarrierOption exposes no quanto sensitivities, which the
ported QuantoOptionTests' testBarrierValues checks). Then an expired option and the refusal of a spot beyond the
barrier.

The outputs nanoql fixes are left out: every process here has positive rates and volatilities and one day counter and
reference date, so none is at zero volatility or on structures dated or day-counted apart, and no rebate is negative
or has no real lambda (the analytic barrier engine's divergence facts); and every volatility is flat, where QuantLib
reads the quanto sensitivities' volatility at the spot (tests/NanoQL.Tests/Divergences/QuantoSmileTests.cs).

A case holds one process, exercise and barrier (tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's
specs) and a quanto spec (quantoengine.py's), its outputs one per payoff. The wheel's calls, by QuantLib-SWIG 1.44's
options.i: QuantoBarrierOption(barrierType, barrier, rebate, payoff, exercise), QuantoBarrierEngine(process,
foreignRiskFreeRate, exchangeRateVolatility, correlation), and the option's NPV and Greeks. Read
back by tests/NanoQL.Tests/Vectors/Instruments/QuantoBarrierOptionVectorTests.cs."""
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
qe = _load("quantoengine", "pricingengines/quanto/quantoengine.py")
bsp, bc = abe.bsp, abe.bc
R = abe.R
CALL, PUT = abe.CALL, abe.PUT
PROCESSES = [p for p in bsp.PROCESSES if p[1]["volatility"]["type"] == "BlackConstantVol"]
OUTPUTS = ["delta", "gamma", "theta", "vega", "rho", "dividendRho"]


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex, barrier, q, specs):
        engine = ql.QuantoBarrierEngine(bsp.process(pspec), *qe.quanto_args(q))
        options = []
        for spec in specs:
            o = ql.QuantoBarrierOption(barrier["barrierType"], barrier["barrier"], barrier["rebate"], bc.payoff(spec),
                                       abe.exercise(ex))
            o.setPricingEngine(engine)
            options.append(o)
        expected = {"NPV": [v.attempt(o.NPV) for o in options]}
        if all(isinstance(x, float) for x in expected["NPV"]):
            for g in OUTPUTS:
                expected[g] = [v.result(getattr(o, g)) for o in options]
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exercise": ex, "barrier": barrier, "quanto": q,
                               "payoffs": specs},
                    "expected": expected})

    for pname, pspec in PROCESSES:
        x0 = pspec["x0"]
        specs = [bc.payoff_spec("PlainVanilla", t, x0) for t in (CALL, PUT)]
        for days in (182, 730):
            ex = abe.exercise_spec("European", days)
            for tname, t in abe.TYPES:
                level = x0 * (0.9 if t in (abe.DOWN_IN, abe.DOWN_OUT) else 1.1)
                for rebate in (0.0, 0.03 * x0):
                    for qname, q in qe.QUANTOS:
                        add(f"{pname}, {days} days, {tname} at {level:g}, rebate {rebate:g}, {qname}", pspec, ex,
                            abe.barrier_spec(t, level, rebate), q, specs)
    pspec = PROCESSES[0][1]
    q = qe.QUANTOS[0][1]
    add("expired", pspec, abe.exercise_spec("European", -1), abe.barrier_spec(abe.DOWN_OUT, 90.0, 3.0), q,
        [bc.payoff_spec("PlainVanilla", CALL, 100.0)])
    add("the spot beyond the barrier", pspec, abe.exercise_spec("European", 365),
        abe.barrier_spec(abe.DOWN_OUT, 105.0, 3.0), q, [bc.payoff_spec("PlainVanilla", CALL, 100.0)])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/quantobarrieroption.hpp", cases)
