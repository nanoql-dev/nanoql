# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NoArbSabrSmileSection and NoArbSabrModel (ql/experimental/volatility/noarbsabrsmilesection.hpp,
.cpp, noarbsabr.hpp, .cpp), on the evaluation date 15 October 2026: Doust's no-arbitrage SABR sections on five
parameter sets (QuantLib's Hagan case, three across the model's domain, and the documented corner at an expiry of 30,
rho 0.9, nu 0.8, where the forward adjustment fails, QuantLib's numbers kept): each section's call and put prices, its
digital call and put prices, its density and its volatility at strikes from half to twice the forward, its at-the-money
level and its strike range; two sections dated by an exercise date, on Actual/365 (Fixed) and Actual/360, with their
exercise times; then a beta past the domain and a nonzero shift, which the model and the section refuse.

The outputs nanoql fixes are left out (the S-xeq plan's §2.8): every set has sigma_I sqrt(T) at least 0.1, where
QuantLib's single integration interval stands (XER5 (xxvi)).

The specs: a section {"expiry", "forward", "params" [alpha, beta, nu, rho], "shift"}, or for a dated one
{"exerciseDays", "dayCounter", "forward", "params", "shift"}, the exercise date that many days after the evaluation
date, and the strikes. The wheel's calls, by QuantLib-SWIG 1.44's volatilities.i: NoArbSabrSmileSection(timeToExpiry,
forward, sabrParameters, shift) and NoArbSabrSmileSection(d, forward, sabrParameters, dc, shift), and their
optionPrice(strike, type), digitalOptionPrice(strike, type), density(strike), volatility(strike), atmLevel(),
minStrike(), maxStrike() and exerciseTime().
Read back by tests/NanoQL.Tests/Vectors/Experimental/Volatility/NoArbSabrSmileSectionVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)


def alpha_for(sigma_i, forward, beta):
    return sigma_i / forward ** (beta - 1.0)


SECTIONS = [
    ("QuantLib's Hagan case", 1.0, 0.0488, [0.026, 0.5, 0.4, -0.1]),
    ("five years, sigma_I 0.3", 5.0, 0.03, [alpha_for(0.3, 0.03, 0.5), 0.5, 0.3, -0.3]),
    ("three months, sigma_I 0.4", 0.25, 0.04, [alpha_for(0.4, 0.04, 0.7), 0.7, 0.6, 0.2]),
    ("ten years, sigma_I 0.2", 10.0, 0.02, [alpha_for(0.2, 0.02, 0.3), 0.3, 0.5, -0.6]),
    ("the documented corner", 30.0, 0.03, [alpha_for(0.3, 0.03, 0.5), 0.5, 0.8, 0.9]),
]


DATED = [
    ("five years by date, Actual/365 (Fixed)", 1826, {"type": "Actual365Fixed"}, ql.Actual365Fixed(), 0.03,
     [alpha_for(0.3, 0.03, 0.5), 0.5, 0.3, -0.3]),
    ("two years by date, Actual/360", 730, {"type": "Actual360"}, ql.Actual360(), 0.04,
     [alpha_for(0.3, 0.04, 0.6), 0.6, 0.4, 0.1]),
]


def priced(s, strikes):
    return {"call": [s.optionPrice(k, ql.Option.Call) for k in strikes],
            "put": [s.optionPrice(k, ql.Option.Put) for k in strikes],
            "digitalCall": [s.digitalOptionPrice(k, ql.Option.Call) for k in strikes],
            "digitalPut": [s.digitalOptionPrice(k, ql.Option.Put) for k in strikes],
            "density": [s.density(k) for k in strikes],
            "volatility": [s.volatility(k) for k in strikes],
            "atmLevel": s.atmLevel(), "minStrike": s.minStrike(), "maxStrike": s.maxStrike()}


def cases():
    st = v.settings(R)
    out = []
    for name, t, f, params in SECTIONS:
        strikes = [f * m for m in (0.5, 0.75, 0.9, 1.0, 1.1, 1.25, 1.5, 2.0)]
        s = ql.NoArbSabrSmileSection(t, f, params)
        out.append({"name": name,
                    "inputs": {"settings": st, "section": {"expiry": t, "forward": f, "params": params, "shift": 0.0},
                               "strikes": strikes},
                    "expected": priced(s, strikes)})
    for name, days, dc_spec, dc, f, params in DATED:
        strikes = [f * m for m in (0.5, 0.75, 0.9, 1.0, 1.1, 1.25, 1.5, 2.0)]
        s = ql.NoArbSabrSmileSection(R + days, f, params, dc)
        expected = priced(s, strikes)
        expected["exerciseTime"] = s.exerciseTime()
        out.append({"name": name,
                    "inputs": {"settings": st, "section": {"exerciseDays": days, "dayCounter": dc_spec, "forward": f,
                                                           "params": params, "shift": 0.0},
                               "strikes": strikes},
                    "expected": expected})
    for name, params, shift in [("beta 1 (refused)", [0.05, 1.0, 0.3, 0.0], 0.0),
                                ("a shift (refused)", [0.05, 0.5, 0.3, 0.0], 0.01)]:
        out.append({"name": name,
                    "inputs": {"settings": st,
                               "section": {"expiry": 1.0, "forward": 0.03, "params": params, "shift": shift},
                               "strikes": [0.03]},
                    "expected": {"atmLevel": v.attempt(lambda: ql.NoArbSabrSmileSection(1.0, 0.03, params,
                                                                                        shift).atmLevel())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/noarbsabrsmilesection.hpp", cases)
