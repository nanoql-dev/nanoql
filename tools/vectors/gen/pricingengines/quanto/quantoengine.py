# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's QuantoEngine as QuantoEuropeanEngine and QuantoForwardEuropeanEngine
(ql/pricingengines/quanto/quantoengine.hpp, over AnalyticEuropeanEngine and ForwardEuropeanEngine), on the evaluation
date 15 October 2026: quanto calls and puts at strikes 90 and 110 expiring in six months or two years, and quanto
forward-start calls and puts on tools/vectors/gen/pricingengines/forward/forwardengine.py's periods at moneyness 0.9
and 1.1, on two flat markets under flat volatilities, at FX volatilities of 10% and 25% and correlations of -0.5 and
0.3: the NPV, delta, gamma, theta, vega, rho and dividend rho, and the vanillas' qvega, qrho and qlambda (the wheel's
QuantoForwardVanillaOption exposes no quanto sensitivities; the ported QuantoOptionTests' testForwardGreeks checks
them).

Left out (the S-exo plan's §2.10): volatilities off flat, where QuantLib reads qvega's and qlambda's volatility at the
spot and a quanto forward's drift at its payoff's strike (tests/NanoQL.Tests/Divergences/QuantoSmileTests.cs), and
the forward-start vega (ForwardVegaTermStructureTests.cs).

A case holds an option (a vanilla {"payoff", "exercise"} or forwardengine.py's forward spec, by its "kind"), a
tools/vectors/gen/processes/blackscholesprocess.py process spec and a quanto spec {"foreign" (a curve spec),
"fxVolatility" (a volatility spec), "correlation"}. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
QuantoVanillaOption(payoff, exercise), QuantoForwardVanillaOption(moneyness, resetDate, payoff, exercise),
QuantoEuropeanEngine and QuantoForwardEuropeanEngine(process, foreignRiskFreeRate, exchangeRateVolatility,
correlation), and the option's NPV, Greeks and quanto sensitivities. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Quanto/QuantoEngineVectorTests.cs."""
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


fwd = _load("forwardengine", "pricingengines/forward/forwardengine.py")
bsp, bc = fwd.bsp, fwd.bc
R = fwd.R
CALL, PUT = fwd.CALL, fwd.PUT
OUTPUTS = ["delta", "gamma", "theta", "vega", "rho", "dividendRho", "qvega", "qrho", "qlambda"]

QUANTOS = [
    ("FX 10%, correlation 0.3", {"foreign": bsp.flat_spec(R, 0.03), "fxVolatility": bsp.bvts.vol_spec_constant(R, 0.10),
                                 "correlation": 0.3}),
    ("FX 25%, correlation -0.5", {"foreign": bsp.flat_spec(R, 0.01),
                                  "fxVolatility": bsp.bvts.vol_spec_constant(R, 0.25), "correlation": -0.5}),
]


def quanto_args(q):
    return (ql.YieldTermStructureHandle(bsp.curve(q["foreign"])),
            ql.BlackVolTermStructureHandle(bsp.bvts.black_vol(q["fxVolatility"])),
            ql.QuoteHandle(ql.SimpleQuote(q["correlation"])))


def vanilla_setups():
    out = []
    for mname, pspec in fwd.MARKETS[:2]:
        for days in (182, 730):
            for t in (CALL, PUT):
                for k in (90.0, 110.0):
                    ospec = {"kind": "Vanilla", "payoff": bc.payoff_spec("PlainVanilla", t, k),
                             "exercise": {"type": "European", "date": (R + days).serialNumber()}}
                    out.append((f"{mname}, vanilla, expiry {days}, {'call' if t == CALL else 'put'}, strike {k:g}",
                                ospec, pspec))
    return out


def forward_setups():
    out = []
    for name, ospec, pspec in fwd.setups(markets=fwd.MARKETS[:2], moneyness=[0.9, 1.1]):
        out.append((f"{name.split(',')[0]}, forward,{name.split(',', 1)[1]}", dict(ospec, kind="Forward"), pspec))
    return out


def cases():
    st = v.settings(R)
    out = []
    for name, ospec, pspec in vanilla_setups() + forward_setups():
        for qname, q in QUANTOS:
            p = bsp.process(pspec)
            if ospec["kind"] == "Vanilla":
                o = ql.QuantoVanillaOption(bc.payoff(ospec["payoff"]),
                                           ql.EuropeanExercise(ql.Date(ospec["exercise"]["date"])))
                o.setPricingEngine(ql.QuantoEuropeanEngine(p, *quanto_args(q)))
            else:
                o = fwd.option(ospec, ql.QuantoForwardVanillaOption)
                o.setPricingEngine(ql.QuantoForwardEuropeanEngine(p, *quanto_args(q)))
            expected = {"NPV": o.NPV()}
            for g in OUTPUTS if ospec["kind"] == "Vanilla" else OUTPUTS[:6]:
                expected[g] = v.result(lambda: getattr(o, g)())
            out.append({"name": f"{name}, {qname}",
                        "inputs": {"settings": st, "option": ospec, "process": pspec, "quanto": q},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/quanto/quantoengine.hpp", cases)
