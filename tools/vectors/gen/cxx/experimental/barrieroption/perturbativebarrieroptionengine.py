# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PerturbativeBarrierOptionEngine (ql/experimental/barrieroption/perturbativebarrieroptionengine.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: up-and-out puts at order 0, and the
engine's refusals.

Three volatility markets on Act/365 from the evaluation date (a Black variance curve at 10.5% and 11% at 90 and 180
days, QuantLib's case's; at 10% and 30%; and flat at 20% to a year), each with rates of 3% and 2%, spot 100: up-and-out
puts struck at 95, 101 and 110 under barriers at 101, 110 and 120 (a strike above its barrier among them), expiring in
180 days, with the engine's γ and with γ 0, at QuantLib's point counts: the NPV at order 0. Then the refusals: a
down-and-out, a rebate, a call, an order of 3 and a zero point count.

The outputs nanoql fixes are left out: orders 1 and 2 read the engine's bivariate normal, whose |ρ| ≥ 0.925 branch
nanoql takes from Genz, and order 2 its trivariate normal and derivative helpers, so they are checked by
tests/NanoQL.Tests/Divergences/PerturbativeBarrierEngineTests.cs and QuantLib's own cases; every structure shares the
evaluation date and Act/365, the engine's clock. The process and option are
tools/cxx/src/experimental/barrieroption/perturbativebarrieroptionengine.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/BarrierOption/PerturbativeBarrierOptionEngineCxxVectorTests.cs."""
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
DOWN_OUT, UP_OUT = 2, 3
MARKETS = [("QuantLib's variance curve", [90, 180], [0.105, 0.11]), ("a steep variance curve", [90, 180], [0.10, 0.30]),
           ("a flat volatility", [365], [0.20])]
OPTIONS = [(95.0, 110.0), (101.0, 101.0), (110.0, 120.0), (110.0, 101.0)]


def spec(market, strike, barrier, barrier_type=UP_OUT, rebate=0.0, option_type=PUT, order=0, zero_gamma=False,
         npoint=1000, npoint2=100):
    _, days, vols = market
    return {"referenceDate": R, "spot": 100.0, "dividendYield": 0.02, "riskFreeRate": 0.03, "varianceDays": days,
            "vols": vols, "barrierType": barrier_type, "barrier": barrier, "rebate": rebate, "optionType": option_type,
            "strike": strike, "expiryDays": 180, "order": order, "zeroGamma": zero_gamma, "npoint": npoint,
            "npoint2": npoint2}


def arguments(s):
    return [s["referenceDate"], s["spot"], s["dividendYield"], s["riskFreeRate"], s["varianceDays"], s["vols"],
            s["barrierType"], s["barrier"], s["rebate"], s["optionType"], s["strike"], s["expiryDays"], s["order"],
            s["zeroGamma"], s["npoint"], s["npoint2"]]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def add(name, s):
            npv = v.attempt(lambda: H.call("PerturbativeBarrierOptionEngine", *arguments(s)))
            out.append({"name": name, "inputs": {"settings": st, "option": s}, "expected": {"NPV": npv}})

        for market in MARKETS:
            for strike, barrier in OPTIONS:
                for zero_gamma in (False, True):
                    add(f"{market[0]}, K {strike:g}, H {barrier:g}{', gamma 0' if zero_gamma else ''}",
                        spec(market, strike, barrier, zero_gamma=zero_gamma))
        market = MARKETS[0]
        add("a down-and-out", spec(market, 101.0, 90.0, barrier_type=DOWN_OUT))
        add("a rebate", spec(market, 101.0, 110.0, rebate=1.0))
        add("a call", spec(market, 101.0, 110.0, option_type=CALL))
        add("order 3", spec(market, 101.0, 110.0, order=3))
        add("no outer points", spec(market, 101.0, 110.0, order=1, npoint=0))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/barrieroption/perturbativebarrieroptionengine.hpp", cases)
