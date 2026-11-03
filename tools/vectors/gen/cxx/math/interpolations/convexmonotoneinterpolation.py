# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ConvexMonotoneInterpolation (ql/math/interpolations/convexmonotoneinterpolation.hpp), each
output computed by the C++ harness: what the wheel's interpolation doesn't take or return, its primitive, its flat
final period, and the local interpolation over a previous one's sections.

Five shapes of section averages on nine nodes from 0 to 10 (rising, humped, falling, wavy, and flat runs, which reach
the constant-gradient sections), all positive with end forwards above 0, under seven settings of quadraticity,
monotonicity and positivity (QuantLib's default 0.3, 0.7 and forced; 1 and 0.5 with positivity forced; 0 with it not
forced, at monotonicities of 0.7 and 1; 0.3 at monotonicity 0; and 0.7 at 0.5), each built whole, with and without a
flat final period, and built as the local bootstrap builds it, at localisations of 2 and 3: the values and primitives
at the nodes, the sections' quarter points, and points below and above the range, extrapolated. Quadraticity 0 with
positivity forced and negative averages are left out: there nanoql's fixes depart from QuantLib's sections
(tests/NanoQL.Tests/Divergences/ConvexMonotoneTests.cs). A case's inputs are "x", "y", "quadraticity",
"monotonicity", "forcePositive", "form" ("whole" with "flatFinalPeriod", or "local" with "localisation") and
"points"; a local case's outputs hold one array per step. The harness unit is
tools/cxx/src/math/interpolations/convexmonotoneinterpolation.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Math/Interpolations/ConvexMonotoneInterpolationCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

X = [0.0, 0.25, 0.5, 1.0, 2.0, 3.0, 5.0, 7.0, 10.0]
SHAPES = {
    "rising": [0.0, 0.020, 0.022, 0.025, 0.029, 0.032, 0.036, 0.039, 0.041],
    "humped": [0.0, 0.030, 0.034, 0.039, 0.042, 0.040, 0.036, 0.033, 0.031],
    "falling": [0.0, 0.050, 0.048, 0.045, 0.041, 0.038, 0.035, 0.033, 0.032],
    "wavy": [0.0, 0.030, 0.026, 0.034, 0.029, 0.037, 0.031, 0.036, 0.033],
    "flat runs": [0.0, 0.030, 0.030, 0.030, 0.030, 0.032, 0.032, 0.032, 0.034],
}
SETTINGS = [(0.3, 0.7, True), (1.0, 0.7, True), (0.5, 0.9, True), (0.0, 0.7, False), (0.0, 1.0, False),
            (0.3, 0.0, True), (0.7, 0.5, True)]


def points():
    out = [-0.1]
    for a, b in zip(X, X[1:]):
        out += [a + k * (b - a) / 4 for k in range(4)]
    return out + [X[-1], 11.0, 15.0]


def cases():
    out = []
    pts = points()
    with cxx.connect() as H:
        st = H.settings(ql.Date(15, ql.January, 2025).serialNumber())
        for shape, y in SHAPES.items():
            for q, m, fp in SETTINGS:
                base = {"settings": st, "x": X, "y": y, "quadraticity": q, "monotonicity": m, "forcePositive": fp}
                for flat in (False, True):
                    inputs = dict(base, form="whole", flatFinalPeriod=flat, points=pts)
                    out.append({"name": f"{shape} q {q} m {m} fp {fp} flat final {flat}", "inputs": inputs,
                                "expected": v.enc(H.call("ConvexMonotoneInterpolation", X, y, q, m, fp, flat, pts))})
                for loc in (2, 3):
                    inputs = dict(base, form="local", localisation=loc, points=pts)
                    out.append({"name": f"{shape} q {q} m {m} fp {fp} local {loc}", "inputs": inputs,
                                "expected": v.enc(H.call("ConvexMonotone::localInterpolate", X, y, q, m, fp, loc,
                                                         pts))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/convexmonotoneinterpolation.hpp", cases)
