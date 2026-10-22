# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiPathGenerator (ql/methods/montecarlo/multipathgenerator.hpp), through the wheel's
GaussianMultiPathGenerator (MultiPathGenerator<GaussianRandomSequenceGenerator>, Mersenne-Twister Gaussian
sequences) and GaussianSobolMultiPathGenerator (MultiPathGenerator<GaussianLowDiscrepancySequenceGenerator>, Sobol
ones): stochastic process arrays of two and three processes (Ornstein-Uhlenbeck, geometric Brownian,
Black-Scholes-Merton) over uniform and non-uniform time grids. Each case draws 12 paths, each followed by its
antithetic, and records every asset's path and the sample's weight; the multi-path's assetNumber. Then the
constructor's refusals (a generator of the wrong dimension, a grid of one time) and the Brownian bridge, which
QuantLib refuses at the first draw and the port at construction, the same text.

The wheel's calls, by QuantLib-SWIG's montecarlo.i: the generators' constructor (process, times, generator,
brownianBridge) takes the grid as its times, next and antithetic return a SampleMultiPath whose value is the
MultiPath (assetNumber, each path by index) and weight the sequence's; the sequence generators are
GaussianRandomSequenceGenerator(UniformRandomSequenceGenerator(dimension, UniformRandomGenerator(seed))), PseudoRandom's
make_sequence_generator, and GaussianLowDiscrepancySequenceGenerator(UniformLowDiscrepancySequenceGenerator(dimension,
seed)), LowDiscrepancy's. stochasticprocessarray.py builds the processes. Read back by
tests/NanoQL.Tests/Vectors/Methods/MonteCarlo/MultiPathGeneratorVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(_dir, "gen", "processes"))

import QuantLib as ql  # noqa: E402

import stochasticprocessarray as spa  # noqa: E402
import vectors as v  # noqa: E402

GRIDS = {"uniform": [0.0, 0.25, 0.5, 0.75, 1.0], "non-uniform": [0.0, 0.1, 0.35, 1.0, 1.5, 3.0]}
DRAWS = 12


def generator(kind, dimension, seed):
    if kind == "pseudorandom":
        return ql.GaussianRandomSequenceGenerator(
            ql.UniformRandomSequenceGenerator(dimension, ql.UniformRandomGenerator(seed)))
    return ql.GaussianLowDiscrepancySequenceGenerator(ql.UniformLowDiscrepancySequenceGenerator(dimension, seed))


def path_generator(kind, process, times, rsg, bb):
    cls = ql.GaussianMultiPathGenerator if kind == "pseudorandom" else ql.GaussianSobolMultiPathGenerator
    return cls(process, times, rsg, bb)


def multipath(mp):
    return [[mp[j][i] for i in range(len(mp[j]))] for j in range(mp.assetNumber())]


def cases():
    st = v.settings(spa.TODAY)
    out = []
    for array in ("two, positive definite", "three, negative eigenvalue"):
        specs = spa.PROCESSES[array]
        corr = spa.CORRELATIONS[array]
        for kind in ("pseudorandom", "lowdiscrepancy"):
            for grid_name, times in GRIDS.items():
                for seed in (42, 1234567):
                    process = ql.StochasticProcessArray([spa.make(*s) for s in specs], ql.Matrix(corr))
                    dimension = process.factors() * (len(times) - 1)
                    g = path_generator(kind, process, times, generator(kind, dimension, seed), False)
                    paths, antithetic, weights = [], [], []
                    asset_number = None
                    for _ in range(DRAWS):
                        s = g.next()
                        paths.append(multipath(s.value()))
                        weights.append(s.weight())
                        asset_number = s.value().assetNumber()
                        a = g.antithetic()
                        antithetic.append(multipath(a.value()))
                    out.append({"name": f"{array}, {kind}, {grid_name} grid, seed {seed}",
                                "inputs": {"settings": st, "processes": [spa.process_input(*s) for s in specs],
                                           "correlation": corr, "generator": kind, "times": times, "seed": seed,
                                           "draws": DRAWS},
                                "expected": {"paths": paths, "antithetic": antithetic, "weights": weights,
                                             "assetNumber": asset_number}})
    specs = spa.PROCESSES["two, positive definite"]
    corr = spa.CORRELATIONS["two, positive definite"]
    times = GRIDS["uniform"]
    for name, dimension, ts, bb in (("wrong dimension", 7, times, False), ("one time", 0, [0.0], False),
                                    ("Brownian bridge", 8, times, True)):
        def first(dimension=dimension, ts=ts, bb=bb):
            process = ql.StochasticProcessArray([spa.make(*s) for s in specs], ql.Matrix(corr))
            return multipath(path_generator("pseudorandom", process, ts, generator("pseudorandom", dimension, 42),
                                            bb).next().value())

        out.append({"name": name,
                    "inputs": {"settings": st, "processes": [spa.process_input(*s) for s in specs],
                               "correlation": corr, "generator": "pseudorandom", "times": ts, "seed": 42,
                               "dimension": dimension, "brownianBridge": bb},
                    "expected": {"first": v.attempt(first)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/montecarlo/multipathgenerator.hpp", cases)
