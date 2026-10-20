# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackVolTermStructure, BlackVolatilityTermStructure and BlackVarianceTermStructure
(ql/termstructures/volatility/equityfx/blackvoltermstructure.hpp, .cpp): the members the bases define, through a
constant volatility, a variance curve and a variance surface on the evaluation date 15 October 2026.

For each structure, at 12 (time, strike) points: blackVol and blackVariance by time and by date (the date
reference date + round(365 t) days), extrapolated; blackForwardVol and blackForwardVariance between two times and two
dates, the equal-times branches (at 0 and at a positive time) among them; the smile section at four times, its
exercise time, minimum and maximum strikes, at-the-money level (null, QuantLib's default) and its volatility and
variance at three strikes, and the smile section by date (the date of each time) and its volatility at 100. Then
the refusals: a forward volatility between reversed times and reversed dates, a
negative time, and a time past the structure's maximum without extrapolation; and a forward variance over a decreasing
variance, which a curve built without the forced monotone variance gives.

The structure specs, which the other equity generators share, are written by vol_spec_constant, vol_spec_curve and
vol_spec_surface below and built by black_vol: {"type": "BlackConstantVol", "referenceDate" | "settlementDays",
"calendar", "volatility", "dayCounter"}, {"type": "BlackVarianceCurve", "referenceDate", "dates", "volatilities",
"dayCounter", "forceMonotoneVariance", "timeExtrapolation", "interpolation"} and {"type": "BlackVarianceSurface",
"referenceDate", "calendar", "dates", "strikes", "volatilities" (one row per strike), "dayCounter", "lower", "upper",
"interpolation"}, an interpolation null for the default or the wheel's setInterpolation name ("cubic" for the curve,
QuantLib's Cubic with its defaults; "bicubic" for the surface). A day counter is {"type"} (with "convention" for
ActualActual), a calendar {"type"}. The wheel's calls, by QuantLib-SWIG 1.44's volatilities.i: each constructor is the
C++ one, setInterpolation(name) calls the C++ template with the named interpolator, and blackVol, blackVariance,
blackForwardVol, blackForwardVariance and smileSection are the base's members. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/BlackVolTermStructureVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}
NULL_CALENDAR = {"type": "NullCalendar"}
TARGET = {"type": "TARGET"}
EVALUATION_DATE = ql.Date(15, ql.October, 2026)


def day_counter(spec):
    t = spec["type"]
    if t == "Actual365Fixed":
        return ql.Actual365Fixed()
    if t == "Actual360":
        return ql.Actual360()
    if t == "ActualActual":
        return ql.ActualActual(spec["convention"])
    raise ValueError(f"unknown day counter {t}")


def calendar(spec):
    t = spec["type"]
    if t == "NullCalendar":
        return ql.NullCalendar()
    if t == "TARGET":
        return ql.TARGET()
    raise ValueError(f"unknown calendar {t}")


def vol_spec_constant(reference, vol, dc=A365, cal=NULL_CALENDAR, settlement_days=None):
    spec = {"type": "BlackConstantVol"}
    if settlement_days is None:
        spec["referenceDate"] = reference.serialNumber()
    else:
        spec["settlementDays"] = settlement_days
    spec.update({"calendar": cal, "volatility": vol, "dayCounter": dc})
    return spec


def vol_spec_curve(reference, offsets, vols, dc=A365, force=True,
                   extrapolation=ql.BlackVolTimeExtrapolation.FlatVolatility, interpolation=None):
    return {"type": "BlackVarianceCurve", "referenceDate": reference.serialNumber(),
            "dates": [(reference + d).serialNumber() for d in offsets], "volatilities": list(vols), "dayCounter": dc,
            "forceMonotoneVariance": force, "timeExtrapolation": int(extrapolation), "interpolation": interpolation}


