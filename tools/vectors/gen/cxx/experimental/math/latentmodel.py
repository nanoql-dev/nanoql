# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LatentModel (ql/experimental/math/latentmodel.hpp), which the wheel doesn't expose, each output
computed by the C++ harness (tools/cxx/src/experimental/math/latentmodel.cpp) on the harness's own subclass, which adds
an integration of a given type (QuantLib's protected IntegrationFactory), so that the base's integration answers.

Gaussian and Student t models built by each constructor QuantLib defines on its policy: the weight matrix (one and two
factors), the one-factor weights (Student t only: QuantLib's default-constructs the Gaussian policy, which crashes, the
S-xcr plan's XR8 (vi), held in Divergences/LatentModelVectorConstructorTests.cs), a common weight, and a correlation
whose square root is the weight (QuantLib's quote constructor). For each: the inspectors; the policy's members through
the model (cumulative and inverse cumulative distributions of the variables, the idiosyncratic and systemic factors,
the factors' density, the all-factor inversion of uniform samples), each variable's value on factor samples and the
variables' correlations; the expected values of integrands under the Gauss-Hermite integration, and, on one factor,
the trapezoid one; and twenty draws of the default factor sampler on Mersenne Twister and Sobol sequences, and of the
Box-Muller sampler on the Gaussian models, at fixed seeds. The polar Student t sampler's draws differ (XR8 (xix), held
in Divergences/PolarStudentTSamplerTests.cs: one uniform stream, each draw scaled by its variance factor), so of its
first draw on a fresh sampler at each of twelve seeds the vectors hold what nanoql's shares: the first systemic value
times its variance factor, sqrt((nu - 2)/nu) as QuantLib's policy computes it (both samplers' first t generator starts
on the seed's stream), the dimension and the weight. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Math/LatentModelCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 38960  # 31 August 2006
QUAD, TRAP = 0, 1
MODELS = [
    ("Gaussian, a matrix of two factors", {"copula": "Gaussian", "factorWeights": [[0.3, 0.2], [0.5, 0.1], [0.2, 0.6]]}),
    ("Gaussian, a matrix of one factor", {"copula": "Gaussian", "factorWeights": [[0.3], [0.7], [0.0]]}),
    ("Gaussian, a common weight", {"copula": "Gaussian", "correlSqr": 0.45, "nVariables": 3}),
    ("Gaussian, a correlation", {"copula": "Gaussian", "correlation": 0.3, "nVariables": 4}),
    ("Student t, a matrix of one factor", {"copula": "T", "tOrders": [5, 5], "factorWeights": [[0.3], [0.5]]}),
    ("Student t, a matrix of two factors", {"copula": "T", "tOrders": [3, 5, 7],
                                            "factorWeights": [[0.3, 0.2], [0.1, 0.5]]}),
    ("Student t, one-factor weights", {"copula": "T", "tOrders": [9, 3], "weights": [0.2, 0.6, 0.4]}),
    ("Student t, a correlation", {"copula": "T", "tOrders": [5, 5], "correlation": 0.3, "nVariables": 2}),
]
REFUSED = [{"copula": "Gaussian", "factorWeights": [[0.8, 0.7]]},
           {"copula": "T", "tOrders": [5, 5], "factorWeights": [[0.3, 0.2]]},
           {"copula": "T", "tOrders": [4, 4], "factorWeights": [[0.3]]},
           {"copula": "T", "tOrders": [2, 5], "factorWeights": [[0.3]]},
           {"copula": "Gaussian", "factorWeights": [[0.3, 0.2], [0.5]]}]
VALUES = [-3.0, -1.0, 0.0, 0.5, 2.0]
PROBS = [0.01, 0.3, 0.5, 0.9]
INTEGRANDS = ["one", "m0", "m0sq", "exp", "indicator", "cos"]
POLAR_SEEDS = [1, 2, 3, 7, 42, 99, 1000, 65537, 123456789, 2147483647, 2863311530, 4294967295]


def counts(spec):
    k = len(spec["factorWeights"][0]) if "factorWeights" in spec else 1
    n = len(spec["factorWeights"]) if "factorWeights" in spec else (
        len(spec["weights"]) if "weights" in spec else spec["nVariables"])
    return n, k


def samples(n, k):
    total = n + k
    base = [-1.5, 0.3, 0.8, -0.2, 2.1, 0.0, -0.7, 1.1]
    return [[base[(j + s) % len(base)] * (1 + 0.1 * s) for j in range(total)] for s in range(3)]


def uniforms(n, k):
    total = n + k
    base = [0.05, 0.5, 0.93, 0.31, 0.77, 0.12, 0.66, 0.41]
    return [[base[(j + s) % len(base)] for j in range(total)] for s in range(3)]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, spec in MODELS:
            st = H.settings(R)
            n, k = counts(spec)
            s, u = samples(n, k), uniforms(n, k)
            integrands = INTEGRANDS + (["m0m1"] if k > 1 else [])
            expected = {"inspect": call("LatentModel::inspect", spec),
                        "members": call("LatentModel::members", spec, VALUES, PROBS, s, u),
                        "integrated": [call("LatentModel::integratedExpectedValue", spec, QUAD, f)
                                       for f in integrands]}
            if k == 1:
                expected["integratedTrapezoid"] = [call("LatentModel::integratedExpectedValue", spec, TRAP, f)
                                                   for f in INTEGRANDS if f != "indicator"]
            samplers = ["MT", "Sobol"] + (["BoxMuller"] if spec["copula"] == "Gaussian" else [])
            expected["draws"] = [call("FactorSampler::draws", spec, g, seed, 20) for g in samplers
                                 for seed in (42, 2863311530)]
            if spec["copula"] == "T":
                first = call("FactorSampler::polarFirst", spec, POLAR_SEEDS)
                nu = spec["tOrders"][0]
                vf = math.sqrt((nu - 2.0) / nu)
                expected["polarFirst"] = ([[d[0] * vf, len(d) - 1, d[-1]] for d in first] if isinstance(first, list)
                                          else first)
            out.append({"name": name,
                        "inputs": {"settings": st, "model": spec, "values": VALUES, "probabilities": PROBS,
                                   "samples": s, "uniforms": u, "integrands": integrands, "samplers": samplers,
                                   "seeds": [42, 2863311530],
                                   **({"polarSeeds": POLAR_SEEDS} if spec["copula"] == "T" else {})},
                        "expected": expected})
        st = H.settings(R)
        out.append({"name": "refused models", "inputs": {"settings": st, "models": REFUSED},
                    "expected": {"inspect": [call("LatentModel::inspect", m) for m in REFUSED]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/latentmodel.hpp", cases)
