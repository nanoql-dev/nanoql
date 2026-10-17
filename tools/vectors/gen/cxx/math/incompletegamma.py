# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the incomplete gamma functions (ql/math/incompletegamma.hpp), which the wheel doesn't expose
(docs/porting-playbook.md §9.4), each output computed by the C++ harness: for ten shapes, incompleteGammaFunction,
incompleteGammaFunctionSeriesRepr and incompleteGammaFunctionContinuedFractionRepr at 30 points with the header's
default accuracy and iteration limit, among them 0 and points on both sides of a + 1, where the function switches
from the series to the continued fraction (each representation, called on the wrong side, may fail to converge,
which QuantLib refuses); the three at an explicit accuracy and iteration limit, among them limits too small to
converge; and the refusals: a shape of 0, -1 or NaN, and a negative or NaN point. A case's accuracy and maxIteration
are null where it takes the defaults. tests/NanoQL.Tests/Vectors/Cxx/Numerics/IncompleteGammaCxxVectorTests.cs
reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

NAN = float("nan")
SHAPES = [0.1, 0.5, 1.0, 1.5, 2.0, 3.0, 5.0, 10.0, 25.0, 60.0]
FUNCTIONS = {"function": "incompleteGammaFunction", "series": "incompleteGammaFunctionSeriesRepr",
             "continuedFraction": "incompleteGammaFunctionContinuedFractionRepr"}
# (a, x, accuracy, maxIteration): explicit limits, loose and tight, and too few iterations.
EXPLICIT = [(3.0, 1.0, 1e-6, 100), (3.0, 1.0, 1e-15, 100), (3.0, 7.0, 1e-6, 100), (3.0, 7.0, 1e-15, 100),
            (3.0, 1.0, 1e-13, 3), (3.0, 7.0, 1e-13, 3), (25.0, 24.0, 1e-13, 30), (25.0, 27.0, 1e-13, 10)]
REFUSED = [(0.0, 1.0), (-1.0, 1.0), (NAN, 1.0), (2.0, -1.0), (2.0, NAN), (-1.0, 0.0)]


def points(a):
    # 0, a tiny point, 24 points over (0, 3(a + 1)], a + 1 and its neighbours, and a far one.
    return ([0.0, 1e-10] + [3.0 * (a + 1.0) * (i + 1) / 24 for i in range(24)]
            + [a + 1.0 - 1e-9, a + 1.0, a + 1.0 + 1e-9, 10.0 * (a + 1.0)])


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def values(method, pairs, limits):
            return [v.attempt(lambda a=a, x=x, lim=lim: H.call(method, a, x, *lim)) for (a, x), lim in zip(pairs, limits)]

        out = []
        for a in SHAPES:
            xs = points(a)
            assert len(xs) == 30
            pairs = [(a, x) for x in xs]
            out.append({"name": f"a {a!r}",
                        "inputs": {"settings": st, "a": [a] * len(xs), "x": xs, "accuracy": None,
                                   "maxIteration": None},
                        "expected": {k: values(m, pairs, [()] * len(pairs)) for k, m in FUNCTIONS.items()}})
        pairs = [(a, x) for a, x, _, _ in EXPLICIT]
        limits = [(acc, n) for _, _, acc, n in EXPLICIT]
        out.append({"name": "explicit accuracy and iterations",
                    "inputs": {"settings": st, "a": [a for a, _ in pairs], "x": [x for _, x in pairs],
                               "accuracy": [acc for acc, _ in limits], "maxIteration": [n for _, n in limits]},
                    "expected": {k: values(m, pairs, limits) for k, m in FUNCTIONS.items()}})
        out.append({"name": "refused arguments",
                    "inputs": {"settings": st, "a": v.enc([a for a, _ in REFUSED]), "x": v.enc([x for _, x in REFUSED]),
                               "accuracy": None, "maxIteration": None},
                    "expected": {k: values(m, REFUSED, [()] * len(REFUSED)) for k, m in FUNCTIONS.items()}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/incompletegamma.hpp", cases)