def vol_spec_surface(reference, offsets, strikes, vols, dc=A365, cal=TARGET,
                     lower=ql.BlackVarianceSurface.InterpolatorDefaultExtrapolation,
                     upper=ql.BlackVarianceSurface.InterpolatorDefaultExtrapolation, interpolation=None):
    return {"type": "BlackVarianceSurface", "referenceDate": reference.serialNumber(), "calendar": cal,
            "dates": [(reference + d).serialNumber() for d in offsets], "strikes": list(strikes),
            "volatilities": [list(row) for row in vols], "dayCounter": dc, "lower": int(lower), "upper": int(upper),
            "interpolation": interpolation}


def black_vol(spec):
    """The wheel's structure for a spec; a settlement-days spec reads the evaluation date set beforehand."""
    t = spec["type"]
    dc = day_counter(spec["dayCounter"])
    if t == "BlackConstantVol":
        if "settlementDays" in spec:
            return ql.BlackConstantVol(spec["settlementDays"], calendar(spec["calendar"]), spec["volatility"], dc)
        return ql.BlackConstantVol(ql.Date(spec["referenceDate"]), calendar(spec["calendar"]), spec["volatility"],
                                   dc)
    if t == "BlackVarianceCurve":
        s = ql.BlackVarianceCurve(ql.Date(spec["referenceDate"]), [ql.Date(d) for d in spec["dates"]],
                                  spec["volatilities"], dc, spec["forceMonotoneVariance"],
                                  spec["timeExtrapolation"])
        if spec["interpolation"] is not None:
            s.setInterpolation(spec["interpolation"])
        return s
    if t == "BlackVarianceSurface":
        s = ql.BlackVarianceSurface(ql.Date(spec["referenceDate"]), calendar(spec["calendar"]),
                                    [ql.Date(d) for d in spec["dates"]], spec["strikes"],
                                    ql.Matrix(spec["volatilities"]), dc, spec["lower"], spec["upper"])
        if spec["interpolation"] is not None:
            s.setInterpolation(spec["interpolation"])
        return s
    raise ValueError(f"unknown structure {t}")


R = EVALUATION_DATE
STRUCTURES = [
    ("constant 20%", vol_spec_constant(R, 0.20)),
    ("constant 35%, Actual/360", vol_spec_constant(R, 0.35, A360)),
    ("variance curve", vol_spec_curve(R, [91, 182, 365, 730, 1826], [0.25, 0.23, 0.22, 0.215, 0.21])),
    ("variance curve, cubic", vol_spec_curve(R, [91, 182, 365, 730, 1826], [0.25, 0.23, 0.22, 0.215, 0.21],
                                             interpolation="cubic")),
    ("variance surface", vol_spec_surface(R, [91, 365, 730], [80.0, 100.0, 120.0],
                                          [[0.30, 0.28, 0.27], [0.22, 0.21, 0.205], [0.25, 0.235, 0.23]])),
    ("variance surface, bicubic, constant strike extrapolation",
     vol_spec_surface(R, [91, 182, 365, 730], [70.0, 90.0, 100.0, 110.0, 130.0],
                      [[0.34, 0.32, 0.31, 0.30], [0.27, 0.26, 0.255, 0.25], [0.24, 0.235, 0.23, 0.228],
                       [0.23, 0.225, 0.222, 0.22], [0.26, 0.25, 0.245, 0.24]],
                      lower=ql.BlackVarianceSurface.ConstantExtrapolation,
                      upper=ql.BlackVarianceSurface.ConstantExtrapolation, interpolation="bicubic")),
]
POINTS = [(0.0, 100.0), (0.1, 95.0), (0.25, 100.0), (0.5, 105.0), (0.75, 90.0), (1.0, 100.0), (1.5, 110.0),
          (2.0, 85.0), (3.0, 100.0), (4.5, 120.0), (6.0, 100.0), (0.999, 100.0)]
FORWARDS = [(0.0, 0.0), (0.5, 0.5), (0.25, 1.0), (1.0, 3.0), (0.0, 2.0)]
SMILE_TIMES = [0.25, 1.0, 1.5, 4.0]
SMILE_STRIKES = [85.0, 100.0, 115.0]


