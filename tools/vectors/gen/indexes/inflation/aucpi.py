# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Australian CPI (ql/indexes/inflation/aucpi.hpp): AUCPI, through
tools/vectors/gen/indexes/inflationindex.py's case families, on its fixings and curves, with its inputs and outputs:
AUCPI quarterly and unrevised, and monthly and revised: each one's inspectors; fixings stored and read back, and a
conflicting one refused; reads on the fixings alone; forecasts on a zero curve and a clone's on a second; and CPI's
lagged observations, past and forecast. YYAUCPI, which the wheel doesn't bind, is the harness's
(tools/vectors/gen/cxx/indexes/inflation/aucpi.py). The wheel's classes are QuantLib-SWIG's proxies of the C++ classes
of those names, their constructors QuantLib's. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Inflation/AUCPIVectorTests.cs."""
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
ii.PRESETS.update({"AUCPI": ql.AUCPI})

M = ii.M
APRIL, JUNE = ql.Date(10, ql.April, 2024), ql.Date(15, ql.June, 2024)
QUARTERLY = {"frequency": int(ql.Quarterly), "revised": False}
MONTHLY = {"frequency": int(ql.Monthly), "revised": True}

INDEXES = [ii.preset("AUCPI", **QUARTERLY), ii.preset("AUCPI", **MONTHLY)]
STORES = [
    ("AU CPI, quarterly", JUNE, ii.preset("AUCPI", **QUARTERLY), ii.QUARTERLY_FIXINGS[:5], [], False),
    ("AU CPI, monthly", JUNE, ii.preset("AUCPI", **MONTHLY), ii.UK_FIXINGS[-6:], [], False),
    ("duplicate refused, quarterly", JUNE, ii.preset("AUCPI", **QUARTERLY), ii.QUARTERLY_FIXINGS[:3],
     [(ii.QUARTERLY_FIXINGS[1][0] - 40, 50.0, False)], True),
]
READS = [
    ("AU CPI, quarterly", APRIL, ii.preset("AUCPI", **QUARTERLY), ii.QUARTERLY_FIXINGS),
    ("AU CPI, monthly", JUNE, ii.preset("AUCPI", **MONTHLY), ii.UK_FIXINGS),
]
FORECASTS = [
    ("AU CPI, quarterly", APRIL, ii.preset("AUCPI", ii.ZERO_QUARTERLY, **QUARTERLY), ii.QUARTERLY_FIXINGS,
     dict(ii.ZERO_QUARTERLY, enableExtrapolation=True)),
    ("AU CPI, monthly", JUNE, ii.preset("AUCPI", ii.ZERO_UK, **MONTHLY), ii.UK_FIXINGS, ii.ZERO_UK_SEASONAL),
]
LAGGED = [
    ("AU CPI, quarterly", APRIL, ii.preset("AUCPI", dict(ii.ZERO_QUARTERLY, enableExtrapolation=True), **QUARTERLY),
     ii.QUARTERLY_FIXINGS, [[0, M], [3, M], [6, M]]),
    ("AU CPI, monthly, past", JUNE, ii.preset("AUCPI", **MONTHLY), ii.UK_FIXINGS, [[0, M], [2, M], [3, M]]),
]


def cases():
    return (ii.inspector_cases(INDEXES) + ii.store_cases(STORES) + ii.read_cases(READS)
            + ii.forecast_cases(FORECASTS) + ii.lagged_cases(LAGGED))


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/inflation/aucpi.hpp", cases)
