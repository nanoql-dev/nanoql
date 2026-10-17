# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TimeGrid (ql/timegrid.hpp, .cpp) through the members the wheel exposes: 30 grids from its
three constructors, ten each, and for each grid its size, its times and its steps (dt). The regular grids,
TimeGrid(end, steps), take ends from 0.25 to 30 and 0 to 40 steps (0 gives one time, 0 times the infinite step, NaN).
The grids with mandatory times only, TimeGrid(times), and with mandatory times and steps, TimeGrid(times, steps), take
unsorted times, repeated times, times within close_enough of one another (dropped by std::unique, which compares
each time with the last one kept), times starting at 0 or not, and step counts whose ratios round half away from zero
(std::lround) or round to 0 and are raised to 1; the steps constructor also runs with 0 steps, where the longest step
is the shortest distance between the mandatory times. Then the refusals: a regular grid whose end isn't positive,
an empty sequence, a negative time, and 0 steps with no time but 0.

The wheel's calls, by QuantLib-SWIG's grid.i: ql.TimeGrid(end, steps) is TimeGrid(Time, Size); ql.TimeGrid(times)
and ql.TimeGrid(times, steps) are SWIG's extensions calling TimeGrid(begin, end) and TimeGrid(begin, end, steps) over
the list; len() is size(); [i] is operator[], and dt(i) is dt, each behind SWIG's own bounds check, which compares
dt's index with size() rather than dt's size, so dt is read only for the size - 1 steps. The other members
(index, closestIndex, closestTime, mandatoryTimes and the element access the wheel lacks) are the C++ harness's
(tools/vectors/gen/cxx/timegrid.py). Read back by tests/NanoQL.Tests/Vectors/TimeGridVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

EPS = 2.220446049250313e-16

REGULAR = [(1.0, 4), (10.0, 5), (0.25, 1), (30.0, 40), (1.0, 0), (2.5, 3), (7.0, 12), (0.1, 7), (5.0, 24), (12.0, 11)]

MANDATORY = [
    [0.0, 1.0, 2.0, 4.0],
    [1.0, 2.0, 5.0],
    [4.0, 1.0, 2.0, 0.5],
    [3.0],
    [0.0],
    [1.0, 1.0, 2.0, 2.0, 2.0, 3.5],
    [1.0, 1.0 + 10 * EPS, 1.0 + 30 * EPS, 1.0 + 60 * EPS, 2.0],
    [0.0, 1e-300, 0.75, 0.25, 1.5],
    [0.1 * k for k in range(1, 11)],
    [10.0, 0.5, 7.25, 3.0, 0.5, 1.0],
]

WITH_STEPS = [
    ([1.0, 2.0, 4.0], 8),
    ([0.0, 1.0, 2.0, 4.0], 0),
    ([1.0, 2.0, 5.0], 0),
    ([0.5, 2.0, 3.0], 4),
    ([1.0, 1.25, 4.0], 2),
    ([2.5, 5.0, 10.0], 3),
    ([0.0, 0.3, 1.0, 1.0 + 10 * EPS, 3.0], 7),
    ([3.0, 1.0, 2.0], 1),
    ([0.25, 0.5, 0.75, 1.0, 5.0], 0),
    ([1.0, 3.0, 4.5, 6.0], 13),
]


def read(tg):
    n = len(tg)
    return {"size": n, "times": v.enc([tg[i] for i in range(n)]), "dt": v.enc([tg.dt(i) for i in range(n - 1)])}


def cases():
    st = v.settings(ql.Date(16, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for end, steps in REGULAR:
        out.append({"name": f"regular end {end!r} steps {steps}",
                    "inputs": {"settings": st, "constructor": "regular", "end": end, "steps": steps},
                    "expected": read(ql.TimeGrid(end, steps))})
    for k, times in enumerate(MANDATORY):
        out.append({"name": f"mandatory {k}",
                    "inputs": {"settings": st, "constructor": "mandatory", "times": times},
                    "expected": read(ql.TimeGrid(times))})
    for k, (times, steps) in enumerate(WITH_STEPS):
        out.append({"name": f"mandatory with steps {k}",
                    "inputs": {"settings": st, "constructor": "steps", "times": times, "steps": steps},
                    "expected": read(ql.TimeGrid(times, steps))})
    # random mandatory times on both constructors, beyond the hand-picked ones
    for k in range(3):
        times = [round(rnd.uniform(0.0, 20.0), 3) for _ in range(rnd.randint(2, 6))]
        steps = rnd.randint(1, 30)
        out.append({"name": f"random mandatory {k}",
                    "inputs": {"settings": st, "constructor": "mandatory", "times": times},
                    "expected": read(ql.TimeGrid(times))})
        out.append({"name": f"random mandatory with steps {k}",
                    "inputs": {"settings": st, "constructor": "steps", "times": times, "steps": steps},
                    "expected": read(ql.TimeGrid(times, steps))})

    refusals = [
        ("regular end 0", {"constructor": "regular", "end": 0.0, "steps": 4}, lambda: ql.TimeGrid(0.0, 4)),
        ("regular end negative", {"constructor": "regular", "end": -1.0, "steps": 4}, lambda: ql.TimeGrid(-1.0, 4)),
        ("mandatory empty", {"constructor": "mandatory", "times": []}, lambda: ql.TimeGrid([])),
        ("mandatory negative", {"constructor": "mandatory", "times": [-3.0, 1.0, 4.0, 5.0]},
         lambda: ql.TimeGrid([-3.0, 1.0, 4.0, 5.0])),
        ("steps empty", {"constructor": "steps", "times": [], "steps": 3}, lambda: ql.TimeGrid([], 3)),
        ("steps negative", {"constructor": "steps", "times": [2.0, -0.5], "steps": 3},
         lambda: ql.TimeGrid([2.0, -0.5], 3)),
        ("steps 0 with only 0", {"constructor": "steps", "times": [0.0], "steps": 0}, lambda: ql.TimeGrid([0.0], 0)),
        ("steps 0 with repeated 0", {"constructor": "steps", "times": [0.0, 0.0], "steps": 0},
         lambda: ql.TimeGrid([0.0, 0.0], 0)),
    ]
    for name, inputs, make in refusals:
        out.append({"name": name, "inputs": {"settings": st, **inputs},
                    "expected": {"size": v.attempt(lambda: len(make()))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/timegrid.hpp", cases)
