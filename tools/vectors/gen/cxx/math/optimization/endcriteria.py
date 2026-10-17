# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for EndCriteria (ql/math/optimization/endcriteria.hpp, .cpp), each output computed by the C++ harness,
since the wheel exposes only its constructor and succeeded (docs/porting-playbook.md §9.4): the constructor's
inspectors, its defaults for a null maximum of stationary iterations (half the iterations, at most 100) and a null
gradient-norm epsilon (the function epsilon), and its refusals; each check, and operator(), over about fifteen inputs
each, with the stationary count before and after and the criterion met (each check starting from ecType None, which
QuantLib leaves unchanged where its criterion isn't met), NaN inputs among them; succeeded and the stream text of
each EndCriteria::Type.

End criteria are a spec (tools/cxx/src/math/optimization/specs.hpp).
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/EndCriteriaCxxVectorTests.cs reads the file back."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

NAN = math.nan


def criteria(max_iterations, max_stationary, root, function, gradient):
    return {"maxIterations": max_iterations, "maxStationaryStateIterations": max_stationary, "rootEpsilon": root,
            "functionEpsilon": function, "gradientNormEpsilon": gradient}


CONSTRUCTED = [
    criteria(10, 3, 1e-6, 1e-8, 1e-7), criteria(1000, None, 1e-8, 1e-8, 1e-8), criteria(250, None, 1e-8, 1e-8, None),
    criteria(201, None, 0.0, 0.0, 0.0), criteria(199, None, 1e-4, 1e-5, 1e-6), criteria(10, None, 1e-8, 1e-8, 1e-8),
    criteria(5, 2, -1.0, NAN, None), criteria(3, None, 1e-8, 1e-8, 1e-8), criteria(10, 1, 1e-8, 1e-8, 1e-8),
    criteria(10, 10, 1e-8, 1e-8, 1e-8), criteria(10, 12, 1e-8, 1e-8, 1e-8), criteria(0, None, 1e-8, 1e-8, 1e-8),
]
INSPECTORS = ("maxIterations", "maxStationaryStateIterations", "rootEpsilon", "functionEpsilon",
              "gradientNormEpsilon")
CHECKED = criteria(10, 3, 1e-6, 1e-8, 1e-7)
NULL_GRADIENT = criteria(10, 3, 1e-6, 1e-8, None)
ITERATIONS = list(range(0, 13)) + [100, 2147483647]
# (old, new, stationary count): changes below, at and above the epsilon, counts below, at and above the maximum.
STATIONARY = [
    (1.0, 1.0, 0), (1.0, 1.0, 2), (1.0, 1.0, 3), (1.0, 1.0, 4), (1.0, 1.0 + 1e-9, 1), (0.0, 1e-6, 3),
    (0.0, 1e-8, 3), (0.0, -1e-9, 3), (5.0, 4.0, 3), (0.0, 1e-7, 2), (NAN, 1.0, 3), (1.0, NAN, 0),
    (2.0, 2.0 + 5e-9, 2), (-3.0, -3.0, 7), (1e300, -1e300, 3),
]
ACCURACY = [(f, positive) for f in (0.0, 5e-9, 1e-8, 2e-8, -1.0, NAN, 1e-3) for positive in (False, True)]
GRADIENT_NORMS = [0.0, 1e-9, 5e-8, 1e-8, 1e-7, 2e-7, 1.0, -1.0, NAN, 1e300, 9.99999e-8, 1.0000001e-7, 3e-8, 1e-6]
# (iteration, stationary count, positive, fOld, normGOld, fNew, normGNew)
OPERATOR = [
    (10, 2, True, 1.0, 1.0, 0.5, 1.0), (11, 0, False, 1.0, 1.0, 1.0, 1.0), (0, 0, False, 1.0, 1.0, 1.0, 1.0),
    (1, 3, False, 1.0, 1.0, 1.0, 1.0), (1, 2, False, 1.0, 1.0, 1.0, 1.0), (1, 2, False, 1.0, 1.0, 0.5, 1.0),
    (1, 0, True, 1.0, 1.0, 5e-9, 1.0), (1, 0, False, 1.0, 1.0, 5e-9, 1.0), (1, 0, True, 1.0, 1.0, 0.5, 5e-8),
    (1, 0, False, 1.0, 1.0, 0.5, 1e-9), (1, 1, False, 1.0, 1.0, 1.0, 1e-9), (5, 3, True, 2e-9, 1.0, 1e-9, 1e-9),
    (9, 2, True, NAN, 1.0, NAN, NAN), (2, 5, False, 0.25, 0.0, 0.25, 0.5), (3, 0, True, 1.0, 1e-9, 2.0, 0.5),
]
TYPES = list(range(8))


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        def inspectors(spec):
            r = call("EndCriteria", spec)
            return r if "throws" in r else [r[k] for k in INSPECTORS]

        def columns(results, keys):
            return {k: [r[k] for r in results] for k in keys}

        out = [{"name": "constructor and inspectors",
                "inputs": {"settings": st, "criteria": v.enc(CONSTRUCTED)},
                "expected": {"inspectors": [inspectors(c) for c in CONSTRUCTED]}}]
        r = [H.call("EndCriteria::checkMaxIterations", CHECKED, i) for i in ITERATIONS]
        out.append({"name": "checkMaxIterations", "inputs": {"settings": st, "criteria": CHECKED,
                                                             "iteration": ITERATIONS},
                    "expected": columns(r, ("result", "ecType"))})
        for method in ("checkStationaryPoint", "checkStationaryFunctionValue"):
            r = [v.enc(H.call(f"EndCriteria::{method}", CHECKED, *s)) for s in STATIONARY]
            out.append({"name": method, "inputs": {"settings": st, "criteria": CHECKED, "inputs": v.enc(STATIONARY)},
                        "expected": columns(r, ("result", "statStateIterations", "ecType"))})
        r = [H.call("EndCriteria::checkStationaryFunctionAccuracy", CHECKED, *a) for a in ACCURACY]
        out.append({"name": "checkStationaryFunctionAccuracy",
                    "inputs": {"settings": st, "criteria": CHECKED, "inputs": v.enc(ACCURACY)},
                    "expected": columns(r, ("result", "ecType"))})
        for label, spec in (("checkZeroGradientNorm", CHECKED), ("checkZeroGradientNorm, null epsilon", NULL_GRADIENT)):
            r = [H.call("EndCriteria::checkZeroGradientNorm", spec, g) for g in GRADIENT_NORMS]
            out.append({"name": label, "inputs": {"settings": st, "criteria": spec, "gradientNorm": v.enc(GRADIENT_NORMS)},
                        "expected": columns(r, ("result", "ecType"))})
        r = [v.enc(H.call("EndCriteria::operator()", CHECKED, *o)) for o in OPERATOR]
        out.append({"name": "operator()", "inputs": {"settings": st, "criteria": CHECKED, "inputs": v.enc(OPERATOR)},
                    "expected": columns(r, ("result", "statStateIterations", "ecType"))})
        out.append({"name": "succeeded and the stream text", "inputs": {"settings": st, "types": TYPES},
                    "expected": {"succeeded": [H.call("EndCriteria::succeeded", t) for t in TYPES],
                                 "text": [H.call("operator<<(EndCriteria::Type)", t) for t in TYPES]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/endcriteria.hpp", cases)
