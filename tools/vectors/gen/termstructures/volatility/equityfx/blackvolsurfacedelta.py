# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackVolatilitySurfaceDelta (ql/termstructures/volatility/equityfx/blackvolsurfacedelta.hpp,
.cpp), on the evaluation date 15 October 2026.

- Surfaces: twelve, over flat domestic and foreign curves on the surface's day counter from its reference date: one to
  three put and call deltas, with and without an at-the-money quote; each delta convention (spot, forward and their
  premium-adjusted forms) and each at-the-money convention QuantLib's calculator takes (spot, forward, delta-neutral,
  vega-max, gamma-max, put-call 50); each smile interpolation (linear, natural cubic, financial cubic, cubic spline),
  with and without flat strike extrapolation; each time extrapolation (flat volatility, linear variance, the
  interpolator's); a switch tenor of a year to long-term forward conventions; Actual/365 (Fixed) and Actual/Actual
  (ISDA). Outputs: the dates and maximum date, the strike range, the forward (atmLevel) at a grid of times, and the
  volatility and variance at the times and a grid of strikes, 0 and QuantLib's null among them (its at-the-money
  reads), extrapolating, each recorded as QuantLib's throw where its delta calculator refuses a strike.
- Refusals: one date, a date on the reference date, dates out of order, no deltas, and a matrix whose columns or rows
  don't match the deltas or dates.

