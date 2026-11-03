# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BarrierOption (ql/instruments/barrieroption.hpp, .cpp), on the evaluation date 15 October
2026: its implied volatility, which QuantLib solves through the analytic barrier engine, or with dividends through the
finite-difference barrier engine on its defaults.

The four barrier types (at 85% of the spot below it, 115% above), with and without a rebate (3% of the spot), calls and
puts at two strikes (90 and 110% of the spot), a year out, on five processes of
tools/vectors/gen/processes/blackscholesprocess.py, each solved for the values the analytic engine gives at volatilities
of 15, 25 and 40%, at an accuracy of 1e-10; then with cash dividends, the finite-difference engine's values at 25%; and
the refusals: an expired option ("option expired"), an American exercise and a value no volatility reaches.

**The stability filter**: each solve is repeated with its target value moved one ulp up and one down (math.nextafter); a
target is written only where both solves give its volatility within 1e-12 × max(1, |base|, |moved|), and dropped
otherwise. The generator prints the dropped targets.

The outputs nanoql fixes are left out: every process here has positive rates and one day counter and reference date, and
no rebate is negative, so no solve meets nanoql's fixes.

A case holds one process, exercise, barrier and payoff, its "targets", its "dividends"
(tools/vectors/gen/pricingengines/ vanilla/fdblackscholesvanillaengine.py's), "accuracy", "maxEvaluations", "minVol" and
"maxVol"; its output one volatility per target. The specs are
tools/vectors/gen/pricingengines/barrier/analyticbarrierengine.py's. The wheel's calls, by QuantLib-SWIG 1.44's
options.i: BarrierOption's impliedVolatility(targetValue, process, accuracy, maxEvaluations, minVol, maxVol) and its
overload with dividends. Read back by tests/NanoQL.Tests/Vectors/Instruments/BarrierOptionVectorTests.cs."""
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


fb = _load("fdblackscholesbarrierengine", "pricingengines/barrier/fdblackscholesbarrierengine.py")
ab, bc, bsm, bsp = fb.ab, fb.bc, fb.bsm, fb.bsp
R = fb.R
TOLERANCE = 1e-12
ACCURACY = 1e-10
DROPPED = []


def with_vol(pspec, vol):
    """The process spec with its volatility a constant one on the evaluation date."""
    return dict(pspec, volatility=bsp.bvts.vol_spec_constant(R, vol))


def solve(option, process, dividends, target):
    if dividends:
        return option.impliedVolatility(target, process, bsm.dividends(dividends), ACCURACY, 100, 1e-7, 4.0)
    return option.impliedVolatility(target, process, ACCURACY, 100, 1e-7, 4.0)


def stable(option, process, dividends, target, base):
    for moved in (math.nextafter(target, math.inf), math.nextafter(target, -math.inf)):
        try:
            m = solve(option, process, dividends, moved)
        except RuntimeError:
            return False
        if abs(m - base) > TOLERANCE * max(1.0, abs(base), abs(m)):
            return False
    return True


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, barrier, payoff, ex, targets, dividends=()):
        option = ab.option(barrier, payoff, ex)
        process = bsp.process(pspec)
        kept, vols = [], []
        for target in targets:
            try:
                base = solve(option, process, list(dividends), target)
            except RuntimeError:
                kept.append(target)
                vols.append(dict(v.THROWS))
                continue
            if stable(option, process, list(dividends), target, base):
                kept.append(target)
                vols.append(base)
            else:
                DROPPED.append(f"{name}: target {target!r}")
        if not kept:
            return
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "barrier": barrier, "payoff": payoff, "exercise": ex,
                               "dividends": list(dividends), "targets": kept, "accuracy": ACCURACY,
                               "maxEvaluations": 100, "minVol": 1e-7, "maxVol": 4.0},
                    "expected": {"impliedVolatility": vols}})

    def values_at(pspec, barrier, payoff, ex, vols, dividends=()):
        out_ = []
        for vol in vols:
            option = ab.option(barrier, payoff, ex)
            process = bsp.process(with_vol(pspec, vol))
            if dividends:
                option.setPricingEngine(ql.FdBlackScholesBarrierEngine(process, bsm.dividends(list(dividends))))
            else:
                option.setPricingEngine(ql.AnalyticBarrierEngine(process))
            out_.append(option.NPV())
        return out_

    year = ab.exercise_spec("European", 365)
    for pname, p in fb.PROCESSES:
        x0 = p["x0"]
        for tname, t in ab.TYPES:
            level = x0 * (0.85 if t in (ab.DOWN_IN, ab.DOWN_OUT) else 1.15)
            for rebate in (0.0, 0.03 * x0):
                barrier = ab.barrier_spec(t, level, rebate)
                for option_type in (fb.CALL, fb.PUT):
                    for f in (0.9, 1.1):
                        payoff = bc.payoff_spec("PlainVanilla", option_type, x0 * f)
                        add(f"{pname}, {tname} at {level:g}, rebate {rebate:g}, "
                            f"{'call' if option_type == fb.CALL else 'put'} {x0 * f:g}",
                            p, barrier, payoff, year, values_at(p, barrier, payoff, year, (0.15, 0.25, 0.40)))
    p = fb.PROCESSES[0][1]
    put = bc.payoff_spec("PlainVanilla", fb.PUT, 100.0)
    divs = [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)]
    for tname, t in ab.TYPES:
        level = 85.0 if t in (ab.DOWN_IN, ab.DOWN_OUT) else 115.0
        barrier = ab.barrier_spec(t, level, 3.0)
        add(f"dividends, {tname} put", p, barrier, put, year, values_at(p, barrier, put, year, (0.25,), divs), divs)
    down_out = ab.barrier_spec(ab.DOWN_OUT, 85.0, 3.0)
    add("expired", p, down_out, put, ab.exercise_spec("European", -1), [5.0])
    add("an American exercise", p, down_out, put, ab.exercise_spec("American", 365), [5.0])
    add("a value no volatility reaches", p, down_out, put, year, [500.0])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/barrieroption.hpp", cases)
    print(f"dropped {len(DROPPED)} targets")
    for d in DROPPED:
        print(" ", d)
