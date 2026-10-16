# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The encoding self-vector: each sentinel and literal of the vector encoding as the wheel produces it, a scaled
amount and a throwing call. tests/NanoQL.Tests/Vectors/Harness/EncodingVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    return [
        {"name": "null sentinels",
         "inputs": {"settings": st},
         "expected": {"nullReal": v.enc(ql.nullDouble()), "nullInteger": v.enc(ql.nullInt()),
                      "nullDate": v.enc(ql.Date())}},
        {"name": "non-finite reals",
         "inputs": {"settings": st},
         "expected": {"nan": v.enc(float("nan")), "infinity": v.enc(float("inf")),
                      "minusInfinity": v.enc(float("-inf"))}},
        {"name": "literals",
         "inputs": {"settings": st},
         "expected": {"integer": v.enc(ql.Date(15, ql.October, 2026)), "real": v.enc(0.1 + 0.2),
                      "boolean": v.enc(ql.Date.isLeap(2024)), "string": v.enc(ql.Actual360().name()),
                      "array": v.enc([1.0, -2.5, 1e-300])}},
        {"name": "scaled amount",
         "inputs": {"settings": st},
         "expected": {"amount": v.amount(1234.5, 1e6)}},
        {"name": "throwing call",
         "inputs": {"settings": st},
         "expected": {"result": v.attempt(lambda: ql.Date(29, ql.February, 2023))}},
    ]


if __name__ == "__main__":
    v.main(__file__, None, cases)
