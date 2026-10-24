# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the Gauss-Kronrod rules (ql/math/integrals/kronrodintegral.hpp, .cpp, through
ql/math/integrals/integral.hpp and .cpp): GaussKronrodAdaptive and GaussKronrodNonAdaptive on the named integrands and
intervals of tools/vectors/gen/math/integrals/simpsonintegral.py, forward, reversed and empty, each integral with its
number of evaluations (numberOfEvaluations() after the call). The adaptive rule at tolerances 1e-6 and 1e-10 with up to
1,000 and 100,000 evaluations; with 15 and 45 allowed, which most integrands need more than, QuantLib refusing ("maximum
number of function evaluations exceeded"); and the refusals of fewer than 15 evaluations and of an accuracy not above
machine epsilon. The non-adaptive rule at absolute and relative accuracies of 1e-6, at 1e-10 with no relative accuracy,
and at 1e-3 with 1e-12, its count 21, 43 or 87 whatever its maximum, which only integrationSuccess() reads; and the
refusal of an accuracy not above machine epsilon. Tolerances the adaptive rule can't reach, which overflow QuantLib's
recursion on a default stack, are left out (tests/NanoQL.Tests/Divergences/KronrodAdaptiveDepthTests.cs). The rules'
error estimates and integrationSuccess(), which the wheel doesn't bind, are the harness's
(tools/vectors/gen/cxx/math/integrals/kronrodintegral.py). The wheel's classes are SWIG wrappers of the C++ classes,
with no logic of their own, calling the Python integrand. Read back by
tests/NanoQL.Tests/Vectors/Numerics/Integrals/KronrodIntegralVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _dir)
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import simpsonintegral as simpson  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    rows = simpson.specs()
    for tolerance, max_evaluations in ((1e-6, 1000), (1e-6, 100000), (1e-10, 100000), (1e-6, 15), (1e-6, 45)):
        integrator = ql.GaussKronrodAdaptive(tolerance, max_evaluations)
        out.append({"name": f"adaptive, tolerance {tolerance!r}, {max_evaluations} evaluations",
                    "inputs": {"settings": st, "rule": "adaptive", "tolerance": tolerance,
                               "maxEvaluations": max_evaluations,
                               "integrals": [{"f": name, "a": a, "b": b} for name, a, b in rows]},
                    "expected": {"result": [simpson.run(integrator, name, a, b) for name, a, b in rows]}})
    for tolerance, max_evaluations, label in ((1e-6, 14, "fewer than 15 evaluations"),
                                              (1e-17, 1000, "an accuracy not above machine epsilon")):
        out.append({"name": f"adaptive, {label}",
                    "inputs": {"settings": st, "rule": "adaptive", "tolerance": tolerance,
                               "maxEvaluations": max_evaluations, "integrals": []},
                    "expected": {"construction": v.attempt(
                        lambda t=tolerance, m=max_evaluations: ql.GaussKronrodAdaptive(t, m) and 0)}})
    for absolute, max_evaluations, relative in ((1e-6, 100, 1e-6), (1e-10, 100, 0.0), (1e-3, 1000000, 1e-12)):
        integrator = ql.GaussKronrodNonAdaptive(absolute, max_evaluations, relative)
        out.append({"name": f"non-adaptive, accuracy {absolute!r}, relative {relative!r}",
                    "inputs": {"settings": st, "rule": "nonAdaptive", "tolerance": absolute,
                               "maxEvaluations": max_evaluations, "relativeAccuracy": relative,
                               "integrals": [{"f": name, "a": a, "b": b} for name, a, b in rows]},
                    "expected": {"result": [simpson.run(integrator, name, a, b) for name, a, b in rows]}})
    out.append({"name": "non-adaptive, an accuracy not above machine epsilon",
                "inputs": {"settings": st, "rule": "nonAdaptive", "tolerance": 1e-17, "maxEvaluations": 100,
                           "relativeAccuracy": 1e-6, "integrals": []},
                "expected": {"construction": v.attempt(lambda: ql.GaussKronrodNonAdaptive(1e-17, 100, 1e-6) and 0)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/kronrodintegral.hpp", cases)
