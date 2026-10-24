# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Boost.Math's bracketing root finders (boost/math/tools/toms748_solve.hpp, Boost 1.90) for double under
the default policy, each output computed by the C++ harness: toms748_solve on fixed functions over brackets around their
roots, at tolerances of 10 to 53 bits and call limits from 0 to 200, with the brackets Boost refuses (out of order, not
bracketing); and bracket_and_solve_root, the bracketing the generic quantile calls, from guesses on both sides of each
root, rising and falling, at factors of 1.1 to 10, among them a root near zero, which takes its escape route, and limits
too small to bracket. Each output is the bracket's two ends and the calls made. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Boost/Toms748SolveCxxVectorTests.cs through nanoql's Toms748SolveFunctions."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310

# (name, a bracket around the root, rising, guesses)
FUNCTIONS = [("x x x - 2", (0.5, 3.0), True, [0.01, 1.0, 1.25, 2.0, 100.0]),
             ("exp(x) - 3", (0.0, 2.0), True, [0.01, 0.5, 1.0986, 3.0, 50.0]),
             ("log(x) - 1", (1.0, 10.0), True, [0.001, 1.0, 2.7, 3.0, 1e6]),
             ("2 - x x", (0.5, 3.0), False, [0.01, 1.0, 1.5, 4.0, 1e3]),
             ("1/x - 3", (0.1, 1.0), False, [0.001, 0.2, 0.34, 2.0, 100.0]),
             ("x - 1e-300", (0.0, 1.0), True, [1.0, 1e-100, 1e-299]),
             ("atan(x) - 1.5", (1.0, 1e3), True, [0.5, 10.0, 14.1, 20.0, 1e4]),
             ("x + 0.5", (-2.0, 1.0), True, [-1.0, -0.25, -10.0]),
             ("1 - exp(x)", (-1.0, 2.0), False, [-1.0, 0.5, 1e-310])]
BITS = [10, 25, 40, 50, 53]
LIMITS = [0, 1, 2, 3, 5, 10, 200]
FACTORS = [1.1, 2.0, 10.0]


def cases():
    with cxx.connect() as H:
        st = H.settings(R)
        solve, bracket = [], []
        for name, (a, b), rising, guesses in FUNCTIONS:
            solve += [(name, a, b, bits, 200) for bits in BITS] + [(name, a, b, 50, n) for n in LIMITS]
            solve += [(name, b, a, 50, 200), (name, b, b + 1.0, 50, 200)]
            bracket += [(name, g, f, rising, 50, 200) for g in guesses for f in FACTORS]
            bracket += [(name, guesses[0], 2.0, rising, 53, n) for n in [1, 3, 10]]
            bracket += [(name, guesses[0], 2.0, not rising, 50, 50)]
        return [
            {"name": "toms748_solve",
             "inputs": {"settings": st, "solve": [{"f": f, "a": a, "b": b, "bits": bits, "maxIter": n}
                                                  for f, a, b, bits, n in solve]},
             "expected": {"result": [v.attempt(lambda s=s: H.call("toms748::toms748_solve", *s)) for s in solve]}},
            {"name": "bracket_and_solve_root",
             "inputs": {"settings": st, "bracket": [{"f": f, "guess": g, "factor": fac, "rising": r, "bits": bits,
                                                     "maxIter": n} for f, g, fac, r, bits, n in bracket]},
             "expected": {"result": [v.attempt(lambda s=s: H.call("toms748::bracket_and_solve_root", *s))
                                     for s in bracket]}},
        ]


if __name__ == "__main__":
    v.main(__file__, "boost/math/tools/toms748_solve.hpp", cases)
