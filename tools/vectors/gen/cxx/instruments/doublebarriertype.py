# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DoubleBarrier::Type (ql/instruments/doublebarriertype.hpp, .cpp), whose stream operator the
wheel doesn't bind, each output computed by the C++ harness: the stream text of each type, and QuantLib's refusal of
a value outside the type.

Each case's inputs hold its settings (the evaluation date, which nothing here reads) and "type", QuantLib's integer.
Read back by tests/NanoQL.Tests/Vectors/Cxx/Instruments/DoubleBarrierTypeCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TYPES = [0, 1, 2, 3, 7]


def cases():
    with cxx.connect() as H:
        st = H.settings(46310)
        return [{"name": f"operator<<({t})", "inputs": {"settings": st, "type": t},
                 "expected": {"text": v.attempt(lambda t=t: H.call("operator<<(DoubleBarrier::Type)", t))}}
                for t in TYPES]


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/doublebarriertype.hpp", cases)
