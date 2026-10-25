# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FixedLocalVolSurface (ql/termstructures/volatility/equityfx/fixedlocalvolsurface.hpp, .cpp),
on the evaluation date 15 October 2026.

- Surfaces: seven, by each of the three constructors (dates and one strike vector, times and one strike vector, times
  and a strike vector per time, the first collapsed to one level as a calibration's at time 0), each strike
  extrapolation on either side, linear and, through setInterpolation("cubic"), Kruger's cubic in strike. Outputs: the
  maximum date and time and the strike range, and the local volatility at a grid of times (before the first, at and
  between the nodes, past the last) and strikes (inside and outside each time's strikes), extrapolating.
- Refusals: a first date before the reference date, a negative first time, times out of order, a matrix whose columns
  don't match the times or whose rows don't match the strikes, strikes out of order, and strike vectors other than one
  per time.

Left out (the S-van plan's §2.10): at a node time, and at every time before the first or past the last, which clamp
onto one, a strike outside the node's strikes where its side's extrapolation is the constant one: nanoql holds the
volatility there, where QuantLib's interpolator extrapolates (VR4 (xviii);
tests/NanoQL.Tests/Divergences/FixedLocalVolNodeExtrapolationTests.cs). The specs ("form" dates, times or
perTimeStrikes) are written by the functions below, which tools/vectors/gen/methods/finitedifferences/utilities/
localvolrndcalculator.py shares. The wheel's calls, by QuantLib-SWIG 1.44: the three constructors and
setInterpolation(name). nanoql's WithInterpolation returns a copy, which the test reads. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/FixedLocalVolSurfaceVectorTests.cs."""
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
A365 = bvts.A365
CONST = int(ql.FixedLocalVolSurface.ConstantExtrapolation)
INTERP = int(ql.FixedLocalVolSurface.InterpolatorDefaultExtrapolation)


def surface_spec(form, when, strikes, vols, dc=A365, lower=CONST, upper=CONST, interpolation=None):
    """A FixedLocalVolSurface spec: "when" the dates' offsets (form "dates") or the times; "strikes" one vector, or one
    per time (form "perTimeStrikes"); "vols" [strike][time]."""
    spec = {"type": "FixedLocalVolSurface", "form": form, "referenceDate": R.serialNumber(), "dayCounter": dc,
            "strikes": strikes, "vols": vols, "lower": lower, "upper": upper, "interpolation": interpolation}
    if form == "dates":
        spec["dates"] = [(R + d).serialNumber() for d in when]
    else:
        spec["times"] = list(when)
    return spec


def fixed_local_vol(spec):
    dc = bvts.day_counter(spec["dayCounter"])
    m = ql.Matrix(len(spec["vols"]), len(spec["vols"][0]) if spec["vols"] else 0)
    for i, row in enumerate(spec["vols"]):
        for j, x in enumerate(row):
            m[i][j] = x
    ref = ql.Date(spec["referenceDate"])
    if spec["form"] == "dates":
        s = ql.FixedLocalVolSurface(ref, [ql.Date(d) for d in spec["dates"]], spec["strikes"], m, dc,
                                    spec["lower"], spec["upper"])
    else:
        s = ql.FixedLocalVolSurface(ref, spec["times"], spec["strikes"], m, dc, spec["lower"], spec["upper"])
    if spec["interpolation"] is not None:
        s.setInterpolation(spec["interpolation"])
    return s


def node_times(spec, s):
    if spec["form"] == "dates":
        return [s.timeFromReference(ql.Date(d)) for d in spec["dates"]]
    return list(spec["times"])


def strikes_at(spec, idx):
    return spec["strikes"][idx] if spec["form"] == "perTimeStrikes" else spec["strikes"]


def close_enough(x, y, n=42):
    """QuantLib's close_enough (ql/math/comparison.hpp)."""
    if x == y:
        return True
    diff, tol = abs(x - y), n * 2.220446049250313e-16
    if x * y == 0.0:
        return diff < tol * tol
    return diff <= tol * abs(x) or diff <= tol * abs(y)


def diverges(spec, times, t, k):
    """VR4 (xviii)'s reach: at a node (after QuantLib's clamp to the first and last times) and outside its strikes on a
    constant side."""
    tc = min(times[-1], max(t, times[0]))
    idx = next(i for i, x in enumerate(times) if not x < tc)
    if not close_enough(tc, times[idx]):
        return False
    ks = strikes_at(spec, idx)
    return (spec["lower"] == CONST and k < ks[0]) or (spec["upper"] == CONST and k > ks[-1])


STRIKES = [60.0, 80.0, 100.0, 120.0, 150.0]
SMILE = [[0.32, 0.30, 0.28, 0.27], [0.26, 0.25, 0.24, 0.235], [0.22, 0.215, 0.21, 0.205],
         [0.23, 0.22, 0.215, 0.21], [0.27, 0.26, 0.25, 0.24]]
RAGGED = [[100.0, 100.0, 100.0, 100.0, 100.0], [70.0, 85.0, 100.0, 115.0, 130.0], [50.0, 75.0, 100.0, 135.0, 180.0],
          [40.0, 70.0, 100.0, 150.0, 220.0]]
SURFACES = [
    ("dates, constant both sides", surface_spec("dates", [91, 182, 365, 730], STRIKES, SMILE)),
    ("dates, the interpolator's both sides", surface_spec("dates", [91, 182, 365, 730], STRIKES, SMILE, lower=INTERP,
                                                          upper=INTERP)),
    ("times, constant below", surface_spec("times", [0.25, 0.5, 1.0, 2.0], STRIKES, SMILE, upper=INTERP)),
    ("times, cubic", surface_spec("times", [0.25, 0.5, 1.0, 2.0], STRIKES, SMILE, interpolation="cubic")),
    ("times from 0, one time", surface_spec("times", [0.0], STRIKES, [[r[0]] for r in SMILE])),
    ("per-time strikes, collapsed at 0", surface_spec("perTimeStrikes", [0.0, 0.5, 1.0, 2.0], RAGGED,
                                                      [[0.25, 0.30, 0.31, 0.32], [0.25, 0.26, 0.27, 0.28],
                                                       [0.25, 0.22, 0.23, 0.24], [0.25, 0.24, 0.25, 0.26],
                                                       [0.25, 0.28, 0.29, 0.30]])),
    ("per-time strikes, the interpolator's above", surface_spec("perTimeStrikes", [0.1, 0.5, 1.0, 2.0], RAGGED[1:] +
                                                                [[45.0, 72.0, 100.0, 140.0, 200.0]],
                                                                SMILE, upper=INTERP)),
]
TIMES = [0.0, 0.05, 0.1, 0.25, 0.3, 0.4986301369863014, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0]
QUERY_STRIKES = [30.0, 55.0, 60.0, 75.0, 90.0, 100.0, 110.0, 125.0, 150.0, 190.0, 300.0]


def cases():
    out = []
    st = v.settings(R)
    for name, spec in SURFACES:
        s = fixed_local_vol(spec)
        times = node_times(spec, s)
        points = [[t, k] for t in TIMES for k in QUERY_STRIKES if not diverges(spec, times, t, k)]
        print(f"{name}: {len(TIMES) * len(QUERY_STRIKES) - len(points)} points left out")
        out.append({"name": name,
                    "inputs": {"settings": st, "surface": spec, "points": points},
                    "expected": v.enc({
                        "maxDate": s.maxDate(),
                        "maxTime": s.maxTime(),
                        "minStrike": s.minStrike(),
                        "maxStrike": s.maxStrike(),
                        "localVol": [v.attempt(lambda t=t, k=k: s.localVol(t, k, True)) for t, k in points]})})
    refusals = [
        ("a first date before the reference date", surface_spec("dates", [-1, 365], STRIKES, [r[:2] for r in SMILE])),
        ("a negative first time", surface_spec("times", [-0.1, 1.0], STRIKES, [r[:2] for r in SMILE])),
        ("times out of order", surface_spec("times", [1.0, 0.5], STRIKES, [r[:2] for r in SMILE])),
        ("columns don't match the times", surface_spec("times", [0.5, 1.0, 2.0], STRIKES, [r[:2] for r in SMILE])),
        ("rows don't match the strikes", surface_spec("times", [0.5, 1.0], STRIKES, [r[:2] for r in SMILE[:4]])),
        ("strikes out of order", surface_spec("times", [0.5, 1.0], [60.0, 100.0, 80.0, 120.0, 150.0],
                                              [r[:2] for r in SMILE])),
        ("strike vectors other than one per time", surface_spec("perTimeStrikes", [0.5, 1.0, 2.0], RAGGED[1:3],
                                                                [r[:3] for r in SMILE])),
    ]
    for name, spec in refusals:
        out.append({"name": name, "inputs": {"settings": st, "surface": spec},
                    "expected": {"build": v.attempt(lambda spec=spec: fixed_local_vol(spec).maxTime())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/fixedlocalvolsurface.hpp", cases)