Left out (the S-van plan's §2.10): curves on another clock, which nanoql reads at the instants the surface's times
denote and QuantLib at those times (VR4 (xxi); tests/NanoQL.Tests/Divergences/DeltaSurfaceMismatchedTimesTests.cs); and
the smile section's prices, which QuantLib's section takes on the at-the-money volatility as its forward (VR4 (xxxii);
DeltaSurfaceSmileSectionTests.cs); the wheel binds neither blackVolSmile nor the section. The wheel's call, by
QuantLib-SWIG 1.44: BlackVolatilitySurfaceDelta(referenceDate, dates, putDeltas, callDeltas, hasAtm, matrix,
dayCounter, calendar, spot, domesticTS, foreignTS, dt, at, atmDeltaType, interpolationMethod, flatStrikeExtrapolation,
timeExtrapolationType, switchTenor, ltdt, ltat, longTermAtmDeltaType); its typemap refuses an explicit optional delta
type (argument 14, "std::optional< DeltaVolQuote::DeltaType >"), so the cases leave both unset: QuantLib's defaults, the
delta type and the long-term delta type. Read back by tests/NanoQL.Tests/Vectors/TermStructures/BlackVolSurfaceDeltaVectorTests.cs."""
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


bvts = _load("blackvoltermstructure", "termstructures/volatility/equityfx/blackvoltermstructure.py")
R = bvts.EVALUATION_DATE
A365, ACT_ISDA = bvts.A365, bvts.ACT_ISDA
TARGET = {"type": "TARGET"}
DQ = ql.DeltaVolQuote
SPOT, FWD, PA_SPOT, PA_FWD = int(DQ.Spot), int(DQ.Fwd), int(DQ.PaSpot), int(DQ.PaFwd)
ATM_SPOT, ATM_FWD, ATM_DN, ATM_VEGA, ATM_GAMMA, ATM_PC50 = (int(DQ.AtmSpot), int(DQ.AtmFwd), int(DQ.AtmDeltaNeutral),
                                                           int(DQ.AtmVegaMax), int(DQ.AtmGammaMax), int(DQ.AtmPutCall50))
B = ql.BlackVolatilitySurfaceDelta
LINEAR, NATURAL, FINANCIAL, SPLINE = (int(B.SmileInterpolationMethod_Linear), int(B.SmileInterpolationMethod_NaturalCubic),
                                      int(B.SmileInterpolationMethod_FinancialCubic),
                                      int(B.SmileInterpolationMethod_CubicSpline))
FLAT_VOL, LINEAR_VAR, USE_INTERP = (int(ql.BlackVolTimeExtrapolation.FlatVolatility),
                                    int(ql.BlackVolTimeExtrapolation.LinearVariance),
                                    int(ql.BlackVolTimeExtrapolation.UseInterpolator))

OFFSETS = [30, 91, 182, 365, 730]
ROWS = {  # one row per date: put deltas (most out of the money first), the at-the-money, then call deltas
    (1, True, 1): [[0.15, 0.13, 0.135], [0.14, 0.11, 0.125], [0.135, 0.105, 0.122], [0.13, 0.10, 0.12],
                   [0.125, 0.095, 0.115]],
    (1, False, 1): [[0.15, 0.135], [0.14, 0.125], [0.135, 0.122], [0.13, 0.12], [0.125, 0.115]],
    (2, True, 2): [[0.19, 0.16, 0.13, 0.15, 0.175], [0.18, 0.15, 0.12, 0.14, 0.165], [0.17, 0.145, 0.115, 0.135, 0.16],
                   [0.165, 0.14, 0.11, 0.13, 0.155], [0.16, 0.135, 0.105, 0.125, 0.15]],
    (3, True, 3): [[0.24, 0.20, 0.17, 0.15, 0.165, 0.19, 0.23], [0.23, 0.19, 0.16, 0.14, 0.155, 0.18, 0.22],
                   [0.22, 0.185, 0.155, 0.135, 0.15, 0.175, 0.21], [0.21, 0.18, 0.15, 0.13, 0.145, 0.17, 0.20],
                   [0.20, 0.175, 0.145, 0.125, 0.14, 0.165, 0.19]],
}
DELTAS = {1: ([-0.25], [0.25]), 2: ([-0.10, -0.25], [0.25, 0.10]), 3: ([-0.05, -0.10, -0.25], [0.25, 0.10, 0.05])}


def spec(shape, dc=A365, dt=SPOT, at=ATM_DN, atm_dt=None, method=LINEAR, flat=False, time_ext=FLAT_VOL, switch=None,
         lt_dt=FWD, lt_at=ATM_DN, lt_atm_dt=None, spot=1.18, rd=0.02, rf=0.035):
    puts, calls = DELTAS[shape[0]]
    return {"offsets": OFFSETS, "putDeltas": puts, "callDeltas": calls, "hasAtm": shape[1], "vols": ROWS[shape],
            "dayCounter": dc, "calendar": TARGET, "spot": spot, "domesticRate": rd, "foreignRate": rf,
            "deltaType": dt, "atmType": at, "atmDeltaType": atm_dt, "interpolationMethod": method,
            "flatStrikeExtrapolation": flat, "timeExtrapolationType": time_ext, "switchTenor": switch,
            "longTermDeltaType": lt_dt, "longTermAtmType": lt_at, "longTermAtmDeltaType": lt_atm_dt}


SURFACES = [
    ("one by one, delta-neutral, linear", spec((1, True, 1))),
    ("one by one, no at-the-money quote", spec((1, False, 1))),
    ("two by two, forward deltas, forward at-the-money", spec((2, True, 2), dt=FWD, at=ATM_FWD)),
    ("two by two, premium-adjusted spot, vega-max", spec((2, True, 2), dt=PA_SPOT, at=ATM_VEGA)),
    ("three by three, premium-adjusted forward, gamma-max", spec((3, True, 3), dt=PA_FWD, at=ATM_GAMMA)),
    ("two by two, forward deltas, put-call 50", spec((2, True, 2), dt=FWD, at=ATM_PC50)),
    ("two by two, natural cubic, flat in strike", spec((2, True, 2), method=NATURAL, flat=True)),
    ("two by two, financial cubic, linear variance", spec((2, True, 2), method=FINANCIAL, time_ext=LINEAR_VAR)),
    ("three by three, cubic spline, the interpolator's extrapolation",
     spec((3, True, 3), method=SPLINE, time_ext=USE_INTERP)),
    ("a switch at a year to forward conventions", spec((2, True, 2), switch=[1, int(ql.Years)], lt_dt=FWD,
                                                        lt_at=ATM_FWD)),
    ("Actual/Actual (ISDA), at-the-money spot", spec((1, True, 1), dc=ACT_ISDA, at=ATM_SPOT)),
    ("a low spot and steep curves", spec((2, True, 2), spot=0.62, rd=0.06, rf=-0.01)),
]
TIMES = [0.01, 0.05, 0.25, 0.4986301369863014, 0.75, 1.0, 1.5, 2.0, 2.5, 4.0]
STRIKES = [0.0, None, 0.9, 1.0, 1.1, 1.15, 1.18, 1.2, 1.25, 1.35, 1.6]


def make(s, extrapolate):
    dc = bvts.day_counter(s["dayCounter"])
    dates = [R + n for n in s["offsets"]]
    m = ql.Matrix(len(s["vols"]), len(s["vols"][0]))
    for i, row in enumerate(s["vols"]):
        for j, x in enumerate(row):
            m[i][j] = x
    kwargs = {"dt": s["deltaType"], "at": s["atmType"], "interpolationMethod": s["interpolationMethod"],
              "flatStrikeExtrapolation": s["flatStrikeExtrapolation"],
              "timeExtrapolationType": s["timeExtrapolationType"], "ltdt": s["longTermDeltaType"],
              "ltat": s["longTermAtmType"]}
    if s["atmDeltaType"] is not None:
        kwargs["atmDeltaType"] = s["atmDeltaType"]
    if s["switchTenor"] is not None:
        kwargs["switchTenor"] = ql.Period(*s["switchTenor"])
    if s["longTermAtmDeltaType"] is not None:
        kwargs["longTermAtmDeltaType"] = s["longTermAtmDeltaType"]
    surface = ql.BlackVolatilitySurfaceDelta(
        R, dates, s["putDeltas"], s["callDeltas"], s["hasAtm"], m, dc, bvts.calendar(s["calendar"]),
        ql.QuoteHandle(ql.SimpleQuote(s["spot"])),
        ql.YieldTermStructureHandle(ql.FlatForward(R, s["domesticRate"], dc)),
        ql.YieldTermStructureHandle(ql.FlatForward(R, s["foreignRate"], dc)), **kwargs)
    if extrapolate:
        surface.enableExtrapolation()
    return surface


def strike(k):
    return v.NULL_REAL if k is None else k


def cases():
    out = []
    st = v.settings(R)
    for name, s in SURFACES:
        surface = make(s, True)
        out.append({"name": name,
                    "inputs": {"settings": st, "surface": s, "times": TIMES, "strikes": STRIKES},
                    "expected": v.enc({
                        "dates": [R + n for n in s["offsets"]],
                        "maxDate": surface.maxDate(),
                        "minStrike": surface.minStrike(),
                        "maxStrike": surface.maxStrike(),
                        "atmLevel": [surface.atmLevel(t) for t in TIMES],
                        "blackVol": [[v.attempt(lambda t=t, k=k: surface.blackVol(t, strike(k), True)) for k in STRIKES]
                                     for t in TIMES],
                        "blackVariance": [[v.attempt(lambda t=t, k=k: surface.blackVariance(t, strike(k), True))
                                           for k in STRIKES] for t in TIMES]})})
    base = spec((1, True, 1))
    refusals = [
        ("one date", dict(base, offsets=[91], vols=[base["vols"][0]])),
        ("a date on the reference date", dict(base, offsets=[0, 91, 182, 365, 730])),
        ("dates out of order", dict(base, offsets=[91, 30, 182, 365, 730])),
        ("no deltas", dict(base, putDeltas=[], callDeltas=[], hasAtm=False, vols=[[] for _ in OFFSETS])),
        ("columns don't match the deltas", dict(base, vols=[row[:2] for row in base["vols"]])),
        ("rows don't match the dates", dict(base, vols=base["vols"][:4])),
    ]
    for name, s in refusals:
        out.append({"name": name, "inputs": {"settings": st, "surface": s},
                    "expected": {"build": v.attempt(lambda s=s: make(s, False).maxDate())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/blackvolsurfacedelta.hpp", cases)
