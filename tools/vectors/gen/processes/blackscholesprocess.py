# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GeneralizedBlackScholesProcess and its named processes, BlackScholesProcess,
BlackScholesMertonProcess, BlackProcess and GarmanKohlagenProcess (ql/processes/blackscholesprocess.hpp, .cpp), on
the evaluation date 15 October 2026.

Each process is built from a spec: its type, its spot, its dividend and risk-free curves (flat forwards, or linear zero
curves whose forward rates move) and its Black volatility (a constant volatility, a variance curve or a variance
surface, the three branches of the process's local volatility: a constant local volatility, a local volatility curve
and a local volatility surface). The outputs are its x0, its state variable, its curves' and volatility's reference
dates, and at 10 points (t, x, dt, dw): drift(t, x), diffusion(t, x), the local volatility at (t, x), expectation,
stdDeviation and variance over (t, x, dt), evolve(t, x, dt, dw) and apply(x, dx). On a surface the expectation is
QuantLib's "not implemented" refusal, and the moments are the Euler discretization's. A process given a local
volatility of its own (the constructor taking it, on a constant local volatility) is never strike-independent, so its
expectation is refused there too. BlackScholesProcess's dividend curve is QuantLib's FlatForward(0, NullCalendar(),
0.0, Actual365Fixed()), dated by the evaluation date: its reference date and its discount are written under two
evaluation dates. Then the refusal of a negative variance curve's local volatility is left out: QuantLib returns NaN
where nanoql refuses (tests/NanoQL.Tests/Divergences/LocalVolCurveRefusalTests.cs).

