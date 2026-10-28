# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for StochasticCollocationInvCDF (ql/math/randomnumbers/stochasticcollocationinvcdf.hpp, .cpp), which the
wheel doesn't bind, each output computed by the C++ harness: QuantLib's test's collocations of the non-central
chi-squared inverse (k 3, lambda 1; 10 points, and 30 with pMax 0.9999999), and the exponential's (lambda 2; 5 points
with pMin 2e-6, 12 with neither), each value(x) on deviates from -4 to 4 and operator()(u) on probabilities from 1e-6
to 1 - 1e-6. pMax and pMin lie off the probabilities' grid: there the deviate lands on the outer node, where QuantLib's
Lagrange interpolation is NaN for descending nodes (the S-van plan's VR4 (xxviii), a documented divergence). Read back
by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/StochasticCollocationInvCDFCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

XS = [i / 4 for i in range(-16, 17)]
US = [1e-6, 1e-4, 0.001, 0.01, 0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 0.95, 0.99, 0.999, 1 - 1e-4, 1 - 1e-6]
COLLOCATIONS = [("chi-squared, 10 points", {"nonCentralChiSquared": [3.0, 1.0]}, 10, None, None),
                ("chi-squared, 30 points, pMax", {"nonCentralChiSquared": [3.0, 1.0]}, 30, 0.9999999, None),
                ("exponential, 5 points, pMin", {"exponential": 2.0}, 5, None, 2e-6),
                ("exponential, 12 points", {"exponential": 2.0}, 12, None, None)]


def cases():
    out = []
    with cxx.connect() as H:
        for name, spec, order, p_max, p_min in COLLOCATIONS:
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            r = H.call("StochasticCollocationInvCDF::evaluate", spec, order, p_max, p_min, XS, US)
            out.append({"name": name,
                        "inputs": {"settings": st, "inverse": spec, "order": order, "pMax": v.enc(p_max),
                                   "pMin": v.enc(p_min), "x": v.enc(XS), "u": v.enc(US)},
                        "expected": {"value": v.enc(r["value"]), "operator()": v.enc(r["operator()"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/stochasticcollocationinvcdf.hpp", cases)
