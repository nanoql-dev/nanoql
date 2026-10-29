# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's time-dependent binomial trees (ql/experimental/lattices/extendedbinomialtree.hpp, .cpp), which
the wheel doesn't bind, each output computed by the C++ harness
(tools/cxx/src/experimental/lattices/extendedbinomialtree.cpp).

- "nodes" cases: each of the seven extended trees on a constant volatility and on a time-dependent one (0.10 to a year,
  0.30 forward to two, 0.25 to five, as a Black variance curve, which the Leisen-Reimer and Joshi trees read to twice
  the tree's end), twenty steps to two years at the money: the column count, the first
  six columns' node values and their up probabilities.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Lattices/ExtendedBinomialTreeCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46311  # Friday 16 October 2026
TREES = ["ExtendedJarrowRudd", "ExtendedCoxRossRubinstein", "ExtendedAdditiveEQPBinomialTree", "ExtendedTrigeorgis",
         "ExtendedTian", "ExtendedLeisenReimer", "ExtendedJoshi4"]
VOLS = {"constant": [0.2], "time-dependent": [0.10, 0.2236068, 0.25]}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        for tree in TREES:
            for label, vols in VOLS.items():
                args = [tree, REFERENCE, 100.0, 0.05, 0.02, vols, 2.0, 20, 100.0, 6]
                out.append({"name": f"{tree}, {label} volatility",
                            "inputs": {"settings": st, "type": tree, "referenceDate": REFERENCE, "spot": 100.0,
                                       "r": 0.05, "q": 0.02, "vols": vols, "end": 2.0, "steps": 20, "strike": 100.0,
                                       "columns": 6},
                            "expected": H.call("ExtendedBinomialTree::nodes", *args)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/lattices/extendedbinomialtree.hpp", cases)
