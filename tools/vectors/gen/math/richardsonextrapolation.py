# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's RichardsonExtrapolation (ql/math/richardsonextrapolation.hpp, .cpp): four functions of the
step size h with errors of known orders (polynomial and rational, so both sides compute the same arithmetic),
extrapolated from h of 0.1 and 0.5 with the order given, at scaling factors t of 1.5 to 10, and with the order unknown,
at (t, s) pairs, where QuantLib solves for the order by Brent (which finds no root for the last function at h 0.5, a
throw); and the refusals: t at or below 1, s at or below 1 or not below t, and the one-factor form with no order.

The functions, FUNCTIONS below, are written the same way in
tests/NanoQL.Tests/Vectors/Numerics/RichardsonExtrapolationVectorTests.cs. The wheel's calls, by QuantLib-SWIG 1.44's
math.i: RichardsonExtrapolation(f, delta_h, n), the C++ constructor with the Python function wrapped, n left out for
QuantLib's Null; __call__(t) and __call__(t, s) its two operators. Read back by
tests/NanoQL.Tests/Vectors/Numerics/RichardsonExtrapolationVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

FUNCTIONS = {
    "2 + 3h^2 - h^3": (lambda h: 2.0 + 3.0 * h * h - h * h * h, 2.0),
    "1/(1 - h)": (lambda h: 1.0 / (1.0 - h), 1.0),
    "(1 + h^4)/(1 + h^2)": (lambda h: (1.0 + h * h * h * h) / (1.0 + h * h), 2.0),
    "0.5 - 0.25h^3 + h^5": (lambda h: 0.5 - 0.25 * h * h * h + h * h * h * h * h, 3.0),
}
TS = [1.5, 2.0, 4.0, 10.0]
PAIRS = [(4.0, 2.0), (3.0, 1.5), (10.0, 4.0)]


def cases():
    st = v.settings(ql.Date(15, 10, 2026))
    out = []
    for name, (f, n) in FUNCTIONS.items():
        for h in (0.1, 0.5):
            known = ql.RichardsonExtrapolation(f, h, n)
            unknown = ql.RichardsonExtrapolation(f, h)
            out.append({"name": f"{name}, h {h}", "inputs": {"settings": st, "function": name, "deltaH": h, "n": n,
                                                             "t": TS, "pairs": [list(p) for p in PAIRS]},
                        "expected": dict({"known": [known(t) for t in TS]},
                                         **{f"unknown{i}": v.attempt(lambda p=p: unknown(*p))
                                            for i, p in enumerate(PAIRS)})})
    f, n = FUNCTIONS["2 + 3h^2 - h^3"]
    known = ql.RichardsonExtrapolation(f, 0.1, n)
    unknown = ql.RichardsonExtrapolation(f, 0.1)
    refusals = [("t 1", lambda: known(1.0), {"t": 1.0}), ("t 0.5", lambda: known(0.5), {"t": 0.5}),
                ("no order", lambda: unknown(2.0), {"t": 2.0, "unknown": True}),
                ("s 1", lambda: unknown(2.0, 1.0), {"t": 2.0, "s": 1.0}),
                ("s above t", lambda: unknown(2.0, 3.0), {"t": 2.0, "s": 3.0}),
                ("s equal to t", lambda: unknown(2.0, 2.0), {"t": 2.0, "s": 2.0})]
    for name, call, inputs in refusals:
        out.append({"name": f"{name} (refused)",
                    "inputs": dict({"settings": st, "function": "2 + 3h^2 - h^3", "deltaH": 0.1, "n": n}, **inputs),
                    "expected": {"value": v.attempt(call)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/richardsonextrapolation.hpp", cases)
