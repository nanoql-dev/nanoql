# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VarianceOption and IntegralHestonVarianceOptionEngine
(ql/experimental/varianceoption/varianceoption.hpp, .cpp, integralhestonvarianceoptionengine.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: calls on the realized variance under Heston.

QuantLib's case's two processes and a third near its long-run variance, calls at strikes around the expected
integrated variance over six months, a year and eighteen months, at rates of 0 and 3% and notionals of 1 and 100; the
NPV. Then a process with 2 kappa theta / sigma^2 at most 1, which the engine refuses.

The outputs nanoql fixes are left out: every option starts on the evaluation date, the risk-free curve is dated on it,
and every payoff is a call: QuantLib's two-dimensional integral, its route for every other payoff, is nanoql's
departure (a put by parity, other payoffs refused). The process and option are
tools/cxx/src/experimental/varianceoption/integralhestonvarianceoptionengine.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/VarianceOption/IntegralHestonVarianceOptionEngineCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
CALL = 1
KEYS = ("riskFreeRate", "v0", "kappa", "theta", "sigma", "rho", "optionType", "strike", "notional", "maturityDays")
PROCESSES = [("QuantLib's call case's process", (2.0, 2.0, 0.01, 0.1, -0.5)),
             ("QuantLib's put case's process", (1.5, 2.0, 0.01, 0.1, -0.5)),
             ("a process near its long-run variance", (0.05, 1.5, 0.04, 0.2, -0.7))]


def mean(v0, kappa, theta, tau):
    return theta * tau + (v0 - theta) * (1.0 - math.exp(-kappa * tau)) / kappa


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def add(name, s):
            try:
                expected = {"NPV": H.call("IntegralHestonVarianceOptionEngine", *[s[k] for k in KEYS])}
            except RuntimeError:
                expected = {"NPV": {"throws": True}}
            out.append({"name": name, "inputs": {"settings": st, "option": s}, "expected": expected})

        for pname, (v0, kappa, theta, sigma, rho) in PROCESSES:
            for days in (180, 360, 540):
                expected_variance = mean(v0, kappa, theta, days / 360.0)
                for m in (0.8, 1.0, 1.2):
                    strike = round(expected_variance * m, 6)
                    for r, notional in ((0.0, 1.0), (0.03, 100.0)):
                        s = dict(zip(KEYS, (r, v0, kappa, theta, sigma, rho, CALL, strike, notional, days)))
                        add(f"{pname}, {days} days, strike {strike:g}, r {r:g}, notional {notional:g}", s)
        add("2 kappa theta / sigma^2 = 0.8 (refused)",
            dict(zip(KEYS, (0.0, 0.04, 1.0, 0.04, 0.2 * math.sqrt(2.5), -0.5, CALL, 0.03, 1.0, 360))))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/varianceoption/integralhestonvarianceoptionengine.hpp", cases)
