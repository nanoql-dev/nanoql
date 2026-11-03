# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's model parameters (ql/models/parameter.hpp): ConstantParameter from a value and from a
constraint alone, NullParameter and PiecewiseConstantParameter, under the five constraints of
tools/vectors/gen/math/optimization/constraint.py.

- ConstantParameter(value, constraint): ten values (inside, on and outside the bounds, 0, a denormal, NaN and the
  infinities) under each constraint; the constructor's refusal of a value its constraint refuses ("<value>: invalid
  value") recorded in "built".
- ConstantParameter(constraint), whose value QuantLib leaves uninitialized (the wheel survey's caveat 5): each read
  after setParam(0, x) has set it.
- NullParameter(): no parameter, the value 0.
- PiecewiseConstantParameter(times[, constraint]): seven sets of breakpoints (none, one, repeated, negative, unsorted
  never: std::upper_bound needs them sorted), with and without the constraint argument, each parameter set by
  setParam(i, x) before any read, as the constraint-only constant parameter's; its value at every breakpoint (which
  takes the next piece), between them, before the first, after the last, at NaN (the last piece, by upper_bound's
  comparison) and at the infinities.
- setParam's own behaviour: a constant parameter set past its constraint, which setParam doesn't check, and set twice.

For each built parameter, after the case's setParam calls in order: size(), params(), its value at the case's times
(operator(), the wheel's __call__, nanoql's Evaluate) and testParams at the case's arrays, of its size and others
(NonhomogeneousBoundaryConstraint refuses an array of another size than its bounds). setParam is called only within the
parameter's size: out of range, QuantLib writes past its array, and on a NullParameter it ends the process (caveat 13;
nanoql refuses). Parameter() isn't public in nanoql, and the wheel's TermStructureFittingParameter isn't bound:
Hull-White's harness vectors reach it through the fitted tree.

The wheel's calls, by QuantLib-SWIG's parameter.i: the classes' constructors, params(), setParam(i, x), size(),
testParams(Array) and __call__(t), each forwarding to the C++ member of that name. Each case's inputs hold its settings,
its "parameter" ({"kind": "ConstantParameter", "value" (null for the constraint-only constructor), "constraint"},
{"kind": "NullParameter"} or {"kind": "PiecewiseConstantParameter", "times", "constraint" (null for the default)}),
the constraint a spec of constraint.py's, the "set" calls ([i, x] pairs), the "times" and the "arrays". Read back by
tests/NanoQL.Tests/Vectors/Models/ParameterVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

NAN, INF = math.nan, math.inf

NO = {"type": "NoConstraint"}
POSITIVE = {"type": "PositiveConstraint"}
BOUNDARY = {"type": "BoundaryConstraint", "low": -1.0, "high": 0.75}
COMPOSITE = {"type": "CompositeConstraint", "c1": POSITIVE, "c2": {"type": "BoundaryConstraint", "low": -2.0,
                                                                     "high": 0.8}}
NONHOMOGENEOUS = {"type": "NonhomogeneousBoundaryConstraint", "low": [0.0], "high": [0.8]}
CONSTRAINTS = [NO, POSITIVE, BOUNDARY, COMPOSITE, NONHOMOGENEOUS]

VALUES = [0.5, 0.75, -0.3, 0.0, 5e-324, 2.0, -5.0, NAN, INF, -INF]
TIMES = [0.0, 1.0, -1.0, 1e10, NAN]
ARRAYS_1 = [[0.5], [0.75], [-0.3], [0.0], [NAN], [2.0], [], [0.25, 0.5]]


def make_constraint(spec):
    t = spec["type"]
    if t == "NoConstraint":
        return ql.NoConstraint()
    if t == "PositiveConstraint":
        return ql.PositiveConstraint()
    if t == "BoundaryConstraint":
        return ql.BoundaryConstraint(spec["low"], spec["high"])
    if t == "CompositeConstraint":
        return ql.CompositeConstraint(make_constraint(spec["c1"]), make_constraint(spec["c2"]))
    return ql.NonhomogeneousBoundaryConstraint(ql.Array(spec["low"]), ql.Array(spec["high"]))


