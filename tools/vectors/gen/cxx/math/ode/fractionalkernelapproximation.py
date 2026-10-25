# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Harness vectors for QuantLib's FractionalKernelApproximation (ql/math/ode/fractionalkernelapproximation.hpp), which
the wheel doesn't expose: the sum-of-exponentials approximation of the fractional kernel.

Orders 0.55, 0.6, 0.8 and 1 (the collapse to one node) at 1, 2, 10 and 40 factors over horizons of 0.25, 1 and 5: the
size, weights, rates and the approximation at five times.

A case holds "alpha", "n", "tMax" and "ts". The harness's call: FractionalKernelApproximation::members
(tools/cxx/src/math/ode/fractionalkernelapproximation.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Ode/FractionalKernelApproximationCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
ALPHAS = [0.55, 0.6, 0.8, 1.0]
NS = [1, 2, 10, 40]
HORIZONS = [0.25, 1.0, 5.0]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for alpha in ALPHAS:
            for n in NS:
                for t_max in HORIZONS:
                    ts = [t_max * f for f in (1e-4, 0.01, 0.2, 0.7, 1.0)]
                    r = H.call("FractionalKernelApproximation::members", alpha, n, t_max, ts)
                    out.append({"name": f"alpha {alpha}, {n} factors, horizon {t_max}",
                                "inputs": {"settings": st, "alpha": alpha, "n": n, "tMax": t_max, "ts": ts},
                                "expected": {"size": r["size"], "weights": [v.enc(x) for x in r["weights"]],
                                             "rates": [v.enc(x) for x in r["rates"]],
                                             "values": [v.enc(x) for x in r["values"]]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/ode/fractionalkernelapproximation.hpp", cases)