def date_at(t):
    return R + int(round(365 * t))


def cases():
    out = []
    st = v.settings(EVALUATION_DATE)
    for name, spec in STRUCTURES:
        s = black_vol(spec)
        s.enableExtrapolation()
        out.append({
            "name": name,
            "inputs": {"settings": st, "structure": spec, "points": [list(p) for p in POINTS],
                       "forwards": [list(f) for f in FORWARDS], "smileTimes": SMILE_TIMES,
                       "smileStrikes": SMILE_STRIKES},
            "expected": v.enc({
                "blackVol": [s.blackVol(t, k, True) for t, k in POINTS],
                "blackVariance": [s.blackVariance(t, k, True) for t, k in POINTS],
                "blackVolDate": [s.blackVol(date_at(t), k, True) for t, k in POINTS],
                "blackVarianceDate": [s.blackVariance(date_at(t), k, True) for t, k in POINTS],
                "blackForwardVol": [v.attempt(lambda: s.blackForwardVol(t1, t2, 100.0, True)) for t1, t2 in FORWARDS],
                "blackForwardVariance": [v.attempt(lambda: s.blackForwardVariance(t1, t2, 100.0, True))
                                         for t1, t2 in FORWARDS],
                "blackForwardVolDate": [v.attempt(lambda: s.blackForwardVol(date_at(t1), date_at(t2), 100.0, True))
                                        for t1, t2 in FORWARDS],
                "blackForwardVarianceDate": [
                    v.attempt(lambda: s.blackForwardVariance(date_at(t1), date_at(t2), 100.0, True))
                    for t1, t2 in FORWARDS],
                "smileExerciseTime": [s.smileSection(t, True).exerciseTime() for t in SMILE_TIMES],
                "smileMinStrike": [s.smileSection(t, True).minStrike() for t in SMILE_TIMES],
                "smileMaxStrike": [s.smileSection(t, True).maxStrike() for t in SMILE_TIMES],
                "smileAtmLevel": [s.smileSection(t, True).atmLevel() for t in SMILE_TIMES],
                "smileVolatility": [[s.smileSection(t, True).volatility(k) for k in SMILE_STRIKES]
                                    for t in SMILE_TIMES],
                "smileVariance": [[s.smileSection(t, True).variance(k) for k in SMILE_STRIKES]
                                  for t in SMILE_TIMES],
                "smileVolatilityDate": [s.smileSection(date_at(t), True).volatility(100.0) for t in SMILE_TIMES],
            })})
    # the refusals
    s = black_vol(STRUCTURES[2][1])
    out.append({
        "name": "refusals",
        "inputs": {"settings": st, "structure": STRUCTURES[2][1]},
        "expected": {
            "reversedTimes": v.attempt(lambda: s.blackForwardVol(1.0, 0.5, 100.0)),
            "reversedDates": v.attempt(lambda: s.blackForwardVol(date_at(1.0), date_at(0.5), 100.0)),
            "negativeTime": v.attempt(lambda: s.blackVol(-0.1, 100.0)),
            "pastMaxTime": v.attempt(lambda: s.blackVol(10.0, 100.0)),
            "pastMaxDate": v.attempt(lambda: s.blackVol(date_at(10.0), 100.0))}})
    decreasing = vol_spec_curve(R, [182, 365], [0.40, 0.20], force=False)
    d = black_vol(decreasing)
    out.append({
        "name": "a decreasing variance",
        "inputs": {"settings": st, "structure": decreasing},
        "expected": {
            "blackVariance": [d.blackVariance(0.5, 100.0), d.blackVariance(1.0, 100.0)],
            "blackForwardVariance": v.attempt(lambda: d.blackForwardVariance(0.5, 1.0, 100.0)),
            "blackForwardVol": v.attempt(lambda: d.blackForwardVol(0.5, 1.0, 100.0)),
            "blackForwardVolEqual": v.attempt(lambda: d.blackForwardVol(0.75, 0.75, 100.0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/blackvoltermstructure.hpp", cases)
