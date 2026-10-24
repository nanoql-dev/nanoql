# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's time basket (ql/cashflows/timebasket.hpp), each output computed by the C++ harness: the sum
and difference of two baskets (QuantLib's += and -=, which the wheel doesn't bind), disjoint, overlapping, one of them
empty, a date's value cancelling to 0 (which stays in the basket), and hasDate on the result's dates and others. The
rest of the basket is cashflows/timebasket.py's (the wheel).

Each "pair" case gives the sum's and the difference's dates and values in date order (plusDates, plusValues, minusDates,
minusValues) and hasDate of the first basket at the probe dates. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Coupons/TimeBasketCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 45672  # 15 January 2025

# (label, (dates, values), (dates, values), probes)
PAIRS = [
    ("disjoint", ([R + 17, R + 59], [100.0, 50.0]), ([R + 30, R + 230], [-20.0, 7.5]), [R + 17, R + 30, R + 59]),
    ("overlapping", ([R + 17, R + 59, R + 400], [100.0, 50.0, 1e6]), ([R + 59, R + 400, R + 900], [25.0, -2e5, 3.0]),
     [R + 59, R + 900]),
    ("a value cancelling to 0", ([R + 10, R + 20], [12.5, 30.0]), ([R + 10], [12.5]), [R + 10, R + 11]),
    ("the second empty", ([R + 1, R + 2], [1.0, 2.0]), ([], []), [R + 1, R + 3]),
    ("the first empty", ([], []), ([R + 5, R + 4], [-1.0, 4.0]), [R + 4]),
    ("both empty", ([], []), ([], []), [R]),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, (d1, v1), (d2, v2), probes in PAIRS:
            plus = H.call("TimeBasket::plus", d1, v1, d2, v2)
            minus = H.call("TimeBasket::minus", d1, v1, d2, v2)
            out.append({"name": label,
                        "inputs": {"settings": st, "dates": d1, "values": v1, "otherDates": d2, "otherValues": v2,
                                   "probes": probes},
                        "expected": {"plusDates": plus[0], "plusValues": plus[1], "minusDates": minus[0],
                                     "minusValues": minus[1],
                                     "hasDate": [H.call("TimeBasket::hasDate", d1, v1, p) for p in probes]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/timebasket.hpp", cases)
