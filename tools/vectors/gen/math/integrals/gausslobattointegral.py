# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for GaussLobattoIntegral (ql/math/integrals/gausslobattointegral.hpp, .cpp, through
ql/math/integrals/integral.hpp, .cpp): the integral and the number of evaluations (numberOfEvaluations() after the
call) of tools/vectors/gen/math/integrals/simpsonintegral.py's named integrands over its intervals, forward, reversed
and empty, under absolute accuracies 1e-6 and 1e-10, with and without a relative accuracy of 1e-8, with and without the
convergence estimate, and with a budget of 30 evaluations, too few for most, which QuantLib refuses ("max number of
iterations reached"); the refusal of x on [-1, 1], whose 13-point Kronrod estimate is exactly 0 while its points
aren't ("can not calculate absolute accuracy from relative accuracy"); and the refusal of an accuracy not above
machine epsilon. The wheel's class is a SWIG wrapper of the C++ class, with no logic of its own, calling the Python
integrand. Read back by tests/NanoQL.Tests/Vectors/Numerics/Integrals/GaussLobattoIntegralVectorTests.cs."""
import importlib.util
import os
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


si = _load("simpsonintegral", "math/integrals/simpsonintegral.py")
SETTINGS = [(1000, 1e-6, None, True), (1000, 1e-10, None, True), (1000, 1e-6, 1e-8, True), (1000, 1e-10, 1e-8, False),
            (1000, 1e-6, None, False), (30, 1e-10, None, True)]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for max_iterations, abs_accuracy, rel_accuracy, estimate in SETTINGS:
        rows = si.specs() + [("x", -1.0, 1.0)]
        integrator = (ql.GaussLobattoIntegral(max_iterations, abs_accuracy) if rel_accuracy is None and estimate
                      else ql.GaussLobattoIntegral(max_iterations, abs_accuracy,
                                                   ql.nullDouble() if rel_accuracy is None else rel_accuracy, estimate))
        out.append({"name": f"{max_iterations} evaluations, absolute {abs_accuracy!r}, relative {rel_accuracy!r}, "
                            f"estimate {estimate}",
                    "inputs": {"settings": st, "maxIterations": max_iterations, "absAccuracy": abs_accuracy,
                               "relAccuracy": rel_accuracy, "useConvergenceEstimate": estimate,
                               "integrals": [{"f": name, "a": a, "b": b} for name, a, b in rows]},
                    "expected": {"result": [si.run(integrator, name, a, b) for name, a, b in rows]}})
    out.append({"name": "an accuracy not above machine epsilon",
                "inputs": {"settings": st, "maxIterations": 1000, "absAccuracy": 1e-17, "relAccuracy": None,
                           "useConvergenceEstimate": True, "integrals": []},
                "expected": {"construction": v.attempt(lambda: ql.GaussLobattoIntegral(1000, 1e-17) and 0)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/gausslobattointegral.hpp", cases)
