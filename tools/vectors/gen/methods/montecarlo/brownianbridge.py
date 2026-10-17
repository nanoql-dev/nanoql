# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BrownianBridge (ql/methods/montecarlo/brownianbridge.hpp, .cpp): 20 bridges from its three
constructors (steps, a vector of times, a time grid), of 1 to 40 steps, the times regular, irregular or from a
TimeGrid with mandatory times and steps. For each bridge: its size and times; the three weight arrays (leftWeight,
rightWeight, stdDeviation); the three index arrays (bridgeIndex, leftIndex, rightIndex); five transforms of random
Gaussian-like inputs, one of them starting at 0 (the canonical bridge); and the refusal of an input of another size.

The wheel's calls, by QuantLib-SWIG's montecarlo.i: ql.BrownianBridge(n), ql.BrownianBridge(times) and
ql.BrownianBridge(timeGrid) are the three constructors (an int dispatches to Size, a list to std::vector<Time>);
size, times, leftWeight, rightWeight and stdDeviation are the members; bridgeIndex, leftIndex and rightIndex are
SWIG's extensions copying the Size vectors to unsigned ints; transform(input) is SWIG's extension calling
transform(input.begin(), input.end(), output.begin()) into a new vector of the input's size, so a wrong size reaches
QuantLib's own check. Read back by tests/NanoQL.Tests/Vectors/Methods/MonteCarlo/BrownianBridgeVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def increasing(rnd, n, start, spread):
    t, out = 0.0, []
    for _ in range(n):
        t += rnd.uniform(start, spread)
        out.append(t)
    return out


def cases():
    st = v.settings(ql.Date(16, ql.October, 2026))
    rnd = random.Random(v.SEED)
    bridges = []
    for n in (1, 2, 3, 5, 8, 12, 33):
        bridges.append(("steps", {"steps": n}, ql.BrownianBridge(n), n))
    for n in (1, 2, 4, 7, 12, 16, 40):
        times = increasing(rnd, n, 0.01, 1.5)
        bridges.append(("times", {"times": times}, ql.BrownianBridge(times), n))
    grids = [
        {"end": 1.0, "steps": 6},
        {"end": 10.0, "steps": 12},
        {"times": [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 2.0, 5.0]},
        {"times": [0.5, 1.0, 1.5, 5.0, 10.0]},
        {"times": [1.0, 2.0, 4.0], "steps": 8},
        {"times": [0.25, 3.0, 7.5], "steps": 11},
    ]
    for g in grids:
        if "end" in g:
            tg = ql.TimeGrid(g["end"], g["steps"])
        elif "steps" in g:
            tg = ql.TimeGrid(g["times"], g["steps"])
        else:
            tg = ql.TimeGrid(g["times"])
        bridges.append(("timeGrid", {"timeGrid": g}, ql.BrownianBridge(tg), len(tg) - 1))

    out = []
    for k, (kind, inputs, bb, n) in enumerate(bridges):
        variates = [[rnd.gauss(0.0, 1.0) for _ in range(n)] for _ in range(5)]
        variates[0][0] = 0.0
        wrong = [rnd.gauss(0.0, 1.0) for _ in range(n + 1)]
        out.append({"name": f"{k} {kind} size {n}",
                    "inputs": {"settings": st, "constructor": kind, **inputs, "variates": variates, "wrong": wrong},
                    "expected": {
                        "size": bb.size(),
                        "times": list(bb.times()),
                        "leftWeight": list(bb.leftWeight()),
                        "rightWeight": list(bb.rightWeight()),
                        "stdDeviation": list(bb.stdDeviation()),
                        "bridgeIndex": list(bb.bridgeIndex()),
                        "leftIndex": list(bb.leftIndex()),
                        "rightIndex": list(bb.rightIndex()),
                        "transform": [v.enc(list(bb.transform(x))) for x in variates],
                        "transformWrongSize": v.attempt(lambda: list(bb.transform(wrong)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/montecarlo/brownianbridge.hpp", cases)
