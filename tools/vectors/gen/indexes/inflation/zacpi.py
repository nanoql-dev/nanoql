# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's South African CPI (ql/indexes/inflation/zacpi.hpp): ZACPI and YYZACPI, through
tools/vectors/gen/indexes/inflationindex.py's case families, on its fixings and curves, with its inputs and outputs:
each index's inspectors; ZACPI's fixings stored and read back, and a conflicting one refused; reads on the fixings
alone; forecasts on a zero or year-on-year curve and a clone's on a second, one zero curve's base date without a fixing;
and CPI's lagged observations, past and forecast. The wheel's classes are QuantLib-SWIG's proxies of the C++ classes of
those names, their constructors QuantLib's. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Inflation/ZACPIVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_inflationindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "inflationindex.py"))
ii = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ii)
ii.PRESETS.update({"ZACPI": ql.ZACPI, "YYZACPI": ql.YYZACPI})

M = ii.M
JUNE = ql.Date(15, ql.June, 2024)
LAGS = [[0, M], [1, M], [3, M]]
ZERO = ii.preset("ZACPI")

INDEXES = [ii.preset("ZACPI"), ii.preset("YYZACPI")]
STORES = [
    ("ZA CPI, 12 months", JUNE, ZERO, ii.UK_FIXINGS[-12:], [], False),
    ("duplicate refused", JUNE, ZERO, ii.UK_FIXINGS[-3:], [(ii.UK_FIXINGS[-2][0] + 5, 1.0, False)], True),
]
READS = [
    ("ZA CPI", JUNE, ZERO, ii.UK_FIXINGS),
    ("quoted year-on-year", JUNE, ii.preset("YYZACPI"), ii.YY_FIXINGS),
]
FORECASTS = [
    ("ZA CPI", JUNE, ii.preset("ZACPI", ii.ZERO_UK), ii.UK_FIXINGS, ii.ZERO_UK_SEASONAL),
    ("ZA CPI, base fixing missing", JUNE, ii.preset("ZACPI", ii.ZERO_UK_LATE), ii.UK_FIXINGS, ii.ZERO_UK),
    ("quoted year-on-year", JUNE, ii.preset("YYZACPI", ii.YOY_UK), ii.YY_FIXINGS, ii.YOY_UK_SEASONAL),
]
LAGGED = [
    ("ZA CPI, past", JUNE, ZERO, ii.UK_FIXINGS, LAGS),
    ("ZA CPI, forecast", JUNE, ii.preset("ZACPI", ii.ZERO_UK_SEASONAL), ii.UK_FIXINGS, LAGS),
    ("quoted year-on-year", JUNE, ii.preset("YYZACPI", ii.YOY_UK), ii.YY_FIXINGS, LAGS),
]


def cases():
    return (ii.inspector_cases(INDEXES) + ii.store_cases(STORES) + ii.read_cases(READS)
            + ii.forecast_cases(FORECASTS) + ii.lagged_cases(LAGGED))


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/inflation/zacpi.hpp", cases)
