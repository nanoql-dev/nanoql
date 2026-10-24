# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of ql/math/integrals/kronrodintegral.hpp and integral.hpp that the wheel doesn't bind, each
output computed by the C++ harness: GaussKronrodNonAdaptive's absoluteError() and integrationSuccess() after an
integration, with its value and numberOfEvaluations(), on the integrands and the forward intervals of
tools/vectors/gen/math/integrals/simpsonintegral.py, at absolute and relative accuracies of 1e-6 with up to 100
evaluations, of 1e-10 with no relative accuracy, and of 1e-3 and 1e-12 with up to 30, which the 43- and 87-point rules
exceed, so integrationSuccess() fails; and relativeAccuracy() as constructed and after setRelativeAccuracy.

The adaptive rule sets no error, so its absoluteError() and integrationSuccess() read an unset member and aren't
vectored; QuantLib leaves the error unset on an empty interval too. Each case's inputs hold its settings, the rule's
accuracies and maximum, and the integrals (as simpsonintegral.py's). tests/NanoQL.Tests/Vectors/Cxx/Numerics/Integrals/
KronrodIntegralCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_simpsonintegral", os.path.join(v.GEN_ROOT, "math", "integrals", "simpsonintegral.py"))
simpson = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(simpson)

TODAY = ql.Date(15, ql.October, 2026)


def cases():
    out = []
    rows = [(name, a, b) for name, a, b in simpson.specs() if a < b]
    with cxx.connect() as H:
        for absolute, max_evaluations, relative in ((1e-6, 100, 1e-6), (1e-10, 100, 0.0), (1e-3, 30, 1e-12)):
            with H.case_settings(v.enc(TODAY)) as st:
                out.append({"name": f"accuracy {absolute!r}, relative {relative!r}, {max_evaluations} evaluations",
                            "inputs": {"settings": st, "tolerance": absolute, "maxEvaluations": max_evaluations,
                                       "relativeAccuracy": relative,
                                       "integrals": [{"f": name, "a": a, "b": b} for name, a, b in rows]},
                            "expected": {"result": [v.attempt(lambda name=name, a=a, b=b: H.call(
                                "GaussKronrodNonAdaptive::integrate", absolute, max_evaluations, relative, name, a,
                                b)) for name, a, b in rows]}})
        with H.case_settings(v.enc(TODAY)) as st:
            out.append({"name": "relativeAccuracy, set to 1e-4",
                        "inputs": {"settings": st, "tolerance": 1e-6, "maxEvaluations": 100, "relativeAccuracy": 1e-6,
                                   "newRelativeAccuracy": 1e-4},
                        "expected": {"relativeAccuracy": H.call(
                            "GaussKronrodNonAdaptive::relativeAccuracy", 1e-6, 100, 1e-6, 1e-4)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/kronrodintegral.hpp", cases)
