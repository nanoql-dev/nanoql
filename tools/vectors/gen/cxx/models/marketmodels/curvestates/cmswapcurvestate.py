# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CMSwapCurveState (ql/models/marketmodels/curvestates/cmswapcurvestate.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: every read of states set on constant-maturity swap
rates.

Four rate-time grids (ten semi-annual rates from 0.5, seven quarterly from 0.25, six irregular times from 0.3, and one
rate from 1 to 2), states spanning 1, 2, 3 and more forwards than the rates, each set on its swap rates from first
valid indexes 0, 2 and the last rate (where the grid has them): the harness's curve-state reads (discount ratios to
the last bond, forward, coterminal and constant-maturity swap rates and annuities at spannings 1, 2, 3 and past the
rates, its own among them, and the three vector reads) and every swapRate(begin, end). Throws: a set of the wrong size
or at an index past the rates.

Left out: a rate read at the number of rates, which QuantLib's index check admits, reading past its vectors (nanoql
refuses it, the S-mm plan's MR3 (xx)).

The harness unit is tools/cxx/src/models/marketmodels/curvestate.cpp ("CurveState.reads"). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/CMSwapCurveStateCxxVectorTests.cs."""
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
    ("ten semi-annual rates", [0.5 * (i + 1) for i in range(11)]),
    ("seven quarterly rates", [0.25 * (i + 1) for i in range(8)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
    ("one rate", [1.0, 2.0]),
]
SPANS = [1, 2, 3, 12]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            rates = [0.029 + 0.0021 * i - 0.0006 * (i % 3) for i in range(n)]
            for spanning in sorted({1, 2, 3, n + 2}):
                for first in sorted({0, min(2, n - 1), n - 1}):
                    out.append({"name": f"{label}, spanning {spanning}, set from {first}",
                                "inputs": {"settings": st, "call": "reads", "rateTimes": times, "spanning": spanning,
                                           "values": rates, "first": first, "spans": SPANS},
                                "expected": v.enc(H.call("CurveState.reads", "CMSwap", times, rates, first, spanning,
                                                         SPANS))})
            out.append({"name": f"{label}, sets refused",
                        "inputs": {"settings": st, "call": "badSets", "rateTimes": times, "values": rates},
                        "expected": {
                            "short": v.attempt(lambda: H.call("CurveState.reads", "CMSwap", times, rates[:-1], 0, 1,
                                                              [])),
                            "pastIndex": v.attempt(lambda: H.call("CurveState.reads", "CMSwap", times, rates, n, 1,
                                                                  []))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/curvestates/cmswapcurvestate.hpp", cases)
