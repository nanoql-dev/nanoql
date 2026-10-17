# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Path (ql/methods/montecarlo/path.hpp) built by its user, which the wheel can't build (its
constructor is private in QuantLib-SWIG), each output computed by the C++ harness (docs/porting-playbook.md §9.4).

Six paths over grids from TimeGrid's three constructors, with values given: for each, length, empty, front, back,
the time grid's times and the values from begin() to end(), and at every index operator[], at, value and time. Then
paths with Path's default values (an empty Array) and with an explicitly empty list, which the constructor replaces
by Array(n), whose elements QuantLib leaves uninitialized: for these only length, empty, the time grid and time(i),
which don't read the values, among them a path over the empty grid. Then the constructor's refusal of values whose
number isn't the grid's, read through length.

A path argument is the harness's spec (tools/cxx/src/methods/montecarlo/path.cpp): {"timeGrid": <TimeGrid spec>,
"values": <list, or null for the default>}. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/MonteCarlo/PathCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

GRIDS = [
    {"end": 1.0, "steps": 4},
    {"end": 10.0, "steps": 12},
    {"times": [0.5, 1.0, 2.5, 4.0]},
    {"times": [0.0, 2.0, 3.0]},
    {"times": [1.0, 2.0, 4.0], "steps": 8},
    {"times": [0.25, 1.5], "steps": 5},
]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 16, 10, 2026))
        rnd = random.Random(v.SEED)

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        for k, grid in enumerate(GRIDS):
            n = H.call("TimeGrid::size", grid)
            path = {"timeGrid": grid, "values": [rnd.uniform(-2.0, 2.0) for _ in range(n)]}
            out.append({"name": f"{k} values",
                        "inputs": {"settings": st, "path": path},
                        "expected": {
                            "length": H.call("Path::length", path),
                            "empty": H.call("Path::empty", path),
                            "front": H.call("Path::front", path),
                            "back": H.call("Path::back", path),
                            "timeGrid": H.call("Path::timeGrid", path),
                            "values": H.call("Path::begin", path),
                            "element": [H.call("Path::operator[]", path, i) for i in range(n)],
                            "at": [H.call("Path::at", path, i) for i in range(n)],
                            "value": [H.call("Path::value", path, i) for i in range(n)],
                            "time": [H.call("Path::time", path, i) for i in range(n)]}})

        for name, path in (("default values", {"timeGrid": GRIDS[1], "values": None}),
                           ("empty values", {"timeGrid": GRIDS[4], "values": []}),
                           ("empty grid", {"timeGrid": {}, "values": None})):
            n = H.call("Path::length", path)
            out.append({"name": name,
                        "inputs": {"settings": st, "path": path},
                        "expected": {"length": n,
                                     "empty": H.call("Path::empty", path),
                                     "timeGrid": H.call("Path::timeGrid", path),
                                     "time": [H.call("Path::time", path, i) for i in range(n)]}})

        for name, path in (("too few values", {"timeGrid": GRIDS[0], "values": [1.0, 2.0]}),
                           ("too many values", {"timeGrid": GRIDS[2], "values": [0.1 * i for i in range(7)]})):
            out.append({"name": name,
                        "inputs": {"settings": st, "path": path},
                        "expected": {"length": call("Path::length", path)}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/montecarlo/path.hpp", cases)
