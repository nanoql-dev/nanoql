# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's TimeGrid (ql/timegrid.hpp, .cpp) that the wheel doesn't expose, each output
computed by the C++ harness (docs/porting-playbook.md §9.4).

Ten grids from the three constructors, each with 30 times: every time of the grid, times within close_enough of one
(above and below), midpoints between neighbours (where closestIndex takes the earlier), times before the first and
after the last, and random times inside. For each grid: index(t) (or its refusal) at each time, closestIndex(t) and
closestTime(t); its mandatoryTimes, size, empty, front and back; and at(i) at every index. Then the empty grid of the
default constructor (size, empty, mandatoryTimes, and closestIndex, which reads no time), and the constructors'
refusals, read through size. The harness never calls index or closestTime with NaN, nor front, back, closestTime or
index on the empty grid, nor at out of range: each is undefined in QuantLib or throws std::out_of_range.

A grid argument is the harness's TimeGrid spec (tools/cxx/src/timegrid.hpp): {} for TimeGrid(), {"end", "steps"},
{"times"} or {"times", "steps"}. Read back by tests/NanoQL.Tests/Vectors/Cxx/TimeGridCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

EPS = 2.220446049250313e-16

GRIDS = [
    {"end": 1.0, "steps": 4},
    {"end": 10.0, "steps": 7},
    {"end": 2.5, "steps": 1},
    {"times": [1.0, 2.0, 5.0]},
    {"times": [0.0, 0.5, 0.75, 3.0, 3.25]},
    {"times": [4.0, 1.0, 1.0 + 10 * EPS, 2.0, 1.0 + 60 * EPS]},
    {"times": [1.0, 2.0, 4.0], "steps": 8},
    {"times": [0.0, 1.0, 2.0, 4.0], "steps": 0},
    {"times": [0.3, 1.0, 1.7], "steps": 5},
    {"times": [0.25, 0.5, 2.0, 6.0], "steps": 13},
]


def queries(rnd, times):
    n = len(times)
    out = list(times)
    out += [times[i] * (1.0 + 10 * EPS) for i in range(1, n, 2)]
    out += [times[i] * (1.0 - 10 * EPS) for i in range(2, n, 3)]
    out += [0.5 * (times[i] + times[i + 1]) for i in range(0, n - 1, 2)]
    out += [-0.5, -1e-300, times[-1] + 0.25, times[-1] * (1.0 + 1e-9)]
    while len(out) < 30:
        out.append(rnd.uniform(times[0], times[-1]))
    return out[:30]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 16, 10, 2026))
        rnd = random.Random(v.SEED)

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        for k, grid in enumerate(GRIDS):
            size = H.call("TimeGrid::size", grid)
            at = [H.call("TimeGrid::at", grid, i) for i in range(size)]
            ts = queries(rnd, at)
            out.append({"name": f"{k} {'regular' if 'end' in grid else 'mandatory'}"
                                f"{' with steps' if 'times' in grid and 'steps' in grid else ''}",
                        "inputs": {"settings": st, "timeGrid": grid, "t": ts},
                        "expected": {
                            "index": [call("TimeGrid::index", grid, t) for t in ts],
                            "closestIndex": [H.call("TimeGrid::closestIndex", grid, t) for t in ts],
                            "closestTime": [H.call("TimeGrid::closestTime", grid, t) for t in ts],
                            "mandatoryTimes": H.call("TimeGrid::mandatoryTimes", grid),
                            "size": size,
                            "empty": H.call("TimeGrid::empty", grid),
                            "front": H.call("TimeGrid::front", grid),
                            "back": H.call("TimeGrid::back", grid),
                            "at": at}})

        empty = {}
        ts = [0.0, 1.0, -1.0]
        out.append({"name": "empty grid",
                    "inputs": {"settings": st, "timeGrid": empty, "t": ts},
                    "expected": {"size": H.call("TimeGrid::size", empty),
                                 "empty": H.call("TimeGrid::empty", empty),
                                 "mandatoryTimes": H.call("TimeGrid::mandatoryTimes", empty),
                                 "closestIndex": [H.call("TimeGrid::closestIndex", empty, t) for t in ts]}})

        refusals = [
            ("regular end 0", {"end": 0.0, "steps": 3}),
            ("regular end negative", {"end": -2.0, "steps": 3}),
            ("mandatory empty", {"times": []}),
            ("mandatory negative", {"times": [2.0, -1.0, 4.0]}),
            ("with steps negative", {"times": [-0.25, 1.0], "steps": 4}),
            ("with steps 0 and only 0", {"times": [0.0], "steps": 0}),
        ]
        for name, grid in refusals:
            out.append({"name": name,
                        "inputs": {"settings": st, "timeGrid": grid},
                        "expected": {"size": call("TimeGrid::size", grid)}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/timegrid.hpp", cases)
