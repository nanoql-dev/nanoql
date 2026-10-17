# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for operator<<(BusinessDayConvention) (ql/time/businessdayconvention.hpp), each output computed by the C++
harness: every convention, Following (0) to Nearest (6), and the values -1 and 7 just outside the enum.
tests/NanoQL.Tests/Vectors/Cxx/Time/BusinessDayConventionCxxVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

CONVENTIONS = list(range(-1, 8))
# Values just outside BusinessDayConvention. nanoql refuses them (docs/porting-playbook.md §2), so one appears only
# where QuantLib throws too.
OUTSIDE = [-1, 7]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        texts = [v.attempt(lambda b=b: H.call("operator<<(BusinessDayConvention)", b)) for b in CONVENTIONS]
        assert all(texts[CONVENTIONS.index(b)] == v.THROWS for b in OUTSIDE), "QuantLib's operator<< returns outside"
        return [{"name": "conventions -1 to 7", "inputs": {"settings": st, "conventions": CONVENTIONS},
                 "expected": {"toString": texts}}]


if __name__ == "__main__":
    v.main(__file__, "ql/time/businessdayconvention.hpp", cases)
