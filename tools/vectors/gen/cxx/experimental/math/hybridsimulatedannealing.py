# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's hybrid simulated annealing on the functors the wheel doesn't name
(ql/experimental/math/hybridsimulatedannealing.hpp, hybridsimulatedannealingfunctors.hpp), each output computed by the
C++ harness (tools/cxx/src/experimental/math/hybridsimulatedannealing.cpp).

- "annealing" cases: four combinations of the Cauchy, ring, very fast and Gaussian samplers with the always-downhill,
  Boltzmann and Boltzmann-downhill probabilities, the Cauchy, Boltzmann, very fast and one-dimensional Cauchy
  temperatures and the trivial and finite-difference reannealings, each without a local optimizer and with
  Levenberg-Marquardt at every best point; the samplers' and probabilities' std::normal, std::uniform_real and
  std::cauchy draws on std::mt19937 as MSVC 14.44 draws them, and every point the cost function evaluates, the ring
  case's NaN trial points among them. The ring sampler's folds end within nanoql's cap of 10,000 passes there.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Math/HybridSimulatedAnnealingCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46311  # Friday 16 October 2026


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        for combination in ("cauchy", "ring", "veryFast", "cauchy1D"):
            for local in ("none", "lm"):
                args = [combination, local, 60, 40]
                out.append({"name": f"annealing: {combination}, local {local}", "inputs": {"settings": st, "args": args},
                            "expected": v.enc(H.call("HybridSimulatedAnnealing::minimize", *args))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/hybridsimulatedannealing.hpp", cases)
