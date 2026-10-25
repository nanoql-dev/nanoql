# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AndreasenHugeVolatilityInterpl (ql/termstructures/volatility/equityfx/
andreasenhugevolatilityinterpl.hpp, .cpp), on the evaluation date 15 October 2026: six calibrations (an oil-market
smile of five strikes at four expiries by calls, by puts and by both, a flat 20% at three expiries by both, a single
option, and a smile on zero curves), across the three interpolations, on grids of 50 to 200 points: the maximum date,
the strike range, the forward at a grid of times, the calibration error (min, max, average), and the option prices of
both types and the local volatility at a grid of times (before, at and between the expiries, and past the last) and
strikes.

**The stability filter** (the S7 plan's S7R6, the S-van plan's VD17): every case is calibrated again with all its
volatilities moved one ulp, up and then down, and with the spot moved one ulp each way, and an output is written only
where every move leaves it within 1e-10 x max(|a|, |b|, 1), the vectors' own tolerance, since the calibration
stops at its end criteria's 1e-10; the generator prints "dropped n of m outputs".

Left out (the S-van plan's §2.10): the call-put calibration's error, which QuantLib picks by a grid point where nanoql
picks it by strike against the forward (VR4 (xvii); tests/NanoQL.Tests/Divergences/AndreasenHugeCallPutErrorTests.cs);
and curves on another clock, which nanoql reads at the instants the risk-free curve's times denote (VR4 (xxiii)): every
structure here is dated 15 October 2026 on Actual/365 (Fixed).

The specs, which the two adapters' generators share, are a calibration {"spot", "riskFree", "dividend" (the curves
tools/vectors/gen/processes/blackscholesprocess.py's specs), "options" ([optionType, strike, expiry offset in days,
volatility] each), "interpolationType", "calibrationType" (QuantLib's integers), "nGridPoints", "minStrike",
"maxStrike" (null for QuantLib's defaults)}. The wheel's calls, by QuantLib-SWIG 1.44: AndreasenHugeVolatilityInterpl(
calibrationSet, spot, rTS, qTS, interpolationType, calibrationType, nGridPoints, minStrike, maxStrike) with its default
Levenberg-Marquardt and end criteria, and its maxDate, minStrike, maxStrike, fwd, calibrationError, optionPrice and
localVol. Read back by tests/NanoQL.Tests/Vectors/TermStructures/AndreasenHugeVolatilityInterplVectorTests.cs."""
import importlib.util
import math
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
R = bsp.R
AH = ql.AndreasenHugeVolatilityInterpl
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
# the vectors' own tolerance: a one-ulp move that shifts an output by less, the assertion holds
TOLERANCE = 1e-10


def oil_options():
    """Borovkova and Permana's oil smile (QuantLib's test-suite) at five strikes and four expiries."""
    r, q, spot = 0.025, 0.085, 100.0
    out = []
    for strike in (50.0, 75.0, 100.0, 125.0, 150.0):
        for days in (30, 91, 182, 365):
            t = days / 365.0
            fwd = spot * math.exp((r - q) * t)
            mn = math.log(fwd / strike) / math.sqrt(t)
            vol = 0.35 + 0.03 * mn + 0.005 * mn * mn - 0.02 * t - 0.005 * mn * t
            out.append([CALL if strike >= fwd else PUT, strike, days, vol])
    return out


def calibration_spec(options, rf, q, interpolation, calibration, n, spot=100.0, min_strike=None, max_strike=None):
    return {"spot": spot, "riskFree": rf, "dividend": q, "options": options, "interpolationType": int(interpolation),
            "calibrationType": int(calibration), "nGridPoints": n, "minStrike": min_strike, "maxStrike": max_strike}


OIL_R, OIL_Q = bsp.flat_spec(R, 0.025), bsp.flat_spec(R, 0.085)
CASES = [
    ("oil smile, calls, cubic spline", calibration_spec(oil_options(), OIL_R, OIL_Q, AH.CubicSpline, AH.Call, 150)),
    ("oil smile, puts, linear", calibration_spec(oil_options(), OIL_R, OIL_Q, AH.Linear, AH.Put, 120)),
    ("oil smile, calls and puts, piecewise constant",
     calibration_spec(oil_options(), OIL_R, OIL_Q, AH.PiecewiseConstant, AH.CallPut, 100)),
    ("flat 20%, calls and puts", calibration_spec(
        [[CALL if k >= 100.0 else PUT, k, d, 0.2] for k in (80.0, 100.0, 120.0) for d in (91, 365, 730)],
        bsp.flat_spec(R, 0.02), bsp.flat_spec(R, 0.0), AH.CubicSpline, AH.CallPut, 200)),
    ("a single option", calibration_spec([[CALL, 10.0, 365, 0.3]], bsp.flat_spec(R, 0.025), bsp.flat_spec(R, 0.085),
                                         AH.CubicSpline, AH.Put, 50, spot=10.0)),
    ("a smile on zero curves, bounded grid", calibration_spec(
        [[CALL if k >= 100.0 else PUT, k, d, 0.22 + 0.15 * ((k - 100.0) / 100.0) ** 2 - 0.01 * d / 365.0]
         for k in (70.0, 90.0, 100.0, 110.0, 130.0) for d in (60, 182, 547)],
        bsp.ZERO_R, bsp.ZERO_Q, AH.CubicSpline, AH.Call, 150, min_strike=20.0, max_strike=400.0)),
]
TIMES = [0.01, 30 / 365.0, 0.15, 0.25, 0.4986301369863014, 0.75, 1.0, 1.5, 2.0, 2.5]
RELATIVE = [0.4, 0.7, 0.9, 1.0, 1.1, 1.35, 2.0]


def build(spec, vol_move=None, spot_move=None):
    """The wheel's interpolation for a spec, its volatilities or spot moved one ulp where asked (S7R6)."""
    cs = ql.CalibrationSet()
    for option_type, strike, days, vol in spec["options"]:
        if vol_move is not None:
            vol = math.nextafter(vol, vol_move)
        o = ql.VanillaOption(ql.PlainVanillaPayoff(option_type, strike), ql.EuropeanExercise(R + days))
        cs.push_back((o, ql.SimpleQuote(vol)))
    spot = spec["spot"] if spot_move is None else math.nextafter(spec["spot"], spot_move)
    min_strike = v.NULL_REAL if spec["minStrike"] is None else spec["minStrike"]
    max_strike = v.NULL_REAL if spec["maxStrike"] is None else spec["maxStrike"]
    r = ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"]))
    q = ql.YieldTermStructureHandle(bsp.curve(spec["dividend"]))
    return ql.AndreasenHugeVolatilityInterpl(cs, ql.QuoteHandle(ql.SimpleQuote(spot)), r, q,
                                             spec["interpolationType"], spec["calibrationType"],
                                             spec["nGridPoints"], min_strike, max_strike)


def moved(spec):
    return [build(spec, vol_move=math.inf), build(spec, vol_move=-math.inf), build(spec, spot_move=math.inf),
            build(spec, spot_move=-math.inf)]


def within(a, b):
    if isinstance(a, dict) or isinstance(b, dict):
        return a == b
    return abs(a - b) <= TOLERANCE * max(abs(a), abs(b), 1.0)


class Tally:
    dropped = total = 0


def filtered(base, others, f):
    """f's value on the base interpolation, or None where a moved one leaves it outside the filter's measure."""
    Tally.total += 1
    a = v.attempt(lambda: f(base))
    if all(within(a, v.attempt(lambda o=o: f(o))) for o in others):
        return a
    Tally.dropped += 1
    return None


def points(spec):
    return [[t, spec["spot"] * r] for t in TIMES for r in RELATIVE]


def cases():
    out = []
    st = v.settings(R)
    for name, spec in CASES:
        base, others = build(spec), moved(spec)
        pts = points(spec)
        call = [filtered(base, others, lambda a, t=t, k=k: a.optionPrice(t, k, ql.Option.Call)) for t, k in pts]
        put = [filtered(base, others, lambda a, t=t, k=k: a.optionPrice(t, k, ql.Option.Put)) for t, k in pts]
        local = [filtered(base, others, lambda a, t=t, k=k: a.localVol(t, k)) for t, k in pts]
        expected = {"maxDate": v.enc(base.maxDate()), "minStrike": base.minStrike(), "maxStrike": base.maxStrike(),
                    "fwd": [base.fwd(t) for t in TIMES]}
        if spec["calibrationType"] != int(AH.CallPut):
            error = [filtered(base, others, lambda a, i=i: getattr(a.calibrationError(), i)())
                     for i in ("first", "second", "third")]
            expected["calibrationError"] = error
        keep = [i for i in range(len(pts)) if call[i] is not None and put[i] is not None and local[i] is not None]
        expected.update({"callPrice": [call[i] for i in keep], "putPrice": [put[i] for i in keep],
                         "localVol": [local[i] for i in keep]})
        if "calibrationError" in expected and None in expected["calibrationError"]:
            del expected["calibrationError"]
        out.append({"name": name, "inputs": {"settings": st, "calibration": spec, "times": TIMES,
                                             "points": [pts[i] for i in keep]},
                    "expected": expected})
    print(f"dropped {Tally.dropped} of {Tally.total} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/andreasenhugevolatilityinterpl.hpp", cases)
