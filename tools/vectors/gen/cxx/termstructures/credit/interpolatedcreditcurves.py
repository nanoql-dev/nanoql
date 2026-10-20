# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's interpolated credit curves (ql/termstructures/credit/interpolatedhazardratecurve.hpp,
interpolateddefaultdensitycurve.hpp and interpolatedsurvivalprobabilitycurve.hpp) with jumps and on the
interpolators the wheel doesn't bind (it binds backward-flat hazard rates, and linear densities and probabilities),
each output computed by the C++ harness.

- "curve" cases: each curve type on backward-flat, linear and log-linear interpolation, without jumps, with jumps on
  given dates (one in the past, one on the reference date, unsorted ones, one past the last node) and with jumps on
  QuantLib's turn-of-year dates: the dates, times, data, maximum date, jump dates and jump times, and at 15 sample
  dates, read with extrapolation, the survival probability, default density and hazard rate, each refused where
  QuantLib refuses it: past a jump outside (0, 1], and where a log-linear interpolation of hazard rates or densities
  needs the primitive QuantLib doesn't implement.

A case's inputs hold its settings, the curve spec (tools/vectors/gen/termstructures/defaulttermstructure.py's, with
"interpolator", "jumps" and "jumpDates") and the sample dates, as tools/cxx/src/termstructures/credit/
interpolatedcreditcurves.cpp reads them. A refused value is {"throws": true}. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/InterpolatedCreditCurvesCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_defaulttermstructure",
                                               os.path.join(v.GEN_ROOT, "termstructures", "defaulttermstructure.py"))
dts = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dts)

TODAY = dts.TODAY
Y = ql.Years
NODES = [TODAY, TODAY + ql.Period(1, Y), TODAY + ql.Period(3, Y), TODAY + ql.Period(5, Y), TODAY + ql.Period(10, Y)]
DATA = {"HazardRateCurve": [0.01, 0.012, 0.018, 0.022, 0.03],
        "DefaultDensityCurve": [0.01, 0.012, 0.016, 0.015, 0.012],
        "SurvivalProbabilityCurve": [1.0, 0.988, 0.95, 0.9, 0.78]}
JUMPS = [
    ("no jumps", None, None),
    ("jumps on dates", [0.99, 0.98, 0.97], [TODAY - 30, TODAY + 400, TODAY + 200]),
    ("a jump on the reference date and one past the last node", [0.95, 0.9], [TODAY, TODAY + 4000]),
    ("turn-of-year jumps", [0.995, 0.99], None),
    ("a jump above one", [1.2], [TODAY + 100]),
]
SAMPLE = [TODAY + d for d in (0, 30, 99, 100, 101, 199, 200, 201, 365, 399, 400, 401, 1500, 3653, 4500)]


def curve(kind, interp, jumps, jump_dates):
    spec = dts.curve_spec(kind, NODES, DATA[kind])
    spec["interpolator"] = interp
    if jumps is not None:
        spec["jumps"] = jumps
        spec["jumpDates"] = v.enc(jump_dates or [])
    return spec


def cases():
    out = []
    k = 0
    with cxx.connect() as H:
        for kind in DATA:
            for interp in ("BackwardFlat", "Linear", "LogLinear"):
                for label, jumps, jump_dates in JUMPS:
                    with H.case_settings(v.enc(TODAY)) as st:
                        spec = curve(kind, interp, jumps, jump_dates)
                        expected = H.call("InterpolatedCreditCurve::values", spec, v.enc(SAMPLE))
                        out.append({"name": f"curve {kind} {interp} {label} / {k}",
                                    "inputs": {"settings": st, "curve": spec, "sample": v.enc(SAMPLE)},
                                    "expected": expected})
                    k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/credit/interpolatedhazardratecurve.hpp", cases)
