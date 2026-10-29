# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VarianceGammaProcess, VarianceGammaEngine and FFTVarianceGammaEngine
(ql/experimental/variancegamma/variancegammaprocess.hpp, .cpp, variancegammaengine.hpp, .cpp,
fftvariancegammaengine.hpp, .cpp), on the evaluation date 15 October 2026: European calls and puts at 90, 100 and 110%
of the spot over six months and a year (two years where nu is at least 0.02) on four processes, QuantLib's case's two
at a spot of 6,000 and two at 100: the analytic engine's NPV at its default absolute error, the FFT engine's NPV on each
option alone and on each process's options as one list (precalculate), the process's x0, and its drift and diffusion,
which it refuses.

The outputs nanoql fixes are left out (the S-xeq plan's §2.8): every process has 1 - theta*nu - sigma^2*nu/2 positive
(XER5 (xvii)) and the moment-generating function's upper strip bound above 2.25, where QuantLib's damping of 1.25
stands (XER5 (xviii)); every strike is above 1 and every list's largest strike gives QuantLib at least 2^14 points,
where nanoql sizes its grid as QuantLib does (XER5 (xix)); and nu is at least a hundredth of each expiry, where nanoql's
integral starts where QuantLib's does (the S-xeq run's R2-4). The analytic engine's density is a product in QuantLib
and a sum of logs in nanoql (XER5 (xvi)), equal within R7.

The specs: a process {"s0", "dividendYield", "riskFreeRate", "sigma", "nu", "theta"}, flat continuous curves on Act/365
(Fixed) from the evaluation date, and options {"optionType", "strike", "expiryDays"}. The wheel's calls, by
QuantLib-SWIG 1.44's options.i and stochasticprocess.i: VarianceGammaProcess(s0, dividendYield, riskFreeRate, sigma, nu,
theta); VarianceGammaEngine(process); FFTVarianceGammaEngine(process) with precalculate(options) for the list; the
option's NPV; the process's x0, drift and diffusion. Read back by
tests/NanoQL.Tests/Vectors/Experimental/VarianceGamma/VarianceGammaEngineVectorTests.cs."""
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
PROCESSES = [
    ("QuantLib's first process", {"s0": 6000.0, "dividendYield": 0.0, "riskFreeRate": 0.05, "sigma": 0.20, "nu": 0.05,
                                  "theta": -0.50}),
    ("QuantLib's second process", {"s0": 6000.0, "dividendYield": 0.02, "riskFreeRate": 0.05, "sigma": 0.15,
                                   "nu": 0.01, "theta": -0.50}),
    ("a short-dated skew", {"s0": 100.0, "dividendYield": 0.01, "riskFreeRate": 0.03, "sigma": 0.2, "nu": 0.1,
                            "theta": -0.1}),
    ("a heavy-tailed process", {"s0": 100.0, "dividendYield": 0.0, "riskFreeRate": 0.05, "sigma": 0.25, "nu": 0.3,
                                "theta": -0.2}),
]


def process(spec):
    dc = ql.Actual365Fixed()
    return ql.VarianceGammaProcess(
        ql.QuoteHandle(ql.SimpleQuote(spec["s0"])),
        ql.YieldTermStructureHandle(ql.FlatForward(R, spec["dividendYield"], dc)),
        ql.YieldTermStructureHandle(ql.FlatForward(R, spec["riskFreeRate"], dc)),
        spec["sigma"], spec["nu"], spec["theta"])


def option(spec):
    return ql.VanillaOption(ql.PlainVanillaPayoff(spec["optionType"], spec["strike"]),
                            ql.EuropeanExercise(R + spec["expiryDays"]))


def cases():
    st = v.settings(R)
    out = []
    for name, p in PROCESSES:
        days = [182, 365] + ([730] if p["nu"] >= 0.02 else [])
        specs = [{"optionType": t, "strike": p["s0"] * m, "expiryDays": d}
                 for d in days for t in (CALL, PUT) for m in (0.9, 1.0, 1.1)]
        analytic = ql.VarianceGammaEngine(process(p))
        fft = ql.FFTVarianceGammaEngine(process(p))
        npv, alone = [], []
        for s in specs:
            o = option(s)
            o.setPricingEngine(analytic)
            npv.append(v.attempt(o.NPV))
            o = option(s)
            o.setPricingEngine(ql.FFTVarianceGammaEngine(process(p)))
            alone.append(v.attempt(o.NPV))
        listed = [option(s) for s in specs]
        fft.precalculate(listed)
        in_list = []
        for o in listed:
            o.setPricingEngine(fft)
            in_list.append(v.attempt(o.NPV))
        out.append({"name": name, "inputs": {"settings": st, "process": p, "options": specs},
                    "expected": {"NPV": npv, "fftNPV": alone, "fftListNPV": in_list}})
        vg = process(p)
        out.append({"name": f"{name}: the process", "inputs": {"settings": st, "process": p},
                    "expected": {"x0": vg.x0(), "drift": v.attempt(lambda: vg.drift(0.5, p["s0"])),
                                 "diffusion": v.attempt(lambda: vg.diffusion(0.5, p["s0"]))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/variancegamma/variancegammaengine.hpp", cases)
