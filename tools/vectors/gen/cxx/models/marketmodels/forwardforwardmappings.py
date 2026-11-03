# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ForwardForwardMappings::RestrictCurveState
(ql/models/marketmodels/forwardforwardmappings.hpp, .cpp), which the wheel doesn't expose, each output computed by the
C++ harness: a curve state restricted to every multiplier-th rate time from an offset.

LIBOR curve states on twelve semi-annual and eight quarterly rates, multipliers 1, 2, 3 and 4, every offset below the
multiplier: the restricted state's rate times, forward rates and discount ratios to its last bond. Throws: an offset
not below the multiplier, a restriction with no long forward (the multiplier past the rates).

Left out (tests/NanoQL.Tests/Divergences/ForwardForwardMappingsTests.cs): the Jacobian and the Y matrix, which nanoql
fixes; and an offset past the rates, where QuantLib's unsigned count wraps to an endless allocation, which nanoql
refuses.

The harness unit is tools/cxx/src/models/marketmodels/forwardforwardmappings.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/ForwardForwardMappingsCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
GRIDS = [
    ("twelve semi-annual rates", [0.5 * (i + 1) for i in range(13)]),
    ("eight quarterly rates", [0.25 * (i + 1) for i in range(9)]),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            fwds = [0.028 + 0.0019 * i + 0.0007 * (i % 2) for i in range(n)]
            for multiplier in (1, 2, 3, 4):
                for offset in range(multiplier):
                    out.append({"name": f"{label}, multiplier {multiplier}, offset {offset}",
                                "inputs": {"settings": st, "call": "restrict", "rateTimes": times, "forwards": fwds,
                                           "multiplier": multiplier, "offset": offset},
                                "expected": v.enc(H.call("ForwardForwardMappings::RestrictCurveState", times, fwds,
                                                         multiplier, offset))})
            for rlabel, multiplier, offset in (("an offset at the multiplier", 2, 2), ("no long forward", n + 1, 0)):
                out.append({"name": f"{label}, refused: {rlabel}",
                            "inputs": {"settings": st, "call": "refused", "rateTimes": times, "forwards": fwds,
                                       "multiplier": multiplier, "offset": offset},
                            "expected": {"restriction": v.attempt(lambda: H.call(
                                "ForwardForwardMappings::RestrictCurveState", times, fwds, multiplier, offset))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/forwardforwardmappings.hpp", cases)
