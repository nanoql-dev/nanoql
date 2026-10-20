# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the trapezoid's refinements (ql/math/integrals/trapezoidintegral.hpp), which the wheel doesn't expose,
each output computed by the C++ harness: Default::integrate's iterates, eight refinements from the trapezoid's estimate
with the number of intervals doubling, and MidPoint::integrate's, six from the midpoint's estimate with the number
tripling, over tools/vectors/gen/math/integrals/simpsonintegral.py's named integrands and intervals, forward, reversed
and empty; and each rule's nbEvalutions. Each refinement's estimate and count are inputs, so the file holds the rules'
arithmetic whatever the integrator starts from: nanoql starts TrapezoidIntegral<MidPoint> from the midpoint's estimate,
where QuantLib starts it from the trapezoid's (Divergences/TrapezoidMidPointTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Integrals/TrapezoidIntegralCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


si = _load("simpsonintegral", "math/integrals/simpsonintegral.py")

# (rule, refinements, the start's estimate)
RULES = [("Default", 8, lambda f, a, b: (f(a) + f(b)) * (b - a) / 2.0),
         ("MidPoint", 6, lambda f, a, b: (b - a) * f((a + b) / 2.0))]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for rule, steps, start in RULES:
            nb = H.call(f"{rule}::nbEvalutions")
            for name, a, b in si.specs():
                estimates, counts, results = [], [], []
                I, N = start(si.INTEGRANDS[name], a, b), 1
                for _ in range(steps):
                    estimates.append(I)
                    counts.append(N)
                    I = H.call(f"{rule}::integrate", name, a, b, I, N)
                    results.append(I)
                    N *= nb
                out.append({"name": f"{rule}, {name} over [{a!r}, {b!r}]",
                            "inputs": {"settings": st, "rule": rule, "f": name, "a": a, "b": b, "I": estimates,
                                       "N": counts},
                            "expected": {"integrate": results}})
            out.append({"name": f"{rule}, nbEvalutions",
                        "inputs": {"settings": st, "rule": rule, "f": None, "a": None, "b": None, "I": [], "N": []},
                        "expected": {"nbEvalutions": nb}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/trapezoidintegral.hpp", cases)
