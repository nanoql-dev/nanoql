# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for TrapezoidIntegral<Default> (ql/math/integrals/trapezoidintegral.hpp, through
ql/math/integrals/integral.hpp, .cpp), which the wheel exposes as TrapezoidIntegralDefault: the integral and the number
of evaluations (numberOfEvaluations() after the call) of tools/vectors/gen/math/integrals/simpsonintegral.py's named
integrands over its intervals, forward, reversed and empty, at accuracies 1e-6 and 1e-8 and up to 100 refinements; six
or seven refinements allowed, too few for the six QuantLib always takes, which it refuses ("max number of iterations
reached"); and the refusal of an accuracy not above machine epsilon. TrapezoidIntegral<MidPoint> isn't vectored here:
nanoql starts it from its own midpoint estimate, where QuantLib starts it from the trapezoid's
(Divergences/TrapezoidMidPointTests.cs); its refinement, MidPoint::integrate, and Default's are vectored through the C++
harness (tools/vectors/gen/cxx/math/integrals/trapezoidintegral.py). The wheel's class is a SWIG wrapper of the C++
class, with no logic of its own, calling the Python integrand. Read back by
tests/NanoQL.Tests/Vectors/Numerics/Integrals/TrapezoidIntegralVectorTests.cs."""
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


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for accuracy, max_iterations in ((1e-6, 100), (1e-8, 100), (1e-6, 6), (1e-6, 7)):
        rows = si.specs()
        integrator = ql.TrapezoidIntegralDefault(accuracy, max_iterations)
        out.append({"name": f"accuracy {accuracy!r}, {max_iterations} refinements",
                    "inputs": {"settings": st, "accuracy": accuracy, "maxIterations": max_iterations,
                               "integrals": [{"f": name, "a": a, "b": b} for name, a, b in rows]},
                    "expected": {"result": [si.run(integrator, name, a, b) for name, a, b in rows]}})
    out.append({"name": "an accuracy not above machine epsilon",
                "inputs": {"settings": st, "accuracy": 1e-17, "maxIterations": 100, "integrals": []},
                "expected": {"construction": v.attempt(lambda: ql.TrapezoidIntegralDefault(1e-17, 100) and 0)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/trapezoidintegral.hpp", cases)
