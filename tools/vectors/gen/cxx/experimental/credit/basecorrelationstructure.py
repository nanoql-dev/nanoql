# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BaseCorrelationTermStructure (ql/experimental/credit/basecorrelationstructure.hpp) and
CorrelationTermStructure (correlationstructure.hpp), which the wheel doesn't expose, each output computed by the C++
harness (tools/cxx/src/experimental/credit/basecorrelationstructure.cpp): bilinear surfaces of two to four tenors and
loss levels, settled 0 and 2 days, under two conventions; the reference and maximum dates, the size and the convention,
the correlation by date and by time at the grid's nodes, between them and off the grid (bilinear extrapolation, the
extrapolate flag unread), and the dates of tenors. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/BaseCorrelationTermStructureCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


pf = _load("cxx_defaultevent", "cxx/experimental/credit/defaultevent.py")
dts = pf.dts
TODAY = pf.TODAY

Y = pf.YEARS
M = pf.MONTHS
SURFACES = [
    ("four tenors, five levels", pf.surface([(1, Y), (3, Y), (5, Y), (7, Y)], [0.03, 0.07, 0.1, 0.15, 0.3],
                                            [[0.15, 0.17, 0.18, 0.2], [0.22, 0.24, 0.25, 0.27], [0.28, 0.3, 0.31, 0.33],
                                             [0.35, 0.37, 0.38, 0.4], [0.5, 0.52, 0.55, 0.58]])),
    ("two tenors, two levels, two settlement days, modified following",
     pf.surface([(6, M), (2, Y)], [0.05, 0.2], [[0.1, 0.2], [0.3, 0.45]], settlement_days=2, convention=1)),
    ("three tenors, two levels", pf.surface([(1, Y), (2, Y), (10, Y)], [0.1, 1.0], [[0.2, 0.25, 0.3], [0.6, 0.6, 0.7]])),
]
DATES = [(TODAY + 200).serialNumber(), (TODAY + 730).serialNumber(), (TODAY + 2000).serialNumber(),
         (TODAY + 4000).serialNumber()]
TIMES = [0.0, 0.5, 1.0, 2.5, 6.0, 12.0]
LEVELS = [0.01, 0.03, 0.05, 0.12, 0.3, 0.5]
TENORS = [[1, Y], [6, M], [18, M], [5, Y]]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, s in SURFACES:
            with H.case_settings(TODAY.serialNumber()) as st:
                out.append({"name": name,
                            "inputs": {"settings": st, "surface": s, "dates": DATES, "times": TIMES, "levels": LEVELS,
                                       "tenors": TENORS},
                            "expected": {"reads": call("BaseCorrelationTermStructure::reads", s, DATES, TIMES, LEVELS,
                                                       TENORS)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/basecorrelationstructure.hpp", cases)
