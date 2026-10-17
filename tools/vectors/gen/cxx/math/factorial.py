# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Factorial::get and Factorial::ln (ql/math/factorial.hpp), which the wheel doesn't expose
(docs/porting-playbook.md §9.4), each output computed by the C++ harness: n from 0 to 300, through the table's last
entry, 27, the gamma function above it, and the overflow of n! from 171 on; and four large n up to 2147483647, the
largest nanoql's int takes. tests/NanoQL.Tests/Vectors/Cxx/Numerics/FactorialCxxVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

LARGE = [1000, 1000000, 2147483646, 2147483647]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def case(name, ns):
            return {"name": name, "inputs": {"settings": st, "n": ns},
                    "expected": {"get": [v.attempt(lambda n=n: H.call("Factorial::get", n)) for n in ns],
                                 "ln": [v.attempt(lambda n=n: H.call("Factorial::ln", n)) for n in ns]}}

        return [case("0 to 300", list(range(301))), case("large", LARGE)]


if __name__ == "__main__":
    v.main(__file__, "ql/math/factorial.hpp", cases)
