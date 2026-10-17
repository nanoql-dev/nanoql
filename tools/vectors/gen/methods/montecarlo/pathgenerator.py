# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PathGenerator (ql/methods/montecarlo/pathgenerator.hpp), with the Path it returns
(ql/methods/montecarlo/path.hpp), over 3 Ornstein-Uhlenbeck processes. GaussianPathGenerator: each process, with the
Brownian bridge and without, from seeds 42 and 1234, over the regular grid of 10 years in 12 steps
(PathGenerator(process, length, timeSteps, generator, brownianBridge)). GaussianSobolPathGenerator: each process,
with the bridge and without, over a grid of 12 irregular steps built from mandatory times
(PathGenerator(process, timeGrid, generator, brownianBridge)). Then each generator through its other constructor,
for the first two processes, with the bridge and without: GaussianPathGenerator from seed 42 over the irregular grid,
GaussianSobolPathGenerator over the regular one. Each case draws 10 paths, each followed by its antithetic: every
path's 13 values (through operator[]) and its weight, and for the first path its length, front, back, value(i) and
time(i); and the generator's size and time grid. Then the refusals: a generator whose dimension isn't the number of
steps, for each generator in both constructors, and a path length that isn't positive.

The wheel's calls, by QuantLib-SWIG's montecarlo.i and randomnumbers.i. GaussianPathGenerator is
PathGenerator<GaussianRandomSequenceGenerator>, and GaussianRandomSequenceGenerator is SWIG's name for
PseudoRandom::rsg_type, InverseCumulativeRsg<RandomSequenceGenerator<MersenneTwisterUniformRng>,
InverseCumulativeNormal>: the C++ type nanoql's InvCumulativeMersenneTwisterPathGenerator closes, the name QuantLib's
own arguments give (SWIG's InvCumulativeMersenneTwisterPathGenerator is the same type). It is built as
ql.GaussianRandomSequenceGenerator(ql.UniformRandomSequenceGenerator(steps, ql.UniformRandomGenerator(seed))):
UniformRandomGenerator is PseudoRandom::urng_type, MersenneTwisterUniformRng(seed), and
UniformRandomSequenceGenerator is PseudoRandom::ursg_type, RandomSequenceGenerator<MersenneTwisterUniformRng>, built
from it by copy. GaussianSobolPathGenerator is PathGenerator<GaussianLowDiscrepancySequenceGenerator>, which is
LowDiscrepancy::rsg_type, InverseCumulativeRsg<SobolRsg, InverseCumulativeNormal>, built as
ql.GaussianLowDiscrepancySequenceGenerator(ql.UniformLowDiscrepancySequenceGenerator(steps, seed)), the second
LowDiscrepancy::ursg_type, SobolRsg(steps, seed, Jaeckel). The generators' constructors copy the sequence generator,
as QuantLib's do. next() and antithetic() return the generator's own sample, read before the next draw; the path's
length, front, back, value, time and [i] are Path's members. Read back by
tests/NanoQL.Tests/Vectors/Methods/MonteCarlo/PathGeneratorVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

PROCESSES = [
    {"type": "OrnsteinUhlenbeckProcess", "speed": 0.1, "volatility": 0.2, "x0": 0.0, "level": 0.0},
    {"type": "OrnsteinUhlenbeckProcess", "speed": 1.2, "volatility": 0.35, "x0": 0.5, "level": 0.1},
    {"type": "OrnsteinUhlenbeckProcess", "speed": -0.05, "volatility": 0.08, "x0": -0.2, "level": 0.3},
]
LENGTH, STEPS = 10.0, 12
SEEDS = [42, 1234]
SOBOL_SEED = 42
MANDATORY = [0.1, 0.25, 0.5, 1.0, 1.5, 2.0, 3.0, 4.0, 5.0, 7.0, 10.0, 15.0]
PATHS = 10


def process(spec):
    return ql.OrnsteinUhlenbeckProcess(spec["speed"], spec["volatility"], spec["x0"], spec["level"])


def mt(steps, seed):
    return ql.GaussianRandomSequenceGenerator(ql.UniformRandomSequenceGenerator(steps, ql.UniformRandomGenerator(seed)))


def sobol(steps, seed):
    return ql.GaussianLowDiscrepancySequenceGenerator(ql.UniformLowDiscrepancySequenceGenerator(steps, seed))


def values(path):
    return [path[i] for i in range(len(path))]


def draws(generator):
    expected = {"paths": [], "weights": [], "antithetic": [], "antitheticWeights": []}
    for k in range(PATHS):
        s = generator.next()
        path = s.value()
        expected["paths"].append(values(path))
        expected["weights"].append(s.weight())
        if k == 0:
            first = {"length": len(path), "front": path.front(), "back": path.back(),
                     "value": [path.value(i) for i in range(len(path))],
                     "time": [path.time(i) for i in range(len(path))]}
        a = generator.antithetic()
        expected["antithetic"].append(values(a.value()))
        expected["antitheticWeights"].append(a.weight())
    tg = generator.timeGrid()
    return {"size": generator.size(), "timeGrid": [tg[i] for i in range(len(tg))], **first, **expected}


def cases():
    st = v.settings(ql.Date(16, ql.October, 2026))
    out = []
    for k, spec in enumerate(PROCESSES):
        for bridge in (False, True):
            for seed in SEEDS:
                g = ql.GaussianPathGenerator(process(spec), LENGTH, STEPS, mt(STEPS, seed), bridge)
                out.append({"name": f"GaussianPathGenerator process {k} {'bridge' if bridge else 'no bridge'} "
                                    f"seed {seed}",
                            "inputs": {"settings": st, "generator": "GaussianPathGenerator", "process": spec,
                                       "length": LENGTH, "timeSteps": STEPS, "seed": seed, "brownianBridge": bridge},
                            "expected": draws(g)})
    for k, spec in enumerate(PROCESSES):
        for bridge in (False, True):
            g = ql.GaussianSobolPathGenerator(process(spec), ql.TimeGrid(MANDATORY), sobol(STEPS, SOBOL_SEED), bridge)
            out.append({"name": f"GaussianSobolPathGenerator process {k} {'bridge' if bridge else 'no bridge'}",
                        "inputs": {"settings": st, "generator": "GaussianSobolPathGenerator", "process": spec,
                                   "times": MANDATORY, "seed": SOBOL_SEED, "brownianBridge": bridge},
                        "expected": draws(g)})
    # each generator through its other constructor
    for k, spec in enumerate(PROCESSES[:2]):
        for bridge in (False, True):
            g = ql.GaussianPathGenerator(process(spec), ql.TimeGrid(MANDATORY), mt(STEPS, SEEDS[0]), bridge)
            out.append({"name": f"GaussianPathGenerator time grid process {k} "
                                f"{'bridge' if bridge else 'no bridge'} seed {SEEDS[0]}",
                        "inputs": {"settings": st, "generator": "GaussianPathGenerator", "process": spec,
                                   "times": MANDATORY, "seed": SEEDS[0], "brownianBridge": bridge},
                        "expected": draws(g)})
            g = ql.GaussianSobolPathGenerator(process(spec), LENGTH, STEPS, sobol(STEPS, SOBOL_SEED), bridge)
            out.append({"name": f"GaussianSobolPathGenerator length process {k} {'bridge' if bridge else 'no bridge'}",
                        "inputs": {"settings": st, "generator": "GaussianSobolPathGenerator", "process": spec,
                                   "length": LENGTH, "timeSteps": STEPS, "seed": SOBOL_SEED, "brownianBridge": bridge},
                        "expected": draws(g)})

    spec = PROCESSES[0]
    refusals = [
        ("GaussianPathGenerator time grid dimension 13 for 12 steps",
         {"generator": "GaussianPathGenerator", "times": MANDATORY, "dimension": STEPS + 1},
         lambda: ql.GaussianPathGenerator(process(spec), ql.TimeGrid(MANDATORY), mt(STEPS + 1, 42), False).size()),
        ("GaussianSobolPathGenerator dimension 11 for 12 steps",
         {"generator": "GaussianSobolPathGenerator", "length": LENGTH, "timeSteps": STEPS, "dimension": STEPS - 1},
         lambda: ql.GaussianSobolPathGenerator(process(spec), LENGTH, STEPS, sobol(STEPS - 1, SOBOL_SEED),
                                               False).size()),
        ("GaussianPathGenerator dimension 11 for 12 steps",
         {"generator": "GaussianPathGenerator", "length": LENGTH, "timeSteps": STEPS, "dimension": STEPS - 1},
         lambda: ql.GaussianPathGenerator(process(spec), LENGTH, STEPS, mt(STEPS - 1, 42), False).size()),
        ("GaussianPathGenerator length 0",
         {"generator": "GaussianPathGenerator", "length": 0.0, "timeSteps": STEPS, "dimension": STEPS},
         lambda: ql.GaussianPathGenerator(process(spec), 0.0, STEPS, mt(STEPS, 42), False).size()),
        ("GaussianSobolPathGenerator dimension 13 for 12 steps",
         {"generator": "GaussianSobolPathGenerator", "times": MANDATORY, "dimension": STEPS + 1},
         lambda: ql.GaussianSobolPathGenerator(process(spec), ql.TimeGrid(MANDATORY), sobol(STEPS + 1, SOBOL_SEED),
                                               False).size()),
    ]
    for name, inputs, make in refusals:
        out.append({"name": name,
                    "inputs": {"settings": st, "process": spec, "seed": 42, "brownianBridge": False, **inputs},
                    "expected": {"size": v.attempt(make)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/montecarlo/pathgenerator.hpp", cases)
