# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's sticky and ratchet payoffs (ql/instruments/stickyratchet.hpp), each output computed by the C++
harness, since the wheel binds none of them: DoubleStickyRatchetPayoff on every pair of types (0 and +/-1), and two
illegal ones, which QuantLib refuses at every forward; and the six named forms, Ratchet, Sticky, RatchetMax, RatchetMin,
StickyMax and StickyMin, on gearings, spreads, initial values and accrual factors of either sign.

Each case gives name, description and the payoff at forwards from -1% to 10% ({"throws": true} where QuantLib refuses).
Read back by tests/NanoQL.Tests/Vectors/Cxx/Instruments/StickyRatchetCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 45672  # 15 January 2025
FORWARDS = [-0.01, 0.0, 0.01, 0.02, 0.025, 0.03, 0.035, 0.05, 0.1]

# (class, arguments)
PAYOFFS = [("DoubleStickyRatchetPayoff", [t1, t2, 1.0, 0.8, 1.2, 0.001, -0.002, 0.0005, 0.03, 0.025, 0.5])
           for t1 in (-1.0, 0.0, 1.0) for t2 in (-1.0, 0.0, 1.0)]
PAYOFFS += [
    ("DoubleStickyRatchetPayoff", [0.5, 0.0, 1.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.03, 0.0, 1.0]),
    ("DoubleStickyRatchetPayoff", [1.0, -2.0, 1.0, 1.0, 1.0, 0.0, 0.0, 0.0, 0.03, 0.02, 1.0]),
    ("RatchetPayoff", [1.0, 1.0, 0.0, 0.0, 0.03, 0.5]),
    ("RatchetPayoff", [0.9, 1.1, 0.002, -0.001, 0.025, 0.25]),
    ("StickyPayoff", [1.0, 1.0, 0.0, 0.0, 0.03, 0.5]),
    ("StickyPayoff", [1.2, 0.7, -0.003, 0.004, 0.02, 1.0]),
    ("RatchetMaxPayoff", [1.0, 1.0, 1.0, 0.0, 0.0, 0.0, 0.03, 0.025, 0.5]),
    ("RatchetMaxPayoff", [0.8, 1.3, 1.1, 0.001, -0.002, 0.0015, 0.028, 0.032, -0.5]),
    ("RatchetMinPayoff", [1.0, 1.0, 1.0, 0.0, 0.0, 0.0, 0.03, 0.025, 0.5]),
    ("RatchetMinPayoff", [1.1, 0.9, 1.0, -0.001, 0.002, 0.0, 0.022, 0.035, 0.75]),
    ("StickyMaxPayoff", [1.0, 1.0, 1.0, 0.0, 0.0, 0.0, 0.03, 0.025, 0.5]),
    ("StickyMaxPayoff", [0.7, 1.4, 1.2, 0.002, 0.001, -0.001, 0.035, 0.02, 1.0]),
    ("StickyMinPayoff", [1.0, 1.0, 1.0, 0.0, 0.0, 0.0, 0.03, 0.025, 0.5]),
    ("StickyMinPayoff", [1.0, 0.5, 2.0, 0.0, 0.003, -0.002, 0.04, 0.01, 0.25]),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for k, (cls, args) in enumerate(PAYOFFS):
            name, description, values = H.call("StickyRatchetPayoff", cls, args, FORWARDS)
            out.append({"name": f"{cls} / {k}",
                        "inputs": {"settings": st, "class": cls, "arguments": args, "forwards": FORWARDS},
                        "expected": {"name": name, "description": description,
                                     "values": [dict(v.THROWS) if x is None else x for x in values]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/stickyratchet.hpp", cases)
