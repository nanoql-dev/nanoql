# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Period (ql/time/period.hpp): normalisation, frequency, negation and text; pairwise <, <= and
==, including the pairs whose order QuantLib can't decide (its <, <= and == throw there); sums, differences and
products. The wheel's Period.__le__ calls the wrapped C++ operator<=."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

UNITS = [ql.Days, ql.Weeks, ql.Months, ql.Years]


def period(n, u):
    return ql.Period(n, UNITS[u])


def pair(p):
    return [p.length(), int(p.units())]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for n in [-14, -12, -7, -1, 0, 1, 2, 3, 4, 6, 7, 12, 13, 14, 24, 26, 52, 365]:
        for u in range(4):
            p = period(n, u)
            out.append({"name": f"unary {n} {u}",
                        "inputs": {"settings": st, "period": [n, u]},
                        "expected": {"normalized": pair(p.normalized()),
                                     "frequency": v.attempt(lambda p=p: int(p.frequency())),
                                     "negated": pair(-p), "toString": str(p)}})
    base = [(n, u) for n in (0, 1, 2, 4, 7, 12, 14, 24, 28, 30, 31, 52, 365, 366, -1) for u in range(4)]
    for a in base:
        pa = period(*a)
        out.append({"name": f"compare {a[0]} {a[1]}",
                    "inputs": {"settings": st, "period": list(a), "others": [list(b) for b in base]},
                    "expected": {"less": [v.attempt(lambda b=b: pa < period(*b)) for b in base],
                                 "lessOrEqual": [v.attempt(lambda b=b: pa <= period(*b)) for b in base],
                                 "equal": [v.attempt(lambda b=b: pa == period(*b)) for b in base]}})
    for a in base[::3]:
        pa = period(*a)
        out.append({"name": f"arithmetic {a[0]} {a[1]}",
                    "inputs": {"settings": st, "period": list(a), "others": [list(b) for b in base],
                               "multipliers": [0, 2, -3]},
                    "expected": {"plus": [v.attempt(lambda b=b: pair(pa + period(*b))) for b in base],
                                 "minus": [v.attempt(lambda b=b: pair(pa - period(*b))) for b in base],
                                 "times": [v.attempt(lambda k=k: pair(pa * k)) for k in (0, 2, -3)]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/period.hpp", cases)
