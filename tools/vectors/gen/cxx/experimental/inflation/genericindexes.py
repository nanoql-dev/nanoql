# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's generic inflation indexes (ql/experimental/inflation/genericindexes.hpp), which the wheel
doesn't bind, each output computed by the C++ harness (tools/cxx/src/experimental/inflation/genericindexes.cpp).

- "values" cases: a GenericCPI and a YYGenericCPI at monthly and quarterly frequencies, revised and not, with
  availability lags of one to three months, in euros, dollars and sterling: each one's name, family name, frequency,
  revised flag, lag, region name and code, and currency code.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Inflation/GenericIndexesCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

M = int(ql.Months)
CASES = [(int(ql.Monthly), False, [3, M], "EUR"), (int(ql.Quarterly), True, [2, M], "USD"),
         (int(ql.Monthly), True, [1, M], "GBP")]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(45000)
        for frequency, revised, lag, code in CASES:
            out.append({"name": f"frequency {frequency}, revised {revised}, lag {lag[0]}M, {code}",
                        "inputs": {"settings": st, "frequency": frequency, "revised": revised, "lag": lag,
                                   "currency": code},
                        "expected": H.call("GenericIndexes::values", frequency, revised, lag, code)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/inflation/genericindexes.hpp", cases)
