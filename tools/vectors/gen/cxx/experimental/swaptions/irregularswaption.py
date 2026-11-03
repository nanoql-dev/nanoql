# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's irregular swap, its swaption and Hagan's engine (ql/experimental/swaptions/irregularswap.hpp,
irregularswaption.hpp, haganirregularswaptionengine.hpp and their .cpp files), which the wheel doesn't bind, each output
computed by the C++ harness (tools/cxx/src/experimental/swaptions/irregularswaption.cpp).

- "values" cases: five-year swaps a year forward on a flat curve, payer and receiver: constant terms, an amortizing
  notional, a step-up fixed rate, and floating spreads; the swap's NPV and legs on the discounting swap engine, and its
  European swaption at the swap's start on Hagan's engine at a normal volatility of 1%, with the swaption's type and
  settlement type. The fair rate and spread, a debug 0 on QuantLib's swap engine and null on nanoql's, are left out.
- "expired" case: the constant-terms swap and its swaption on an evaluation date past the swap's end, both expired.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Swaptions/IrregularSwaptionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46311  # Friday 16 October 2026
EXPIRED = REFERENCE + 7 * 365  # past the swap's end, six years on
FLAT_NOTIONALS = ([100.0] * 5, [100.0] * 10)
AMORTIZING = ([100.0, 80.0, 60.0, 40.0, 20.0], [100.0, 100.0, 80.0, 80.0, 60.0, 60.0, 40.0, 40.0, 20.0, 20.0])
CASES = [
    ("constant terms", 1, FLAT_NOTIONALS, [0.03] * 5, [0.0] * 10),
    ("constant terms, receiver", -1, FLAT_NOTIONALS, [0.03] * 5, [0.0] * 10),
    ("amortizing", 1, AMORTIZING, [0.03] * 5, [0.0] * 10),
    ("step-up rate", 1, FLAT_NOTIONALS, [0.02, 0.025, 0.03, 0.035, 0.04], [0.0] * 10),
    ("floating spread, amortizing", 1, AMORTIZING, [0.03] * 5, [0.001] * 10),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        for name, kind, (fixed_n, float_n), rates, spreads in CASES:
            args = [REFERENCE, 0.03, kind, fixed_n, rates, float_n, spreads, 0.01]
            out.append({"name": name,
                        "inputs": {"settings": st, "args": args},
                        "expected": H.call("HaganIrregularSwaptionEngine::values", *args)})
        expired = H.settings(EXPIRED)
        args = [REFERENCE, 0.03, 1, FLAT_NOTIONALS[0], [0.03] * 5, FLAT_NOTIONALS[1], [0.0] * 10, 0.01]
        out.append({"name": "expired: constant terms, past the swap's end",
                    "inputs": {"settings": expired, "args": args},
                    "expected": H.call("HaganIrregularSwaptionEngine::values", *args)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/swaptions/haganirregularswaptionengine.hpp", cases)
