# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Loss (ql/experimental/credit/loss.hpp), which the wheel doesn't expose, each output computed
by the C++ harness (tools/cxx/src/experimental/credit/loss.cpp): the default and the two-value constructors, and the
four comparisons on pairs of losses, which read the times alone (equal times with unequal amounts compare equal). Read
back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/LossCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 38960  # 31 August 2006, the evaluation date, which no output reads
LOSSES = [(0.5, 10.0), (1.25, 0.0), (0.0, 3.5), (-1.0, 2.0), (1e-300, 1e300)]
PAIRS = [(0.5, 10.0, 1.25, 0.0), (1.25, 0.0, 0.5, 10.0), (0.5, 10.0, 0.5, 20.0), (0.5, 1.0, 0.5, 1.0),
         (0.0, 1.0, -0.0, 2.0), (-1.0, 2.0, 1e-300, 1e300), (2.0, -5.0, 2.0 + 2.0 ** -51, -5.0)]


def cases():
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(R) as st:
            return [{"name": "losses",
                     "inputs": {"settings": st, "losses": [list(x) for x in LOSSES],
                                "pairs": [list(p) for p in PAIRS]},
                     "expected": {"default": call("Loss::inspect"),
                                  "inspect": [call("Loss::inspect", t, a) for t, a in LOSSES],
                                  "compare": call("Loss::compare", [list(p) for p in PAIRS])}}]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/loss.hpp", cases)
