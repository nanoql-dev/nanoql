# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's QuantoDoubleBarrierOption (ql/experimental/barrieroption/quantodoublebarrieroption.hpp, .cpp)
under QuantoEngine<DoubleBarrierOption, AnalyticDoubleBarrierEngine>, which the wheel binds without an engine, each
output computed by the C++ harness.

Knock-ins and knock-outs on [80, 120] and [70, 140], calls and puts at 90, 100 and 110, spot 100, a dividend yield of
1%, a domestic rate of 5% and a volatility of 25%, a foreign rate of 3% and an exchange-rate volatility of 20%, at
correlations of −0.5, 0 and 0.5, over six months and a year: the NPV. Then an expired option.

The outputs nanoql fixes are left out (the S-xeq plan's §2.8, §2.10): every structure shares the evaluation date and
Act/365, every rebate is 0, every strike is inside the corridor and the volatility isn't low, where S-bar's fixes of
the analytic engine leave QuantLib's values; and an output whose inner knock-out at five terms isn't within 5e-13 of
its value at 50 on max(|value|, 1), computed on the wheel's analytic engine at the quanto dividend yield, is left out,
the engine keeping QuantLib's five terms only within 1e-12 of its converged series (S-bar's BR5 (xliii)). The process
and option are tools/cxx/src/experimental/barrieroption/quantodoublebarrieroption.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/BarrierOption/QuantoDoubleBarrierOptionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
CALL, PUT = 1, -1
TYPES = [("knock-in", 0), ("knock-out", 1)]
CORRIDORS = [(80.0, 120.0), (70.0, 140.0)]
AGREED = 5e-13
A365 = ql.Actual365Fixed()


def spec(correlation, barrier_type, lo, hi, option_type, strike, days):
    return {"referenceDate": R, "spot": 100.0, "dividendYield": 0.01, "riskFreeRate": 0.05, "vol": 0.25,
            "foreignRate": 0.03, "fxVol": 0.2, "correlation": correlation, "barrierType": barrier_type,
            "barrierLo": lo, "barrierHi": hi, "rebate": 0.0, "optionType": option_type, "strike": strike,
            "expiryDays": days}


def arguments(s):
    return [s[k] for k in ("referenceDate", "spot", "dividendYield", "riskFreeRate", "vol", "foreignRate", "fxVol",
                           "correlation", "barrierType", "barrierLo", "barrierHi", "rebate", "optionType", "strike",
                           "expiryDays")]


def converged(s):
    # the inner engine's knock-out on the quanto process: the dividend yield raised by r − r_f + ρσσ_fx, flat
    d = ql.Date(R)
    q = s["dividendYield"] + s["riskFreeRate"] - s["foreignRate"] + s["correlation"] * s["vol"] * s["fxVol"]
    process = ql.BlackScholesMertonProcess(
        ql.QuoteHandle(ql.SimpleQuote(s["spot"])), ql.YieldTermStructureHandle(ql.FlatForward(d, q, A365)),
        ql.YieldTermStructureHandle(ql.FlatForward(d, s["riskFreeRate"], A365)),
        ql.BlackVolTermStructureHandle(ql.BlackConstantVol(d, ql.NullCalendar(), s["vol"], A365)))
    values = []
    for series in (5, 50):
        o = ql.DoubleBarrierOption(ql.DoubleBarrier.KnockOut, s["barrierLo"], s["barrierHi"], 0.0,
                                   ql.PlainVanillaPayoff(s["optionType"], s["strike"]),
                                   ql.EuropeanExercise(d + s["expiryDays"]))
        o.setPricingEngine(ql.AnalyticDoubleBarrierEngine(process, series))
        values.append(o.NPV())
    return abs(values[0] - values[1]) <= AGREED * max(abs(values[1]), 1.0)


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def add(name, s):
            npv = v.attempt(lambda: H.call("QuantoDoubleBarrierOption", *arguments(s)))
            out.append({"name": name, "inputs": {"settings": st, "option": s}, "expected": {"NPV": npv}})

        for correlation in (-0.5, 0.0, 0.5):
            for tname, t in TYPES:
                for lo, hi in CORRIDORS:
                    for kind, option_type in (("call", CALL), ("put", PUT)):
                        for strike in (90.0, 100.0, 110.0):
                            for days in (182, 365):
                                s = spec(correlation, t, lo, hi, option_type, strike, days)
                                if not converged(s):
                                    continue
                                add(f"correlation {correlation:g}, {tname} on [{lo:g}, {hi:g}], {kind} {strike:g}, "
                                    f"{days} days", s)
        add("expired", spec(0.3, 1, 80.0, 120.0, CALL, 100.0, -1))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/barrieroption/quantodoublebarrieroption.hpp", cases)
