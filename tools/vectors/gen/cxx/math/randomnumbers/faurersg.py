# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for FaureRsg (ql/math/randomnumbers/faurersg.hpp, .cpp), which the wheel doesn't bind, each output computed
by the C++ harness on a fresh generator: the first 200 draws in 1, 2, 3, 5, 10, 50 and 100 dimensions (bases 2, 2, 3, 5,
11, 53 and 101), each draw's sequence and integer sequence, MSVC's 32-bit long setting the digits the sequence carries.
Read back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/FaureRsgCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

DIMENSIONS = [1, 2, 3, 5, 10, 50, 100]
DRAWS = 200


def cases():
    out = []
    with cxx.connect() as H:
        for dim in DIMENSIONS:
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            r = H.call("FaureRsg::draws", dim, DRAWS)
            out.append({"name": f"{dim} dimensions", "inputs": {"settings": st, "dimensionality": dim, "draws": DRAWS},
                        "expected": {"nextSequence": [v.enc(s) for s in r["nextSequence"]],
                                     "lastIntSequence": r["lastIntSequence"]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/faurersg.hpp", cases)
