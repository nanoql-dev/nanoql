# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's YYAUCPI (ql/indexes/inflation/aucpi.hpp), each output computed by the C++ harness: the wheel
doesn't bind it. YYAUCPI quarterly and unrevised, and monthly and revised, in
tools/vectors/gen/indexes/inflationindex.py's case families, with their inputs and outputs, on its fixings and curves
(the harness's curves take no seasonality, so a clone's curve is the forecast curve shifted 20 basis points): each one's
inspectors; reads on the fixings alone; forecasts on a year-on-year curve and a clone's on a second; and
CPI::laggedYoYRate, past and forecast. The quarterly fixings are
inflationindex.py's quarterly index's, read as year-on-year rates. The harness's methods are
tools/cxx/src/indexes/inflation/aucpi.cpp's. tests/NanoQL.Tests/Vectors/Cxx/Indexes/Inflation/AUCPICxxVectorTests.cs
reads it back."""
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

_spec = importlib.util.spec_from_file_location("wheel_inflationindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "inflationindex.py"))
ii = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ii)
its = ii.its

M = ii.M
APRIL, JUNE = ql.Date(10, ql.April, 2024), ql.Date(15, ql.June, 2024)
QUARTERLY = {"frequency": int(ql.Quarterly), "revised": False}
MONTHLY = {"frequency": int(ql.Monthly), "revised": True}
LAG = ql.Period(2, ql.Months)  # YYAUCPI's availability lag
INTERPOLATIONS = [int(i) for i in ii.INTERPOLATIONS]

YY_QUARTERLY_FIXINGS = [[s, round(0.02 + (val - 100.0) / 400.0, 6)] for s, val in ii.QUARTERLY_FIXINGS]
YOY_QUARTERLY = its.curve_spec("YoYInflationCurve", APRIL,
                               its.monthly_nodes(ql.Date(1, ql.January, 2024), (1, 2, 5, 10)),
                               [0.024, 0.023, 0.022, 0.021, 0.02], ql.Quarterly, its.A365, extrapolate=True)


def shifted(curve):
    return dict(curve, rates=[round(r + 0.002, 6) for r in curve["rates"]])


# (label, evaluation date, the preset's arguments, fixings, curve)
SETUPS = [
    ("quarterly", APRIL, QUARTERLY, YY_QUARTERLY_FIXINGS, YOY_QUARTERLY, [[0, M], [3, M], [6, M]]),
    ("monthly", JUNE, MONTHLY, ii.YY_FIXINGS, ii.YOY_UK, [[0, M], [2, M], [3, M]]),
]


def case(H, kind, label, k, evaluation_date, inputs, method, *args):
    with H.case_settings(v.enc(evaluation_date)) as st:
        expected = H.call(method, *args)
    return {"name": f"{kind} YYAUCPI, {label} / {k}", "inputs": {"settings": st, "kind": kind, **inputs},
            "expected": expected}


def cases():
    out = []
    with cxx.connect() as H:
        for k, (label, evaluation_date, preset, fixings, curve, lags) in enumerate(SETUPS):
            args = [preset["frequency"], preset["revised"]]
            weekend = v.enc(JUNE)
            out.append(case(H, "inspectors", label, k, JUNE,
                            {"index": ii.preset("YYAUCPI", **preset), "weekend": weekend},
                            "YYAUCPI::inspectors", *args, weekend))
        for k, (label, evaluation_date, preset, fixings, curve, lags) in enumerate(SETUPS):
            args = [preset["frequency"], preset["revised"]]
            dates = v.enc(ii.around(evaluation_date - LAG, 400, 120, 13))
            out.append(case(H, "reads", label, k, evaluation_date,
                            {"index": ii.preset("YYAUCPI", **preset), "fixings": fixings, "dates": dates},
                            "YYAUCPI::reads", *args, fixings, dates))
        for k, (label, evaluation_date, preset, fixings, curve, lags) in enumerate(SETUPS):
            args = [preset["frequency"], preset["revised"]]
            dates = v.enc(ii.around(evaluation_date, 90, 3700, 97))
            out.append(case(H, "forecasts", label, k, evaluation_date,
                            {"index": ii.preset("YYAUCPI", curve, **preset), "fixings": fixings, "dates": dates,
                             "cloneCurve": shifted(curve)},
                            "YYAUCPI::forecasts", *args, curve, fixings, dates, shifted(curve)))
        for k, (label, evaluation_date, preset, fixings, curve, lags) in enumerate(SETUPS):
            args = [preset["frequency"], preset["revised"]]
            dates = v.enc(ii.around(evaluation_date, 420, 1500, 37))
            out.append(case(H, "lagged", label, k, evaluation_date,
                            {"index": ii.preset("YYAUCPI", curve, **preset), "fixings": fixings, "dates": dates,
                             "lags": lags, "interpolations": INTERPOLATIONS},
                            "YYAUCPI::lagged", *args, curve, fixings, dates, lags, INTERPOLATIONS))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/inflation/aucpi.hpp", cases)