The specs, which the other equity generators share, are written by flat_spec, zero_spec and process_spec below and
built by curve and process: a curve is {"type": "FlatForward", "referenceDate", "forward", "dayCounter"} or {"type":
"ZeroCurve", "dates", "rates", "dayCounter"} (QuantLib's InterpolatedZeroCurve<Linear>, continuous annual rates), a
process {"type", "x0", "dividend" (none for BlackScholesProcess; the foreign curve for GarmanKohlagenProcess),
"riskFree", "volatility"}, the volatility a structure spec of
tools/vectors/gen/termstructures/volatility/equityfx/blackvoltermstructure.py. The wheel's calls, by QuantLib-SWIG
1.44's stochasticprocess.i: each process's constructor is the C++ one with its default discretization (Euler's) and
forceDiscretization false, on handles of the quote and structures; x0, drift, diffusion, expectation, stdDeviation,
variance, evolve, apply, stateVariable, dividendYield, riskFreeRate, blackVolatility and localVolatility are the C++
members. Read back by tests/NanoQL.Tests/Vectors/Processes/BlackScholesProcessVectorTests.cs."""
import importlib.util
import os
import random
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


bvts = _load("blackvoltermstructure", "termstructures/volatility/equityfx/blackvoltermstructure.py")
A365, A360 = bvts.A365, bvts.A360
R = bvts.EVALUATION_DATE


def flat_spec(reference, forward, dc=A365):
    return {"type": "FlatForward", "referenceDate": reference.serialNumber(), "forward": forward, "dayCounter": dc}


def zero_spec(reference, offsets, rates, dc=A365):
    return {"type": "ZeroCurve", "dates": [(reference + d).serialNumber() for d in offsets], "rates": list(rates),
            "dayCounter": dc}


def curve(spec):
    dc = bvts.day_counter(spec["dayCounter"])
    if spec["type"] == "FlatForward":
        return ql.FlatForward(ql.Date(spec["referenceDate"]), spec["forward"], dc)
    if spec["type"] == "ZeroCurve":
        return ql.ZeroCurve([ql.Date(d) for d in spec["dates"]], spec["rates"], dc)
    raise ValueError(f"unknown curve {spec['type']}")


def process_spec(kind, x0, risk_free, volatility, dividend=None):
    spec = {"type": kind, "x0": x0}
    if dividend is not None:
        spec["dividend"] = dividend
    spec.update({"riskFree": risk_free, "volatility": volatility})
    return spec


def process(spec):
    """The wheel's process for a spec, and the structures it holds (to keep them alive)."""
    x0 = ql.QuoteHandle(ql.SimpleQuote(spec["x0"]))
    rf = ql.YieldTermStructureHandle(curve(spec["riskFree"]))
    vol = ql.BlackVolTermStructureHandle(bvts.black_vol(spec["volatility"]))
    t = spec["type"]
    if t == "BlackScholesProcess":
        return ql.BlackScholesProcess(x0, rf, vol)
    if t == "BlackProcess":
        return ql.BlackProcess(x0, rf, vol)
    q = ql.YieldTermStructureHandle(curve(spec["dividend"]))
    if t == "BlackScholesMertonProcess":
        return ql.BlackScholesMertonProcess(x0, q, rf, vol)
    if t == "GarmanKohlagenProcess":
        return ql.GarmanKohlagenProcess(x0, q, rf, vol)
    if t == "GeneralizedBlackScholesProcess":
        return ql.GeneralizedBlackScholesProcess(x0, q, rf, vol)
    raise ValueError(f"unknown process {t}")


FLAT_Q = flat_spec(R, 0.02)
FLAT_R = flat_spec(R, 0.04)
ZERO_Q = zero_spec(R, [0, 182, 365, 1095, 3650], [0.015, 0.018, 0.02, 0.022, 0.025])
ZERO_R = zero_spec(R, [0, 91, 365, 730, 1825, 3650], [0.03, 0.032, 0.035, 0.038, 0.04, 0.042])
CONSTANT = bvts.vol_spec_constant(R, 0.22)
CURVE = bvts.vol_spec_curve(R, [91, 182, 365, 730, 1826], [0.25, 0.23, 0.22, 0.215, 0.21])
SURFACE = bvts.vol_spec_surface(R, [91, 365, 730, 1826], [70.0, 85.0, 100.0, 115.0, 130.0],
                                [[0.32, 0.30, 0.28, 0.27], [0.26, 0.25, 0.24, 0.235], [0.22, 0.215, 0.21, 0.205],
                                 [0.21, 0.205, 0.20, 0.198], [0.23, 0.22, 0.215, 0.21]])

PROCESSES = [
    ("Merton, flat, constant", process_spec("BlackScholesMertonProcess", 100.0, FLAT_R, CONSTANT, FLAT_Q)),
    ("Merton, zero curves, constant", process_spec("BlackScholesMertonProcess", 100.0, ZERO_R, CONSTANT, ZERO_Q)),
    ("Merton, zero curves, variance curve",
     process_spec("BlackScholesMertonProcess", 95.0, ZERO_R, CURVE, ZERO_Q)),
    ("Merton, zero curves, surface", process_spec("BlackScholesMertonProcess", 100.0, ZERO_R, SURFACE, ZERO_Q)),
    ("generalized, flat, surface", process_spec("GeneralizedBlackScholesProcess", 110.0, FLAT_R, SURFACE, FLAT_Q)),
    ("Black-Scholes, zero curve, constant", process_spec("BlackScholesProcess", 100.0, ZERO_R, CONSTANT)),
    ("Black-Scholes, flat, variance curve", process_spec("BlackScholesProcess", 50.0, FLAT_R, CURVE)),
    ("Black, zero curve, surface", process_spec("BlackProcess", 100.0, ZERO_R, SURFACE)),
    ("Black, flat, constant", process_spec("BlackProcess", 100.0, FLAT_R, CONSTANT)),
    ("Garman-Kohlhagen, zero curves, variance curve",
     process_spec("GarmanKohlagenProcess", 1.2, ZERO_R, bvts.vol_spec_curve(R, [182, 365, 730], [0.10, 0.11, 0.12]),
                  ZERO_Q)),
]


def points(rnd):
    out = []
    for _ in range(10):
        out.append([rnd.choice([0.0, rnd.uniform(0.0, 0.5), rnd.uniform(0.5, 6.0)]), rnd.uniform(60.0, 140.0),
                    rnd.choice([0.01, 0.25, 1.0, rnd.uniform(0.0, 2.0)]), rnd.uniform(-2.5, 2.5)])
    return out


def scaled(p, x0):
    """The points with the state scaled to the process's spot."""
    return [[t, x * x0 / 100.0, dt, dw] for t, x, dt, dw in p]


def cases():
    out = []
    st = v.settings(R)
    rnd = random.Random(v.SEED)
    for name, spec in PROCESSES:
        p = process(spec)
        pts = scaled(points(rnd), spec["x0"])
        lv = p.localVolatility()
        out.append({
            "name": name,
            "inputs": {"settings": st, "process": spec, "points": pts},
            "expected": v.enc({
                "x0": p.x0(),
                "stateVariable": p.stateVariable().value(),
                "riskFreeReferenceDate": p.riskFreeRate().referenceDate(),
                "dividendReferenceDate": p.dividendYield().referenceDate(),
                "blackVolatilityReferenceDate": p.blackVolatility().referenceDate(),
                "localVolatilityReferenceDate": lv.referenceDate(),
                "drift": [v.attempt(lambda: p.drift(t, x)) for t, x, _, _ in pts],
                "diffusion": [v.attempt(lambda: p.diffusion(t, x)) for t, x, _, _ in pts],
                "localVol": [v.attempt(lambda: lv.localVol(t, x, True)) for t, x, _, _ in pts],
                "expectation": [v.attempt(lambda: p.expectation(t, x, dt)) for t, x, dt, _ in pts],
                "stdDeviation": [v.attempt(lambda: p.stdDeviation(t, x, dt)) for t, x, dt, _ in pts],
                "variance": [v.attempt(lambda: p.variance(t, x, dt)) for t, x, dt, _ in pts],
                "evolve": [v.attempt(lambda: p.evolve(t, x, dt, dw)) for t, x, dt, dw in pts],
                "apply": [p.apply(x, dw * 0.1) for t, x, dt, dw in pts]})})

    # a process given its own local volatility: Euler's moments, and no exact expectation
    spec = process_spec("GeneralizedBlackScholesProcess", 100.0, ZERO_R, CONSTANT, ZERO_Q)
    local = {"referenceDate": R.serialNumber(), "volatility": 0.3, "dayCounter": A365}
    p = ql.GeneralizedBlackScholesProcess(
        ql.QuoteHandle(ql.SimpleQuote(100.0)), ql.YieldTermStructureHandle(curve(ZERO_Q)),
        ql.YieldTermStructureHandle(curve(ZERO_R)), ql.BlackVolTermStructureHandle(bvts.black_vol(CONSTANT)),
        ql.LocalVolTermStructureHandle(ql.LocalConstantVol(R, 0.3, ql.Actual365Fixed())))
    pts = points(rnd)
    out.append({
        "name": "generalized, its own local volatility",
        "inputs": {"settings": st, "process": spec, "localVolatility": local, "points": pts},
        "expected": v.enc({
            "drift": [v.attempt(lambda: p.drift(t, x)) for t, x, _, _ in pts],
            "diffusion": [v.attempt(lambda: p.diffusion(t, x)) for t, x, _, _ in pts],
            "expectation": [v.attempt(lambda: p.expectation(t, x, dt)) for t, x, dt, _ in pts],
            "stdDeviation": [v.attempt(lambda: p.stdDeviation(t, x, dt)) for t, x, dt, _ in pts],
            "variance": [v.attempt(lambda: p.variance(t, x, dt)) for t, x, dt, _ in pts],
            "evolve": [v.attempt(lambda: p.evolve(t, x, dt, dw)) for t, x, dt, dw in pts]})})

    # Black-Scholes's own dividend curve, dated by the evaluation date
    for evaluation in (R, ql.Date(30, ql.June, 2027)):
        st2 = v.settings(evaluation)
        spec = process_spec("BlackScholesProcess", 100.0, flat_spec(R, 0.04), CONSTANT)
        p = process(spec)
        out.append({
            "name": f"Black-Scholes's dividend curve on {evaluation.ISO()}",
            "inputs": {"settings": st2, "process": spec},
            "expected": v.enc({"dividendReferenceDate": p.dividendYield().referenceDate(),
                               "dividendDiscount": p.dividendYield().discount(evaluation + 365)})})
    v.settings(R)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/blackscholesprocess.hpp", cases)
