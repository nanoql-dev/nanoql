# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's UK HICP (ql/indexes/inflation/ukhicp.hpp): UKHICP, through
tools/vectors/gen/indexes/inflationindex.py's case families, on its fixings and curves, with its inputs and outputs: its
inspectors; its fixings stored and read back, and a conflicting one refused; reads on the fixings alone; forecasts on a
zero curve and a clone's on a second, one curve's base date without a fixing; and CPI's lagged observations, past and
forecast. The wheel's classes are QuantLib-SWIG's proxies of the C++ classes of those names, their constructors
QuantLib's. Read back by tests/NanoQL.Tests/Vectors/Indexes/Inflation/UKHICPVectorTests.cs."""
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
ii.PRESETS.update({"UKHICP": ql.UKHICP})

M = ii.M
JUNE = ql.Date(15, ql.June, 2024)
LAGS = [[0, M], [1, M], [3, M]]
ZERO = ii.preset("UKHICP")

INDEXES = [ii.preset("UKHICP")]
STORES = [
    ("UK HICP, 12 months", JUNE, ZERO, ii.UK_FIXINGS[-12:], [], False),
    ("duplicate refused", JUNE, ZERO, ii.UK_FIXINGS[-3:], [(ii.UK_FIXINGS[-2][0] + 5, 1.0, False)], True),
]
READS = [
    ("UK HICP", JUNE, ZERO, ii.UK_FIXINGS),
]
FORECASTS = [
    ("UK HICP", JUNE, ii.preset("UKHICP", ii.ZERO_UK), ii.UK_FIXINGS, ii.ZERO_UK_SEASONAL),
    ("UK HICP, base fixing missing", JUNE, ii.preset("UKHICP", ii.ZERO_UK_LATE), ii.UK_FIXINGS, ii.ZERO_UK),
]
LAGGED = [
    ("UK HICP, past", JUNE, ZERO, ii.UK_FIXINGS, LAGS),
    ("UK HICP, forecast", JUNE, ii.preset("UKHICP", ii.ZERO_UK_SEASONAL), ii.UK_FIXINGS, LAGS),
]


def cases():
    return (ii.inspector_cases(INDEXES) + ii.store_cases(STORES) + ii.read_cases(READS)
            + ii.forecast_cases(FORECASTS) + ii.lagged_cases(LAGGED))


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/inflation/ukhicp.hpp", cases)
