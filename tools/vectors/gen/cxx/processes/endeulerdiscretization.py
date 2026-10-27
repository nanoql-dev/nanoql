# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EndEulerDiscretization (ql/processes/endeulerdiscretization.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness: the drift, diffusion and variance on a Black-Scholes-Merton
process whose local volatility moves with time, and the drift, diffusion and covariance on a Heston process, at start
times 0 to 1.5, states around the spot and steps of a day to half a year, all read at the step's end.

The harness unit and its processes are tools/cxx/src/processes/endeulerdiscretization.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Processes/EndEulerDiscretizationCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for t0 in (0.0, 0.2, 0.9, 1.5):
            for dt in (1 / 365.0, 0.1, 0.5):
                for x0 in (80.0, 100.0, 130.0):
                    out.append({"name": f"Black-Scholes, t0 {t0}, x0 {x0}, dt {dt:.6g}",
                                "inputs": {"settings": st, "call": "1d", "t0": t0, "x0": x0, "dt": dt},
                                "expected": {m: H.call("EndEulerDiscretization.1d", m, t0, x0, dt)
                                             for m in ("drift", "diffusion", "variance")}})
                for x in ([100.0, 0.04], [90.0, 0.09]):
                    out.append({"name": f"Heston, t0 {t0}, x0 {x}, dt {dt:.6g}",
                                "inputs": {"settings": st, "call": "nd", "t0": t0, "x0": x, "dt": dt},
                                "expected": {"drift": H.call("EndEulerDiscretization.nd", "drift", t0, x, dt),
                                             "diffusion": [e for row in H.call("EndEulerDiscretization.nd", "diffusion", t0, x, dt) for e in row],
                                             "covariance": [e for row in H.call("EndEulerDiscretization.nd", "covariance", t0, x, dt) for e in row]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/endeulerdiscretization.hpp", cases)
