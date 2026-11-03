# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's hybrid simulated annealing (ql/experimental/math/hybridsimulatedannealing.hpp,
hybridsimulatedannealingfunctors.hpp) through the three typedefs the wheel names, GaussianSimulatedAnnealing,
LogNormalSimulatedAnnealing and MirrorGaussianSimulatedAnnealing, on their named functors at fixed seeds: the samplers'
std::normal_distribution and the probability's std::uniform_real_distribution on std::mt19937, as MSVC 14.44 draws them.

- "annealing" cases: each typedef minimizing the sphere centred at (1.2, −0.7) from (3, 2), at starting temperatures 1
  and 200 (the default), the reset schemes to the best point (the default) and to the origin every 7 steps, and
  reannealing every 5; the mirror sampler within [−5, 5]². The wheel's cost function gives the local optimizer no
  values, so each new best's Levenberg-Marquardt step throws and the annealing's catch skips it, as the reader's
  cost function does: the result is the starting point, and the vectors keep every evaluated point, which the samplers'
  and the probability's streams set.

Read back by tests/NanoQL.Tests/Vectors/Experimental/Math/HybridSimulatedAnnealingVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

REF = ql.Date(16, ql.October, 2026)
CENTER = (1.2, -0.7)
CONFIGS = [(kind, t0, reset, steps, reanneal) for kind in ("gaussian", "lognormal", "mirror")
           for (t0, reset, steps, reanneal) in ((1.0, "best", 150, 50), (200.0, "best", 150, 50),
                                                 (1.0, "origin", 7, 5))]


def annealing(kind, t0, reset, steps, reanneal):
    typedef = {"gaussian": ql.GaussianSimulatedAnnealing, "lognormal": ql.LogNormalSimulatedAnnealing,
               "mirror": ql.MirrorGaussianSimulatedAnnealing}[kind]
    if kind == "gaussian":
        sampler = ql.SamplerGaussian(42)
    elif kind == "lognormal":
        sampler = ql.SamplerLogNormal(42)
    else:
        sampler = ql.SamplerMirrorGaussian(ql.Array([-5.0, -5.0]), ql.Array([5.0, 5.0]), 42)
    scheme = typedef.ResetToBestPoint if reset == "best" else typedef.ResetToOrigin
    return typedef(sampler, ql.ProbabilityBoltzmannDownhill(43), ql.TemperatureExponential(1.0, 2),
                   ql.ReannealingTrivial(), t0, 0.01, reanneal, scheme, steps)


def cases():
    out = []
    ql.Settings.instance().evaluationDate = REF
    st = v.settings(REF)
    for kind, t0, reset, steps, reanneal in CONFIGS:
        log = []

        def f(x):
            log.extend(list(x))
            return (x[0] - CENTER[0]) ** 2 + (x[1] - CENTER[1]) ** 2

        method = annealing(kind, t0, reset, steps, reanneal)
        x = ql.Optimizer().solve(f, ql.NoConstraint(), method, ql.EndCriteria(60, 40, 1e-8, 1e-8, 1e-8),
                                 ql.Array([3.0, 2.0]))
        out.append({"name": f"annealing: {kind}, start {t0}, reset {reset} every {steps}, reanneal every {reanneal}",
                    "inputs": {"settings": st, "kind": kind, "startTemperature": t0, "reset": reset,
                               "resetSteps": steps, "reAnnealSteps": reanneal},
                    "expected": {"x": list(x), "log": log}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/hybridsimulatedannealing.hpp", cases)
