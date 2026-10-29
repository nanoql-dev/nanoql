# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's particle swarm and firefly algorithm (ql/experimental/math/particleswarmoptimization.hpp,
.cpp, fireflyalgorithm.hpp, .cpp), which the wheel doesn't bind, each output computed by the C++ harness
(tools/cxx/src/experimental/math/swarmoptimizers.cpp), only where the S-xrt plan's fixes don't reach.

- "swarm" cases: eight particles on the sphere centred at (1.2, −0.7) within [−5, 5]², seed 42, on the global topology as
  QuantLib documents it (the harness's and the reader's, QuantLib's own keeping the worst, XRR5 (xxviii)), the
  constriction form with the trivial, simple random and Levy flight inertias and the inertia form with the trivial
  one: the result, the evaluations, the end criterion and every evaluated point.
- "firefly" cases: one firefly on the sphere with each walk and both intensities, where neither QuantLib's attraction loop (XRR5 (xxxii)) nor
  its value updates (the run's R4-1) differ from nanoql's, the decreasing walk on a constant cost, whose stationary end
  stops it after two rounds, before QuantLib's count of its walks parts from nanoql's at the third (the S-xrt code
  review's F4-2); and one firefly with three differential evolution members
  on a constant cost, which keeps the population in its own order and rejects every trial, the trials' points carrying
  the differential evolution's std::uniform_int_distribution draws as MSVC 14.44 draws them (XRD4).

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Math/SwarmOptimizersCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46311  # Friday 16 October 2026
LOWER, UPPER, CENTER = [-5.0, -5.0], [5.0, 5.0], [1.2, -0.7]
SWARMS = [("constriction", [2.05, 2.05], "trivial", []),
          ("constriction", [2.05, 2.05], "simpleRandom", [0.5, 11]),
          ("constriction", [2.05, 2.05], "levyFlight", [1.5, 2, 13]),
          ("inertia", [0.7, 1.4, 1.4], "trivial", [])]
FIREFLIES = [(1, 0, "gaussian", [0.1, 0.9, 17], "sphere", 40, 30),
             (1, 0, "gaussian", [0.2, 0.8, 29], "sphere", 20, 15),
             (1, 0, "levy", [1.5, 0.5, 0.9, 19], "sphere", 40, 30),
             (1, 0, "decreasing", [0.1, 0.9, 23], "constant", 3, 2),
             (4, 3, "gaussian", [0.1, 0.9, 17], "constant", 10, 9)]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        for form, c, inertia, ip in SWARMS:
            args = [8, form, c, inertia, ip, 42, LOWER, UPPER, CENTER, 30, 20]
            out.append({"name": f"swarm: {form} {c}, {inertia} {ip}", "inputs": {"settings": st, "args": args},
                        "expected": H.call("ParticleSwarmOptimization::minimize", *args)})
        for k, (M, Mde, walk, wp, cost, maxIt, maxStat) in enumerate(FIREFLIES):
            intensity, ip = ("inverse", [1.0, 0.2]) if k == 1 else ("exponential", [1.0, 0.2, 1.0])
            args = [M, Mde, 1.0, 0.5, 42, walk, wp, intensity, ip, cost, LOWER, UPPER, CENTER, maxIt, maxStat]
            out.append({"name": f"firefly: M {M}, Mde {Mde}, {walk} {wp}, {intensity}, {cost}",
                        "inputs": {"settings": st, "args": args},
                        "expected": H.call("FireflyAlgorithm::minimize", *args)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/particleswarmoptimization.hpp", cases)
