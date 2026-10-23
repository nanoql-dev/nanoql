# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for SegmentIntegral (ql/math/integrals/segmentintegral.hpp, .cpp, through ql/math/integrals/integral.hpp,
.cpp): the trapezoid rule on a fixed number of intervals, the integral and the number of evaluations
(numberOfEvaluations() after the call, which the rule never counts) of seven of
tools/vectors/gen/math/integrals/simpsonintegral.py's named integrands over its intervals, forward, reversed and
empty, on 1, 2, 7, 10, 100 and 1,000 intervals; and the refusal of no interval ("at least 1 interval needed, 0
given"). The wheel's class is a SWIG wrapper of the C++ class, with no logic of its own, calling the Python integrand.
Read back by tests/NanoQL.Tests/Vectors/Numerics/Integrals/SegmentIntegralVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_simpsonintegral", os.path.join(v.GEN_ROOT, "math", "integrals", "simpsonintegral.py"))
simpson = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(simpson)

INTERVALS = [1, 2, 7, 10, 100, 1000]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for intervals in INTERVALS:
        rows = simpson.specs()
        integrator = ql.SegmentIntegral(intervals)
        out.append({"name": f"{intervals} intervals",
                    "inputs": {"settings": st, "intervals": intervals,
                               "integrals": [{"f": name, "a": a, "b": b} for name, a, b in rows]},
                    "expected": {"result": [simpson.run(integrator, name, a, b) for name, a, b in rows]}})
    out.append({"name": "no interval",
                "inputs": {"settings": st, "intervals": 0, "integrals": []},
                "expected": {"construction": v.attempt(lambda: ql.SegmentIntegral(0) and 0)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/segmentintegral.hpp", cases)
