# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's Rounding (ql/math/rounding.hpp) that the wheel doesn't expose
(docs/porting-playbook.md §9.4), each output computed by the C++ harness: the constructor Rounding(precision, type,
digit), for the six types (None among them, which the wheel's classes can't build with a precision) at every precision
QuantLib documents, 0 to 16, with rounding digits 5, 0 and 10 (QuantLib doesn't check the digit: 0 always rounds up
and 10 never does); its inspectors precision(), type() and roundingDigit(); and the
rounding applied to two values. Rounding(), QuantLib's default construction, gives its type, None; its precision and
digit are uninitialized (rounding.hpp:87, 89), so they aren't read.

Each case's input "rounding" is the harness's Rounding spec, {"precision", "type", "digit"}, or null for Rounding();
"values" lists the values rounded. Read back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/RoundingCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

# Rounding::Type: None, Up, Down, Closest, Floor, Ceiling (rounding.hpp:48-69).
TYPES = range(6)
PRECISIONS = range(17)
DIGITS = [0, 5, 10]
VALUES = [1.2345678901234567, -98.76543210987654]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = [{"name": "Rounding()", "inputs": {"settings": st, "rounding": None, "values": []},
                "expected": {"type": H.call("Rounding::type", None)}}]
        for t in TYPES:
            for p in PRECISIONS:
                for d in DIGITS:
                    r = {"precision": p, "type": t, "digit": d}
                    out.append({"name": f"Rounding({p}, {t}, {d})",
                                "inputs": {"settings": st, "rounding": r, "values": VALUES}, "expected": {
                                    "precision": H.call("Rounding::precision", r),
                                    "type": H.call("Rounding::type", r),
                                    "roundingDigit": H.call("Rounding::roundingDigit", r),
                                    "rounded": [v.attempt(lambda x=x: H.call("Rounding::operator()", r, x))
                                                for x in VALUES]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/rounding.hpp", cases)
