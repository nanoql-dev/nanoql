# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Halley solver (ql/math/solvers1d/halley.hpp), which the wheel doesn't expose, each output
computed by the C++ harness (docs/porting-playbook.md §9.4): the roots of polynomials and exponentials with their first
and second derivatives, from guesses with a step (QuantLib's bracketing first) and in brackets, at accuracies from
1e-4 to 1e-12, steps that leave the bracket (QuantLib's fallback to NewtonSafe), and too few evaluations (refused).

The harness's calls are tools/cxx/src/math/solvers1d/halley.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Math/Solvers1D/HalleyCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

# (name, function, accuracy, guess, step or None, bracket or None, maxEvaluations)
CASES = [
    ("x^2 - 1 from 0.5", ["polynomial", -1.0, 0.0, 1.0], 1e-8, 0.5, 0.1, None, 100),
    ("x^2 - 1 from 1.5, bracketed", ["polynomial", -1.0, 0.0, 1.0], 1e-10, 1.5, None, [0.0, 2.0], 100),
    ("1 - x^2 from 0.01", ["polynomial", 1.0, 0.0, -1.0], 1e-6, 0.01, 0.1, None, 100),
    ("cubic", ["polynomial", -6.0, 11.0, -6.0, 1.0], 1e-12, 0.4, 0.05, None, 100),
    ("cubic, bracketed past a turning point", ["polynomial", -6.0, 11.0, -6.0, 1.0], 1e-9, 2.6, None, [2.5, 3.6],
     100),
    ("quartic", ["polynomial", -2.0, 0.0, 0.0, 0.0, 1.0], 1e-10, 1.0, 0.1, None, 100),
    ("exponential", ["exponential", 2.0, 0.5, 3.0], 1e-10, 0.0, 0.5, None, 100),
    ("steep exponential, bracketed", ["exponential", 1.0, 8.0, 100.0], 1e-8, 0.2, None, [0.0, 1.0], 100),
    ("flat start, leaves the bracket", ["polynomial", -1.0, 0.0, 1.0], 1e-8, 0.001, None, [0.0, 3.0], 100),
    ("too few evaluations (refused)", ["exponential", 1.0, 8.0, 100.0], 1e-14, 0.2, None, [0.0, 1.0], 2),
    ("accuracy 1e-4", ["polynomial", -2.0, 0.0, 1.0], 1e-4, 1.0, 0.1, None, 100),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        for name, fn, accuracy, guess, step, bracket, max_evaluations in CASES:
            if bracket is None:
                root = v.attempt(lambda: H.call("Halley::solve", fn, accuracy, guess, step, max_evaluations))
            else:
                root = v.attempt(lambda: H.call("Halley::solve(bracketed)", fn, accuracy, guess, bracket[0],
                                                bracket[1], max_evaluations))
            out.append({"name": name,
                        "inputs": {"settings": st, "function": fn, "accuracy": accuracy, "guess": guess,
                                   "step": step, "bracket": bracket, "maxEvaluations": max_evaluations},
                        "expected": {"root": root}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/solvers1d/halley.hpp", cases)
