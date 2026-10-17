# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The encoding self-vector's twin: tools/vectors/gen/harness/encoding.py's cases, each value produced by the C++
harness. tools/vectors/agree.py checks it against tests/vectors/harness/encoding.json, and
tests/NanoQL.Tests/Vectors/Cxx/Harness/EncodingCxxVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        return [
            {"name": "null sentinels",
             "inputs": {"settings": st},
             "expected": {"nullReal": v.enc(H.call("harness.nullReal")),
                          "nullInteger": v.enc(H.call("harness.nullInteger")),
                          "nullDate": v.enc(H.call("harness.nullDate"))}},
            {"name": "non-finite reals",
             "inputs": {"settings": st},
             "expected": {"nan": v.enc(H.call("harness.nan")), "infinity": v.enc(H.call("harness.infinity")),
                          "minusInfinity": v.enc(H.call("harness.minusInfinity"))}},
            {"name": "literals",
             "inputs": {"settings": st},
             "expected": {"integer": v.enc(H.call("Date(Day,Month,Year)", 15, 10, 2026)),
                          "real": v.enc(H.call("harness.sum", 0.1, 0.2)),
                          "boolean": v.enc(H.call("Date::isLeap", 2024)),
                          "string": v.enc(H.call("DayCounter::name", {"type": "Actual360"})),
                          "array": v.enc(H.call("harness.reals", [1.0, -2.5, 1e-300]))}},
            {"name": "scaled amount",
             "inputs": {"settings": st},
             "expected": {"amount": v.amount(H.call("harness.echo", 1234.5), 1e6)}},
            {"name": "throwing call",
             "inputs": {"settings": st},
             "expected": {"result": v.attempt(lambda: H.call("Date(Day,Month,Year)", 29, 2, 2023))}},
        ]


if __name__ == "__main__":
    v.main(__file__, None, cases)
