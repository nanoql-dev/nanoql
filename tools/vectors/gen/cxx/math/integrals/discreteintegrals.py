# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the discrete integrals (ql/math/integrals/discreteintegrals.hpp, .cpp), which the wheel doesn't expose,
each output computed by the C++ harness.

- DiscreteTrapezoidIntegral and DiscreteSimpsonIntegral on arrays: exp(x), sin(x) and x⁴ sampled at 0 to 9 points of
  an even grid over [0, 1] and of an uneven one (the points' squares), and QuantLib's refusal of arrays of different
  sizes ("inconsistent size").
- DiscreteTrapezoidIntegrator and DiscreteSimpsonIntegrator on five named integrands (as
  tools/cxx/src/math/integrals/trapezoidintegral.cpp names them) over four intervals, forward, reversed and empty, at
  3 to 10, 101 and 1000 evaluations, and the trapezoid at 2: the value and the evaluations counted.

Left out: the integrators below two evaluations, where QuantLib's count of intervals wraps below zero, and Simpson's
at two, where QuantLib counts f(a) twice (tests/NanoQL.Tests/Divergences/DiscreteIntegratorFewPointsTests.cs). Read
back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/Integrals/DiscreteIntegralsCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

SAMPLED = [("exp(x)", math.exp), ("sin(x)", math.sin), ("x x x x", lambda x: x * x * x * x)]
GRIDS = [("even", lambda n: [i / (n - 1) if n > 1 else 0.0 for i in range(n)]),
         ("uneven", lambda n: [(i / (n - 1)) ** 2 if n > 1 else 0.0 for i in range(n)])]
INTEGRANDS = ["exp(x)", "sin(x)", "x x x x", "1/(1+x x)", "sqrt(x)"]
INTERVALS = [(0.0, 1.0), (0.25, 3.0), (2.0, -1.0), (1.5, 1.5)]
EVALUATIONS = [3, 4, 5, 6, 7, 8, 9, 10, 101, 1000]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for gname, grid in GRIDS:
            for fname, f in SAMPLED:
                for n in range(10):
                    x = grid(n)
                    fx = [f(t) for t in x]
                    out.append({"name": f"arrays, {fname} at {n} points, {gname}",
                                "inputs": {"settings": st, "x": x, "f": fx},
                                "expected": {rule: v.attempt(lambda r=rule: H.call(f"{r}::apply", x, fx))
                                             for rule in ["DiscreteTrapezoidIntegral", "DiscreteSimpsonIntegral"]}})
        out.append({"name": "arrays of different sizes",
                    "inputs": {"settings": st, "x": [0.0, 0.5, 1.0], "f": [1.0, 2.0]},
                    "expected": {rule: v.attempt(lambda r=rule: H.call(f"{r}::apply", [0.0, 0.5, 1.0], [1.0, 2.0]))
                                 for rule in ["DiscreteTrapezoidIntegral", "DiscreteSimpsonIntegral"]}})
        for name in INTEGRANDS:
            for a, b in INTERVALS:
                if name == "sqrt(x)" and min(a, b) < 0.0:
                    continue
                expected = {}
                for rule in ["DiscreteTrapezoidIntegrator", "DiscreteSimpsonIntegrator"]:
                    counts = ([2] if rule == "DiscreteTrapezoidIntegrator" else []) + EVALUATIONS
                    results = [H.call(f"{rule}::integrate", name, a, b, n) for n in counts]
                    expected[rule] = [r["value"] for r in results]
                    expected[rule + " evaluations"] = [r["evaluations"] for r in results]
                out.append({"name": f"integrators, {name} over [{a!r}, {b!r}]",
                            "inputs": {"settings": st, "integrand": name, "a": a, "b": b,
                                       "trapezoidEvaluations": [2] + EVALUATIONS, "simpsonEvaluations": EVALUATIONS},
                            "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/discreteintegrals.hpp", cases)
