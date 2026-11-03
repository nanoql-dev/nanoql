# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Geman-Roncoroni, extended Black-Scholes-Merton and vega-stressed processes
(ql/experimental/processes/gemanroncoroniprocess.hpp, extendedblackscholesprocess.hpp,
vegastressedblackscholesprocess.hpp and their .cpp files), which the wheel doesn't bind, each output computed by the
C++ harness (tools/cxx/src/experimental/processes/electricityandstressprocesses.cpp).

- "geman-roncoroni" case: vpp.cpp's testGemanRoncoroniProcess paths at seed 421, the first three paths' first thirty
  steps of the electricity and fuel log prices.
- "extended" cases: the extended process on flat curves and a flat 0.4 at three points: its drift and diffusion on each
  scheme, and its predictor-corrector step, the exact step there; QuantLib's Euler and Milstein steps are left out,
  nanoql's are the log-Euler step.
- "vega" cases: the vega-stressed process with no stress, every member at three points, and with a stress of 0.1 in a
  box, its drift, diffusion and expectation; QuantLib's stressed deviation, variance and step are left out, nanoql's
  read the stressed diffusion.
- "vega setters" case: a stress box set through the process's five setters on QuantLib's default box, the box read back
  and the diffusion at three points.
- "geman-roncoroni values" case: vpp.cpp's process's drift, diffusion and deviation at three points, each step long
  enough that the deviation is QuantLib's formula.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Processes/ElectricityAndStressProcessesCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46311  # Friday 16 October 2026
EULER, MILSTEIN, PREDICTOR_CORRECTOR = 0, 1, 2
POINTS = [[0.0, 100.0, 0.25, -1.0], [0.5, 90.0, 0.1, 0.5], [1.0, 110.0, 0.02, 2.0]]
# |2 theta1 dt| at or above 1e-2 (theta1 35), where nanoql's deviation is QuantLib's formula
GR_POINTS = [[0.0, 3.3, 0.004], [0.25, 3.5, 0.004], [1.3, 2.9, 0.1]]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        out.append({"name": "geman-roncoroni: three paths, thirty steps",
                    "inputs": {"settings": st, "paths": 3, "steps": 30},
                    "expected": H.call("GemanRoncoroniProcess::paths", 3, 30)})
        for scheme, keep in ((EULER, ("drift", "diffusion")), (MILSTEIN, ("drift", "diffusion")),
                             (PREDICTOR_CORRECTOR, ("drift", "diffusion", "evolve"))):
            r = H.call("ExtendedBlackScholesMertonProcess::values", 100.0, 0.03, 0.01, 0.4, scheme, POINTS)
            out.append({"name": f"extended: scheme {scheme}",
                        "inputs": {"settings": st, "spot": 100.0, "r": 0.03, "q": 0.01, "vol": 0.4, "scheme": scheme,
                                   "points": POINTS},
                        "expected": {k: r[k] for k in keep}})
        for box, keep in (([0.0, 1000000.0, 0.0, 1000000.0, 0.0], None),
                          ([0.0, 1.0, 50.0, 150.0, 0.1], ("drift", "diffusion", "expectation"))):
            r = H.call("VegaStressedBlackScholesProcess::values", 100.0, 0.03, 0.01, 0.2, box, POINTS)
            out.append({"name": f"vega: stress {box[4]}",
                        "inputs": {"settings": st, "spot": 100.0, "r": 0.03, "q": 0.01, "vol": 0.2, "box": box,
                                   "points": POINTS},
                        "expected": r if keep is None else {k: r[k] for k in keep}})
        box = [0.25, 2.0, 80.0, 105.0, 0.05]
        out.append({"name": "vega setters: a box set member by member",
                    "inputs": {"settings": st, "spot": 100.0, "r": 0.03, "q": 0.01, "vol": 0.2, "box": box,
                               "points": POINTS},
                    "expected": H.call("VegaStressedBlackScholesProcess::setters", 100.0, 0.03, 0.01, 0.2, box,
                                       POINTS)})
        out.append({"name": "geman-roncoroni values: three points",
                    "inputs": {"settings": st, "points": GR_POINTS},
                    "expected": H.call("GemanRoncoroniProcess::values", GR_POINTS)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/processes/gemanroncoroniprocess.hpp", cases)
