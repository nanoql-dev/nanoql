# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticContinuousGeometricAveragePriceAsianHestonEngine
(ql/experimental/asian/analytic_cont_geom_av_price_heston.hpp, .cpp), on the evaluation date 15 October 2026:
continuous geometric average-price Asian options under Heston, Kim and Wee's characteristic function inverted.

Three Heston processes (S₀ 100 on flat curves of 5% and 0% and of 3% and 1% on Actual/365F from the evaluation date):
Kim and Wee's Feller and non-Feller parameters and a positive correlation; expiries in 73, 365 and 1,095 days; calls and
puts at 90, 100 and 110: the NPV, at QuantLib's default cutoff and integral limit and at a cutoff of 30. Then an expired
option and an arithmetic average ("not a geometric average option").

No output is left out: the prices read <c>Phi</c> at w = 0 only, where nanoql's coefficient of w doesn't enter; no
option carries a start date, which nanoql refuses; and the curves share the evaluation date and day counter, so the
model's clock is the risk-free curve's.

A case holds one process (tools/vectors/gen/processes/hestonprocess.py's spec), the exercise date, the average type, the
cutoff and the limit, and the payoffs (bc's specs). The wheel's calls, by QuantLib-SWIG 1.44's asianoptions.i:
ContinuousAveragingAsianOption(averageType, payoff, exercise),
AnalyticContinuousGeometricAveragePriceAsianHestonEngine(process, summationCutoff, xiRightLimit) and the option's NPV.
Read back by
tests/NanoQL.Tests/Vectors/Experimental/Asian/AnalyticContinuousGeometricAveragePriceAsianHestonEngineVectorTests.cs."""
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


hp = _load("hestonprocess", "processes/hestonprocess.py")
bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
bsp = hp.bsp
R = hp.R
CALL, PUT = 1, -1
PARAMETERS = [("Kim and Wee's Feller set", 0.05, 0.0, 0.09, 1.15, 0.348, 0.39, -0.64),
              ("Kim and Wee's non-Feller set", 0.05, 0.0, 0.09, 2.0, 0.09, 1.0, -0.3),
              ("a positive correlation", 0.03, 0.01, 0.04, 1.5, 0.05, 0.5, 0.4)]
EXPIRIES = [73, 365, 1095]
STRIKES = [90.0, 100.0, 110.0]
GEOMETRIC, ARITHMETIC = int(ql.Average.Geometric), int(ql.Average.Arithmetic)


def process_spec(r, q, v0, kappa, theta, sigma, rho):
    return {"riskFree": bsp.flat_spec(R, r), "dividend": bsp.flat_spec(R, q), "s0": 100.0, "v0": v0, "kappa": kappa,
            "theta": theta, "sigma": sigma, "rho": rho,
            "discretization": int(ql.HestonProcess.QuadraticExponentialMartingale)}


def npv(pspec, ex_date, average, cutoff, limit, payoff):
    o = ql.ContinuousAveragingAsianOption(average, bc.payoff(payoff), ql.EuropeanExercise(ql.Date(ex_date)))
    o.setPricingEngine(ql.AnalyticContinuousGeometricAveragePriceAsianHestonEngine(hp.process(pspec), cutoff, limit))
    return v.attempt(o.NPV)


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, ex_date, average, cutoff, limit, specs):
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "exerciseDate": ex_date, "averageType": average,
                               "summationCutoff": cutoff, "xiRightLimit": limit, "payoffs": specs},
                    "expected": {"NPV": [npv(pspec, ex_date, average, cutoff, limit, p) for p in specs]}})

    specs = [bc.payoff_spec("PlainVanilla", t, k) for t in (CALL, PUT) for k in STRIKES]
    for pname, r, q, v0, kappa, theta, sigma, rho in PARAMETERS:
        pspec = process_spec(r, q, v0, kappa, theta, sigma, rho)
        for days in EXPIRIES:
            for cutoff in (50, 30):
                add(f"{pname}, {days} days, cutoff {cutoff}", pspec, (R + days).serialNumber(), GEOMETRIC, cutoff,
                    100.0, specs)
    pspec = process_spec(*PARAMETERS[0][1:])
    add("expired", pspec, (R - 1).serialNumber(), GEOMETRIC, 50, 100.0, specs[:1])
    add("an arithmetic average", pspec, (R + 365).serialNumber(), ARITHMETIC, 50, 100.0, specs[:1])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/asian/analytic_cont_geom_av_price_heston.hpp", cases)
