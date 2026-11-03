# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticHestonForwardEuropeanEngine
(ql/experimental/forward/analytichestonforwardeuropeanengine.hpp, .cpp), on the evaluation date 15 October 2026:
forward-starting European options under Heston priced by Kruse's nested integrals.

Three Heston processes (S₀ 100 on flat curves of 3% and 1% on Actual/365F from the evaluation date): v₀ 0.04, κ 1.5,
θ 0.04 at vols of vol 0.2 and 0.3, ρ −0.5 and 0.3; resets today (the engine's vanilla route), in 30 days and, at the
vol of vol 0.2, in 91 and 182; expiries in one and two years; calls and puts at moneyness 0.9, 1.0 and 1.1: the NPV.
Then an expired option.

The outputs nanoql fixes are left out: every reset is today or a month and more ahead, where QuantLib's propagator is
finite, and its outer integral's nodes carry the propagator's unit mass within 1e-6, so nanoql keeps QuantLib's
integral: at the vol of vol 0.3, 4κθ/σ² 2.67, they miss it from the 91-day reset on, where nanoql integrates over the
propagator's support (by up to 6.6e-4 at 182 days; tests/NanoQL.Tests/Divergences/HestonForwardEngineTests.cs), so that
process stops at 30 days; no κ − ρσ is near 0; and the curves share the evaluation date and day counter, so the model's
clock is the risk-free curve's.

A case holds one process (tools/vectors/gen/processes/hestonprocess.py's spec), the reset, the exercise date and the
payoffs, each a moneyness and an option type. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
ForwardVanillaOption(moneyness, resetDate, payoff, exercise), AnalyticHestonForwardEuropeanEngine(process, 144) and the
option's NPV. Read back by
tests/NanoQL.Tests/Vectors/Experimental/Forward/AnalyticHestonForwardEuropeanEngineVectorTests.cs."""
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
bsp = hp.bsp
R = hp.R
CALL, PUT = 1, -1
PARAMETERS = [("vol of vol 0.2, rho -0.5", 0.2, -0.5, [0, 30, 91, 182]),
              ("vol of vol 0.3, rho -0.5", 0.3, -0.5, [0, 30]),
              ("vol of vol 0.3, rho 0.3", 0.3, 0.3, [0, 30])]
EXPIRIES = [365, 730]
MONEYNESS = [0.9, 1.0, 1.1]


def process_spec(sigma, rho):
    return {"riskFree": bsp.flat_spec(R, 0.03), "dividend": bsp.flat_spec(R, 0.01),
            "s0": 100.0, "v0": 0.04, "kappa": 1.5, "theta": 0.04, "sigma": sigma, "rho": rho,
            "discretization": int(ql.HestonProcess.QuadraticExponentialMartingale)}


def npv(pspec, reset, ex_date, payoff):
    o = ql.ForwardVanillaOption(payoff["moneyness"], ql.Date(reset), ql.PlainVanillaPayoff(payoff["optionType"], 0.0),
                                ql.EuropeanExercise(ql.Date(ex_date)))
    o.setPricingEngine(ql.AnalyticHestonForwardEuropeanEngine(hp.process(pspec), 144))
    return v.attempt(o.NPV)


def cases():
    st = v.settings(R)
    out = []

    def add(name, pspec, reset, ex_date, specs):
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "resetDate": reset, "exerciseDate": ex_date,
                               "payoffs": specs},
                    "expected": {"NPV": [npv(pspec, reset, ex_date, p) for p in specs]}})

    for pname, sigma, rho, resets in PARAMETERS:
        pspec = process_spec(sigma, rho)
        for reset in resets:
            for expiry in EXPIRIES:
                specs = [{"moneyness": m, "optionType": t} for t in (CALL, PUT) for m in MONEYNESS]
                add(f"{pname}, reset in {reset} days, expiry in {expiry}", pspec, (R + reset).serialNumber(),
                    (R + expiry).serialNumber(), specs)
    pspec = process_spec(0.3, -0.5)
    add("expired", pspec, (R - 30).serialNumber(), (R - 1).serialNumber(), [{"moneyness": 1.0, "optionType": CALL}])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/forward/analytichestonforwardeuropeanengine.hpp", cases)
