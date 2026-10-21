# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmStepConditionComposite::conditions
(ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp, .cpp), which the wheel binds but can't
return (its list of conditions aborts the interpreter), each output computed by the C++ harness
(docs/porting-playbook.md §9.4): composites of snapshot conditions under unsorted, repeated stopping times, none among
them, and joinConditions of a snapshot and such a composite; each the stopping times and the conditions in order, a
snapshot by its time and a composite by its number of conditions.

The harness's call is tools/cxx/src/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.cpp's. Read back
by tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/StepConditions/FdmStepConditionCompositeCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

# (name, stopping times, snapshot times, the joined snapshot's time or None)
CASES = [
    ("no condition", [[]], [], None),
    ("one snapshot", [[0.5]], [0.5], None),
    ("snapshots under unsorted, repeated times", [[0.7, 0.2], [0.4, 0.2, 0.9]], [0.4, 0.1, 0.7], None),
    ("joined to a snapshot", [[0.25, 0.75]], [0.25, 0.75], 0.5),
    ("joined to an empty composite", [[]], [], 0.3),
]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for name, times, snapshots, join in CASES:
            r = H.call("FdmStepConditionComposite::conditions", times, snapshots, join)
            out.append({"name": name,
                        "inputs": {"settings": st, "stoppingTimes": times, "snapshots": snapshots, "join": join},
                        "expected": {"stoppingTimes": r["stoppingTimes"], "conditions": r["conditions"]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/stepconditions/fdmstepconditioncomposite.hpp", cases)
