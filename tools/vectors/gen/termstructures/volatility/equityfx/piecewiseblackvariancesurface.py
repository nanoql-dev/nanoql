# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PiecewiseBlackVarianceSurface (ql/termstructures/volatility/equityfx/
piecewiseblackvariancesurface.hpp, .cpp), on the evaluation date 15 October 2026.

- Surfaces: seven, from flat sections at three tenors; linearly interpolated sections on ragged strike grids, with and
  without the surface's extrapolation; SABR sections at four tenors; one section; makeFromGrid on a 5 x 4 grid; and
  surfaces on Actual/360 and Actual/Actual (ISDA) over sections on Actual/365 (Fixed). Outputs: the maximum date and
  strike range, and at a grid of times (0, before the first tenor, at and between the tenors, past the last) and
  strikes (inside and outside the sections' grids) the variance and volatility, extrapolating in time, each recorded
  as QuantLib's throw where a strike is outside a section's grid and the surface doesn't extrapolate; and the smile
  section's volatility at the strikes at each tenor's time and between them.
- Refusals: no dates, dates and sections in different numbers, a first date on the reference date, dates out of order
  or repeated, and makeFromGrid's matrix not matching its strikes or dates.

The section specs are tools/vectors/gen/termstructures/volatility/flatsmilesection.py's, interpolatedsmilesection.py's
and sabrsmilesection.py's. The wheel's calls, by QuantLib-SWIG 1.44: PiecewiseBlackVarianceSurface(referenceDate, dates,
smileSections, dayCounter), makeFromGrid(referenceDate, dates, strikes, matrix, dayCounter) and enableExtrapolation().
nanoql's EnableExtrapolation returns a copy, which the test reads. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/PiecewiseBlackVarianceSurfaceVectorTests.cs."""
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


bvts = _load("blackvoltermstructure", "termstructures/volatility/equityfx/blackvoltermstructure.py")
flat = _load("flatsmilesection", "termstructures/volatility/flatsmilesection.py")
interp = _load("interpolatedsmilesection", "termstructures/volatility/interpolatedsmilesection.py")
sabr = _load("sabrsmilesection", "termstructures/volatility/sabrsmilesection.py")

R = bvts.EVALUATION_DATE
A365, A360, ACT_ISDA = bvts.A365, bvts.A360, bvts.ACT_ISDA
ql_a365 = ql.Actual365Fixed()


def linear(offset, strikes, vols):
    d = R + offset
    t = ql_a365.yearFraction(R, d)
    return interp.section("LinearInterpolatedSmileSection", "date", d, strikes, interp.std_devs(vols, t), 100.0, A365,
                          reference=R)


def sabr_at(offset, params):
    t = ql_a365.yearFraction(R, R + offset)
    return sabr.time_spec(t, 100.0 * math.exp(0.02 * t), params)


def make_section(spec):
    if spec["type"] == "FlatSmileSection":
        return flat.make_section(spec)
    if spec["type"] == "SabrSmileSection":
        return sabr.make_section(spec)
    return interp.make_section(spec)


SABR = [0.3, 0.7, 0.4, -0.4]
SURFACES = [
    ("flat sections", A365, [91, 182, 365], [flat.date_spec(R + n, R, s, A365) for n, s in
                                             [(91, 0.20), (182, 0.25), (365, 0.30)]], False),
    ("ragged linear sections", A365, [182, 365],
     [linear(182, [80.0, 100.0, 120.0], [0.30, 0.20, 0.18]), linear(365, [70.0, 90.0, 110.0, 130.0],
                                                                       [0.35, 0.25, 0.22, 0.20])], False),
    ("ragged linear sections, extrapolating", A365, [182, 365],
     [linear(182, [80.0, 100.0, 120.0], [0.30, 0.20, 0.18]), linear(365, [70.0, 90.0, 110.0, 130.0],
                                                                       [0.35, 0.25, 0.22, 0.20])], True),
    ("SABR sections", A365, [91, 182, 365, 730], [sabr_at(n, SABR) for n in [91, 182, 365, 730]], True),
    ("one section", A365, [182], [flat.date_spec(R + 182, R, 0.22, A365)], True),
    ("Actual/360 over Actual/365 sections", A360, [91, 365],
     [linear(91, [80.0, 100.0, 120.0], [0.28, 0.22, 0.21]), linear(365, [80.0, 100.0, 120.0], [0.30, 0.25, 0.23])],
     True),
    ("Actual/Actual (ISDA) over flat sections", ACT_ISDA, [91, 400], [flat.date_spec(R + n, R, s, A365) for n, s in
                                                                      [(91, 0.21), (400, 0.27)]], True),
]
GRID = ("makeFromGrid", A365, [91, 182, 365, 730], [70.0, 85.0, 100.0, 115.0, 130.0],
        [[0.32, 0.30, 0.28, 0.27], [0.26, 0.25, 0.24, 0.235], [0.22, 0.215, 0.21, 0.205],
         [0.21, 0.205, 0.20, 0.198], [0.23, 0.22, 0.215, 0.21]])
TIMES = [0.0, 0.05, 0.2493150684931507, 0.4, 0.4986301369863014, 0.75, 1.0, 1.5, 2.0, 3.0]
STRIKES = [60.0, 75.0, 80.0, 90.0, 100.0, 107.5, 120.0, 135.0]


def outputs(s, extrapolates):
    if extrapolates:
        s.enableExtrapolation()
    tenor_times = [s.timeFromReference(d) for d in s_dates(s)]
    section_times = tenor_times + [0.5 * (tenor_times[0] + tenor_times[-1]) + 0.01]
    return {
        "maxDate": s.maxDate(),
        "minStrike": s.minStrike(),
        "maxStrike": s.maxStrike(),
        "blackVariance": [[v.attempt(lambda t=t, k=k: s.blackVariance(t, k, True)) for k in STRIKES] for t in TIMES],
        "blackVol": [[v.attempt(lambda t=t, k=k: s.blackVol(t, k, True)) for k in STRIKES] for t in TIMES],
        "sectionTimes": section_times,
        "sectionVol": [[v.attempt(lambda t=t, k=k: s.smileSection(t, True).volatility(k)) for k in STRIKES]
                       for t in section_times],
    }


_dates = {}


def s_dates(s):
    return _dates[id(s)]


def cases():
    out = []
    st = v.settings(R)
    for name, dc, offsets, sections, extrapolates in SURFACES:
        dates = [R + n for n in offsets]
        s = ql.PiecewiseBlackVarianceSurface(R, dates, [make_section(x) for x in sections], bvts.day_counter(dc))
        _dates[id(s)] = dates
        out.append({"name": name,
                    "inputs": {"settings": st, "form": "sections", "dayCounter": dc, "dates": v.enc(dates),
                               "sections": sections, "extrapolate": extrapolates, "times": TIMES,
                               "strikes": STRIKES},
                    "expected": v.enc(outputs(s, extrapolates))})
    name, dc, offsets, strikes, vols = GRID
    dates = [R + n for n in offsets]
    m = ql.Matrix(len(strikes), len(dates))
    for i in range(len(strikes)):
        for j in range(len(dates)):
            m[i][j] = vols[i][j]
    s = ql.PiecewiseBlackVarianceSurface.makeFromGrid(R, dates, strikes, m, bvts.day_counter(dc))
    _dates[id(s)] = dates
    out.append({"name": name,
                "inputs": {"settings": st, "form": "grid", "dayCounter": dc, "dates": v.enc(dates),
                           "gridStrikes": strikes, "vols": vols, "extrapolate": True, "times": TIMES,
                           "strikes": STRIKES},
                "expected": v.enc(outputs(s, True))})

    f = [flat.date_spec(R + n, R, 0.25, A365) for n in [182, 365]]
    refusals = [
        ("no dates", [], []),
        ("dates and sections in different numbers", [182, 365], f[:1]),
        ("first date on the reference date", [0], [flat.date_spec(R, R, 0.25, A365)]),
        ("dates out of order", [365, 182], [f[1], f[0]]),
        ("a date repeated", [182, 182], [f[0], f[0]]),
    ]
    for name, offsets, sections in refusals:
        dates = [R + n for n in offsets]
        out.append({"name": name,
                    "inputs": {"settings": st, "form": "sections", "dayCounter": A365, "dates": v.enc(dates),
                               "sections": sections},
                    "expected": {"build": v.attempt(lambda d=dates, x=sections: ql.PiecewiseBlackVarianceSurface(
                        R, d, [make_section(y) for y in x], ql_a365).maxDate())}})
    for name, strikes, rows, columns in [("grid rows don't match the strikes", [80.0, 100.0, 120.0], 2, 1),
                                         ("grid columns don't match the dates", [80.0, 100.0, 120.0], 3, 2)]:
        dates = [R + 182]
        vols = [[0.2] * columns for _ in range(rows)]
        m = ql.Matrix(rows, columns)
        for i in range(rows):
            for j in range(columns):
                m[i][j] = vols[i][j]
        out.append({"name": name,
                    "inputs": {"settings": st, "form": "grid", "dayCounter": A365, "dates": v.enc(dates),
                               "gridStrikes": strikes, "vols": vols},
                    "expected": {"build": v.attempt(lambda d=dates, k=strikes, mm=m:
                                                    ql.PiecewiseBlackVarianceSurface.makeFromGrid(
                                                        R, d, k, mm, ql_a365).maxDate())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/piecewiseblackvariancesurface.hpp", cases)
