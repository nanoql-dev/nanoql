# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for differential evolution (ql/math/optimization/differentialevolution.hpp, .cpp), each output computed by
the C++ harness on a new optimizer, so its Mersenne Twister starts from the seed, since the wheel binds no cost function
for it to minimize (docs/porting-playbook.md §9.4).

Each of the seven strategies under each of the three crossover types, on QuantLib's first De Jong function (the sphere)
in 3 variables in [-10, 10], 30 members, 40 generations; each strategy with adaptive crossover, without bounds applied,
and with the bounds given in the configuration, on the extended Rosenbrock in 2 variables in [-5, 5] and Griewangk's
function in 4 variables in [-600, 600]; an initial population given; three seeds; and QuantLib's
testDifferentialEvolution's first configuration (test-suite/optimizers.cpp:937-946: 500 members, the best member with
jitter, adaptive normal crossover, seed 3242) on its first De Jong function in 3 variables and on the extended
Rosenbrock in 2 (its second De Jong's function, written as the Rosenbrock). Each case records the Problem after minimize
(current value, function value, gradient norm, the evaluation counts) and the end criterion. The optimizer's seed is
nanoql's constructor argument and QuantLib's configuration's. The harness unit is
tools/cxx/src/math/optimization/differentialevolution.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/DifferentialEvolutionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

FIELDS = ("currentValue", "functionValue", "gradientNormValue", "functionEvaluation", "gradientEvaluation",
          "endCriteriaType")
STRATEGIES = ["Rand1Standard", "BestMemberWithJitter", "CurrentToBest2Diffs", "Rand1DiffWithPerVectorDither",
              "Rand1DiffWithDither", "EitherOrWithOptimalRecombination", "Rand1SelfadaptiveWithRotation"]
CROSSOVERS = ["Normal", "Binomial", "Exponential"]
SHORT = {"maxIterations": 40, "maxStationaryStateIterations": 10, "rootEpsilon": 1e-10, "functionEpsilon": 1e-8,
         "gradientNormEpsilon": None}


def box(lo, hi):
    return {"type": "BoundaryConstraint", "low": lo, "high": hi}


def configuration(strategy, crossover, members=30, weight=0.5, probability=0.9, apply_bounds=True, adaptive=False,
                  seed=1, **extra):
    return {"strategy": STRATEGIES.index(strategy), "crossoverType": CROSSOVERS.index(crossover),
            "populationMembers": members, "stepsizeWeight": weight, "crossoverProbability": probability,
            "applyBounds": apply_bounds, "adaptiveCrossover": adaptive, "seed": seed, **extra}


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def case(name, function, constraint, start, criteria, config):
            r = H.call("DifferentialEvolution::minimize", function, constraint, start, criteria, config)
            return {"name": name,
                    "inputs": {"settings": st, "costFunction": function, "constraint": constraint,
                               "initialValue": start, "endCriteria": criteria, "configuration": config},
                    "expected": {k: v.enc(r[k]) for k in FIELDS}}

        out = []
        sphere = {"type": "firstDeJong"}
        for strategy in STRATEGIES:
            for crossover in CROSSOVERS:
                out.append(case(f"sphere, {strategy}, {crossover}", sphere, box(-10.0, 10.0), [5.0, 5.0, 5.0], SHORT,
                                configuration(strategy, crossover)))
        rosenbrock = {"type": "extendedRosenbrock"}
        griewangk = {"type": "griewangk"}
        for strategy in STRATEGIES:
            out.append(case(f"Rosenbrock, {strategy}, adaptive", rosenbrock, box(-5.0, 5.0), [-1.2, 1.0], SHORT,
                            configuration(strategy, "Normal", adaptive=True, seed=7)))
            out.append(case(f"Griewangk, {strategy}, bounds not applied", griewangk, box(-600.0, 600.0),
                            [100.0] * 4, SHORT, configuration(strategy, "Binomial", apply_bounds=False, seed=11)))
            out.append(case(f"Rosenbrock, {strategy}, the configuration's bounds", rosenbrock, {"type": "NoConstraint"},
                            [-1.2, 1.0], SHORT,
                            configuration(strategy, "Exponential", seed=13, lowerBound=[-2.0, -1.0],
                                          upperBound=[2.0, 3.0])))
        population = [[0.5 * k - 2.0, 1.0 - 0.25 * k] for k in range(12)]
        out.append(case("Rosenbrock, an initial population", rosenbrock, box(-5.0, 5.0), [-1.2, 1.0], SHORT,
                        configuration("BestMemberWithJitter", "Normal", members=12, initialPopulation=population)))
        for seed in (42, 3242, 4294967295):
            out.append(case(f"sphere, seed {seed}", sphere, box(-10.0, 10.0), [5.0, 5.0, 5.0], SHORT,
                            configuration("Rand1Standard", "Normal", seed=seed)))
        conf = dict(members=500, weight=0.4, probability=0.35, adaptive=True, seed=3242)
        for name, function, n in (("first De Jong", sphere, 3), ("extended Rosenbrock", rosenbrock, 2)):
            criteria = {"maxIterations": 100, "maxStationaryStateIterations": 10, "rootEpsilon": 1e-10,
                        "functionEpsilon": 1e-8, "gradientNormEpsilon": None}
            out.append(case(f"QuantLib's configuration, {name}", function, box(-10.0, 10.0), [5.0] * n, criteria,
                            configuration("BestMemberWithJitter", "Normal", **conf)))
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/differentialevolution.hpp", cases)
