# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FedFunds preset (ql/indexes/ibor/fedfunds.hpp), the Federal Funds effective rate, an
overnight index on the US Federal Reserve calendar: one case, as tools/vectors/gen/indexes/ibor/euribor.py writes them,
with its DATES and index_case. The wheel's FedFunds is QuantLib-SWIG's proxy of the C++ class, its constructor
QuantLib's with the default empty forwarding handle. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Ibor/FedFundsVectorTests.cs."""
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
    return [eur.index_case({"type": "FedFunds"}, ql.FedFunds)]


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/fedfunds.hpp", cases)
