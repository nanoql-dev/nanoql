# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Estr preset (ql/indexes/ibor/estr.hpp), the euro short-term rate, an overnight index fixed on
TARGET: one case per index, as tools/vectors/gen/indexes/ibor/euribor.py writes them, with its DATES and index_case: the
name, family name, tenor, fixing days, currency code, fixing calendar's name, day counter's name, business-day
convention and end-of-month flag, and fixingDate, valueDate and maturityDate at the 30 dates, valueDate refusing a date
that isn't a fixing date.

The wheel's Estr is QuantLib-SWIG's proxy of the C++ class of that name, its constructor QuantLib's with the default
empty forwarding handle. Read back by tests/NanoQL.Tests/Vectors/Indexes/Ibor/EstrVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_euribor",
                                               os.path.join(v.GEN_ROOT, "indexes", "ibor", "euribor.py"))
eur = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(eur)


def cases():
    return [eur.index_case({"type": "Estr"}, ql.Estr)]


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/estr.hpp", cases)
