# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TrinomialTree (ql/methods/lattices/trinomialtree.hpp, .cpp), which the wheel doesn't
expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4).

Thirteen trees on Ornstein-Uhlenbeck processes: uniform grids of 1 to 25 steps; the non-uniform grid TimeGrid([0.25,
0.2525, 1.0, 1.003, 3.0, 5.0], 20), whose tree is widest at an earlier step than its last; grids of mandatory times
only with a step 0.0099 and 0.0101 of the longest, either side of the dx floor's 0.01 threshold (as
test-suite/trinomialtree.cpp's testFloorThresholdBoundary), and 0.001 (its testSmallMandatoryGapDoesNotExplode); mean
reversion large, small, near 0 (below sqrt(QL_EPSILON), the process's own branch), 0 and negative; isPositive on a
process near 0, alone and with a floored step; a negative start. For each: the branches constant, columns(),
timeGrid(), and at every column size(i) and dx(i), underlying(i, j) at every node, and, at every column with a step
after it, descendant(i, j, b) and probability(i, j, b) at every node and branch. Then the constructor's refusals,
read through columns(): a grid of one time ("null time steps for trinomial tree"), the empty grid (QuantLib's "vector
too long", nanoql's own refusal), a zero and a NaN volatility and a NaN reversion (NaN probabilities, nanoql's
refusal), a near-zero volatility whose node offset leaves the integers, and a negative volatility (the process's).

A tree argument is the harness's spec (tools/cxx/src/methods/lattices/trinomialtree.cpp): {"process": {"type":
"OrnsteinUhlenbeckProcess", "speed", "volatility", "x0", "level"}, "timeGrid", "isPositive"}, the grid a TimeGrid
spec of tools/vectors/gen/cxx/timegrid.py. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/Lattices/TrinomialTreeCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def ou(speed, volatility, x0=0.0, level=0.0):
    return {"type": "OrnsteinUhlenbeckProcess", "speed": speed, "volatility": volatility, "x0": x0, "level": level}


def tree(process, grid, positive=False):
    return {"process": process, "timeGrid": grid, "isPositive": positive}


TREES = [
    ("uniform, 4 steps", tree(ou(0.1, 0.01), {"end": 1.0, "steps": 4})),
    ("uniform, 20 steps to 10 years", tree(ou(0.1, 0.01), {"end": 10.0, "steps": 20})),
    ("large reversion", tree(ou(1.5, 0.02, 0.03, 0.05), {"end": 5.0, "steps": 25})),
    ("reversion below sqrt(QL_EPSILON)", tree(ou(1e-9, 0.01), {"end": 3.0, "steps": 12})),
    ("zero reversion", tree(ou(0.0, 0.012, 0.01, 0.02), {"end": 2.0, "steps": 8})),
    ("negative reversion", tree(ou(-0.05, 0.01, 0.0, 0.01), {"end": 4.0, "steps": 10})),
    ("non-uniform, widest before the last step",
     tree(ou(0.1, 0.01), {"times": [0.25, 0.2525, 1.0, 1.003, 3.0, 5.0], "steps": 20})),
    ("floored step, 0.0099 of the longest", tree(ou(0.1, 0.01), {"times": [1.0, 1.0099, 2.0, 3.0]})),
    ("unfloored step, 0.0101 of the longest", tree(ou(0.1, 0.01), {"times": [1.0, 1.0101, 2.0, 3.0]})),
    ("small mandatory gap", tree(ou(0.1, 0.01), {"times": [1.0, 1.001, 2.0, 3.0]})),
    ("isPositive near 0", tree(ou(0.1, 0.05, 0.02, 0.04), {"end": 2.0, "steps": 8}, True)),
    ("isPositive with a floored step", tree(ou(0.2, 0.03, 0.03, 0.05), {"times": [0.5, 0.5049, 1.0, 2.0]}, True)),
    ("negative start, mandatory times with steps",
     tree(ou(0.5, 0.02, -0.01, 0.0), {"times": [0.3, 1.0, 1.7], "steps": 5})),
]

REFUSALS = [
    ("one time", tree(ou(0.1, 0.01), {"times": [0.0]})),
    ("empty grid", tree(ou(0.1, 0.01), {})),
    ("zero volatility", tree(ou(0.1, 0.0), {"end": 1.0, "steps": 4})),
    ("NaN volatility", tree(ou(0.1, math.nan), {"end": 1.0, "steps": 4})),
    ("NaN reversion", tree(ou(math.nan, 0.01), {"end": 1.0, "steps": 4})),
    ("near-zero volatility, the node offset out of the integers",
     tree(ou(0.1, 1e-300, 0.05), {"end": 1.0, "steps": 4})),
    ("negative volatility", tree(ou(0.1, -0.01), {"end": 1.0, "steps": 4})),
]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 16, 10, 2026))
        out = []
        for name, spec in TREES:
            columns = H.call("Tree::columns", spec)
            sizes = [H.call("TrinomialTree::size", spec, i) for i in range(columns)]
            steps = range(columns - 1)
            out.append({"name": name,
                        "inputs": {"settings": st, "tree": v.enc(spec)},
                        "expected": {
                            "branches": H.call("TrinomialTree::branches"),
                            "columns": columns,
                            "timeGrid": H.call("TrinomialTree::timeGrid", spec),
                            "size": sizes,
                            "dx": [H.call("TrinomialTree::dx", spec, i) for i in range(columns)],
                            "underlying": [[H.call("TrinomialTree::underlying", spec, i, j) for j in range(sizes[i])]
                                           for i in range(columns)],
                            "descendant": [[[H.call("TrinomialTree::descendant", spec, i, j, b) for b in range(3)]
                                            for j in range(sizes[i])] for i in steps],
                            "probability": [[[H.call("TrinomialTree::probability", spec, i, j, b) for b in range(3)]
                                             for j in range(sizes[i])] for i in steps]}})
        for name, spec in REFUSALS:
            out.append({"name": name,
                        "inputs": {"settings": st, "tree": v.enc(spec)},
                        "expected": {"columns": v.attempt(lambda: H.call("Tree::columns", spec))}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/lattices/trinomialtree.hpp", cases)
