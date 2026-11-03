# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCDoubleBarrierEngine and DoubleBarrierPathPricer
(ql/experimental/barrieroption/mcdoublebarrierengine.hpp, .cpp), which the wheel doesn't expose, each output computed
by the C++ harness: fixed-seed Monte Carlo prices and the path pricer on given paths.

The engine: knock-ins and knock-outs on [80, 120], a call and a put struck at 100, rebates of 0 and 2, with and without
antithetic variates, at QuantLib's case's seeds 1 and 10 and at 42, on 50 steps and 2,000 samples, spot 100, rates of
3% and 1% and a volatility of 20%, six months to expiry, on pseudo-random paths ("traits": "pr"): the NPV and the error
estimate. Then a KIKO, which the path pricer refuses ("unknown barrier type"), and a spot on a barrier ("barrier
touched"). Then each type, payoff and rebate on low-discrepancy paths ("ld"), plain, seed 42: the NPV alone, as the
traits give no error estimate. The path pricer: each type on three paths over a quarter, inside, through the lower
barrier and through the upper.

No output is left out: the process's structures share the evaluation date and Act/365, so the engine's clock is the
risk-free curve's; every exercise is European. The process and option are
tools/cxx/src/experimental/barrieroption/mcdoublebarrierengine.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/BarrierOption/MCDoubleBarrierEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
CALL, PUT = 1, -1
KNOCK_IN, KNOCK_OUT, KIKO = 0, 1, 2
TYPES = [("knock-in", KNOCK_IN), ("knock-out", KNOCK_OUT)]


def spec(barrier_type, option_type, rebate, antithetic, seed, lo=80.0, hi=120.0, traits="pr"):
    return {"traits": traits, "referenceDate": R, "spot": 100.0, "dividendYield": 0.01, "riskFreeRate": 0.03, "vol": 0.2,
            "barrierType": barrier_type, "barrierLo": lo, "barrierHi": hi, "rebate": rebate, "optionType": option_type,
            "strike": 100.0, "expiryDays": 182, "steps": 50, "antithetic": antithetic, "samples": 2000, "seed": seed}


def arguments(s):
    return [s[k] for k in ("traits", "referenceDate", "spot", "dividendYield", "riskFreeRate", "vol", "barrierType", "barrierLo",
                           "barrierHi", "rebate", "optionType", "strike", "expiryDays", "steps", "antithetic",
                           "samples", "seed")]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def engine(name, s):
            try:
                r = H.call("MCDoubleBarrierEngine", *arguments(s))
                expected = {k: r[k] for k in ("NPV", "errorEstimate") if k in r}
            except RuntimeError:
                expected = {"NPV": {"throws": True}}
            out.append({"name": name, "inputs": {"settings": st, "option": s}, "expected": expected})

        for tname, t in TYPES:
            for kind, option_type in (("call", CALL), ("put", PUT)):
                for rebate in (0.0, 2.0):
                    for antithetic in (False, True):
                        for seed in (1, 10, 42):
                            engine(f"{tname} {kind}, rebate {rebate:g}{', antithetic' if antithetic else ''}, "
                                   f"seed {seed}", spec(t, option_type, rebate, antithetic, seed))
        engine("a KIKO", spec(KIKO, CALL, 0.0, False, 42))
        engine("the spot on the lower barrier", spec(KNOCK_OUT, CALL, 0.0, False, 42, lo=100.0))
        for tname, t in TYPES:
            for kind, option_type in (("call", CALL), ("put", PUT)):
                for rebate in (0.0, 2.0):
                    engine(f"low-discrepancy, {tname} {kind}, rebate {rebate:g}",
                           spec(t, option_type, rebate, False, 42, traits="ld"))
        times = [0.0, 0.0625, 0.125, 0.1875, 0.25]
        discounts = [1.0, 0.998, 0.996, 0.994, 0.992]
        paths = [("inside", [100.0, 104.0, 97.0, 110.0, 115.0]), ("through the lower barrier", [100.0, 90.0, 79.0, 85.0,
                                                                                               95.0]),
                 ("through the upper barrier", [100.0, 112.0, 121.0, 118.0, 99.0])]
        for tname, t in TYPES:
            for pname, values in paths:
                p = {"barrierType": t, "barrierLo": 80.0, "barrierHi": 120.0, "rebate": 2.0, "optionType": CALL,
                     "strike": 100.0, "discounts": discounts, "times": times, "values": values}
                value = H.call("DoubleBarrierPathPricer", t, 80.0, 120.0, 2.0, CALL, 100.0, discounts, times, values)
                out.append({"name": f"path pricer, {tname}, {pname}", "inputs": {"settings": st, "pathPricer": p},
                            "expected": {"value": value}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/barrieroption/mcdoublebarrierengine.hpp", cases)
