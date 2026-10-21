# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmSnapshotCondition (ql/methods/finitedifferences/stepconditions/fdmsnapshotcondition.hpp,
.cpp): snapshots at times 0, 0.5 and 1, each its time; its values before any applyTo (empty), after applyTo at another
time (still empty), after applyTo at its time (the array, which applyTo leaves as it is), and after a second applyTo at
its time with another array (that array).

The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmSnapshotCondition(t), the C++ constructor; getTime, getValues and
applyTo the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/StepConditions/FdmSnapshotConditionVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bsm = _load("fdmblackscholesmesher", "methods/finitedifferences/meshers/fdmblackscholesmesher.py")


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def cases():
    st = v.settings(bsm.R)
    rng = random.Random(20261026)
    out = []
    for t in (0.0, 0.5, 1.0):
        a = [rng.uniform(-5.0, 5.0) for _ in range(7)]
        b = [rng.uniform(-5.0, 5.0) for _ in range(9)]
        s = ql.FdmSnapshotCondition(t)
        before = arr(s.getValues())
        x = ql.Array(a)
        s.applyTo(x, t + 0.25)
        other = arr(s.getValues())
        s.applyTo(x, t)
        own, left = arr(s.getValues()), arr(x)
        s.applyTo(ql.Array(b), t)
        out.append({"name": f"snapshot at {t}", "inputs": {"settings": st, "t": t, "a": a, "b": b, "other": t + 0.25},
                    "expected": {"time": s.getTime(), "before": before, "afterOther": other, "afterOwn": own,
                                 "arrayAfterOwn": left, "afterSecond": arr(s.getValues())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/stepconditions/fdmsnapshotcondition.hpp", cases)