def make_parameter(spec):
    kind = spec["kind"]
    if kind == "ConstantParameter":
        c = make_constraint(spec["constraint"])
        return ql.ConstantParameter(c) if spec["value"] is None else ql.ConstantParameter(spec["value"], c)
    if kind == "NullParameter":
        return ql.NullParameter()
    if spec["constraint"] is None:
        return ql.PiecewiseConstantParameter(spec["times"])
    return ql.PiecewiseConstantParameter(spec["times"], make_constraint(spec["constraint"]))


def case(st, name, spec, sets, times, arrays):
    expected = {}
    try:
        p = make_parameter(spec)
    except RuntimeError:
        expected["built"] = dict(v.THROWS)
    else:
        expected["built"] = True
        for i, x in sets:
            assert 0 <= i < p.size(), (name, i)
            p.setParam(i, x)
        expected.update({"size": p.size(), "params": v.enc(list(p.params())),
                         "evaluate": [v.enc(p(t)) for t in times],
                         "testParams": [v.attempt(lambda a=a: p.testParams(ql.Array(a))) for a in arrays]})
    return {"name": name,
            "inputs": {"settings": st, "parameter": v.enc(spec), "set": v.enc([list(s) for s in sets]),
                       "times": v.enc(times), "arrays": v.enc(arrays)},
            "expected": expected}


def constant(value, constraint):
    return {"kind": "ConstantParameter", "value": value, "constraint": constraint}


def piecewise(times, constraint=None):
    return {"kind": "PiecewiseConstantParameter", "times": times, "constraint": constraint}


BREAKPOINTS = [
    [],
    [1.0],
    [1.0, 2.0],
    [0.5, 1.0, 1.0, 3.0],
    [-1.0, 0.0, 2.5],
    [0.25, 0.5, 1.0, 2.0, 5.0, 10.0],
    [1e-300, 1e300],
]


def piecewise_times(times):
    """Each breakpoint, the midpoints between them, a time before the first and after the last, NaN and the
    infinities."""
    out = list(times)
    out += [0.5 * (a + b) for a, b in zip(times, times[1:])]
    out += [(times[0] - 1.0) if times else 0.0, (times[-1] + 1.0) if times else 1.0, NAN, INF, -INF]
    return out


def piecewise_arrays(n):
    return [[0.1 * (k + 1) for k in range(n)], [-0.1] + [0.2] * (n - 1), [0.5] * (n + 1), []]


def cases():
    st = v.settings(ql.Date(16, ql.October, 2026))
    out = []
    for k, c in enumerate(CONSTRAINTS):
        for x in VALUES:
            out.append(case(st, f"constant {v.enc(x)}, constraint {k}", constant(x, c), [], TIMES, ARRAYS_1))
    for k, c in enumerate(CONSTRAINTS):
        out.append(case(st, f"constant from constraint {k}, set to 0.6", constant(None, c), [(0, 0.6)], TIMES,
                        ARRAYS_1))
        out.append(case(st, f"constant from constraint {k}, set to -4", constant(None, c), [(0, -4.0)], TIMES,
                        ARRAYS_1))
    out.append(case(st, "constant set past its constraint", constant(0.5, POSITIVE), [(0, -2.0)], TIMES, ARRAYS_1))
    out.append(case(st, "constant set twice", constant(0.5, BOUNDARY), [(0, 0.1), (0, NAN)], TIMES, ARRAYS_1))
    out.append(case(st, "null", {"kind": "NullParameter"}, [], TIMES, ARRAYS_1))
    for k, times in enumerate(BREAKPOINTS):
        n = len(times) + 1
        sets = [(i, 0.01 * (i + 1) * (-1) ** i) for i in range(n)]
        for name, c in (("default", None), ("no constraint", NO), ("positive", POSITIVE),
                        ("boundary", BOUNDARY)):
            out.append(case(st, f"piecewise {k}, {name}", piecewise(times, c), sets, piecewise_times(times),
                            piecewise_arrays(n)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/parameter.hpp", cases)
