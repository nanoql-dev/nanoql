# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for CMA-ES (ql/math/optimization/cmaes.hpp, .cpp), each output computed by the C++ harness on a new
optimizer, so its Mersenne Twister starts from the seed, since the wheel binds no cost function for it to minimize
(docs/porting-playbook.md §9.4).

QuantLib's testCMAES problems (test-suite/optimizers.cpp:1032-1283; its functions in
tools/cxx/src/math/optimization/specs.hpp): the shifted, ill-conditioned weighted quadratics in 2 and 10 variables; the
extended Rosenbrock in 2 and 10 (a population of 20); Rastrigin's function in 5 variables in [-5.12, 5.12], at a
population of 30 where QuantLib's test takes 150; a quadratic form and its rotation; the determinism case; the
box-bounded quadratic; weights over twelve decades; a weight of 0; and the rotated ill-conditioned form. Then, on a
quadratic: the bounds given in the configuration, an initial mean given, a population of 2, three seeds, and a limit of
3 generations. Each case records the Problem after minimize (current value, function value, gradient norm, the
evaluation counts) and the end criterion. Rastrigin's cost cancels near its optimum (10 n against terms near -10 each),
so samples there tie, and a population above 32 ranks ties by MSVC's introsort, where nanoql sorts stably (the S-core
plan's CD5; tests/NanoQL.Tests/Divergences/CmaesTests.cs): at 150 the point returned differs, its cost equal, so the
case keeps to 30, where MSVC's insertion sort is stable. The optimizer's seed is nanoql's constructor argument and
QuantLib's configuration's. The harness unit is tools/cxx/src/math/optimization/cmaes.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/CmaesCxxVectorTests.cs."""
import math
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
NONE = {"type": "NoConstraint"}


def criteria(max_iterations, stationary, root, function):
    return {"maxIterations": max_iterations, "maxStationaryStateIterations": stationary, "rootEpsilon": root,
            "functionEpsilon": function, "gradientNormEpsilon": None}


def quadratic(center, weight):
    return {"type": "weightedQuadratic", "center": center, "weight": weight}


def config(sigma, seed, population=0, **extra):
    return {"populationSize": population, "sigma": sigma, "seed": seed, **extra}


def matmul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(len(b))) for j in range(len(b[0]))] for i in range(len(a))]


def transpose(a):
    return [list(r) for r in zip(*a)]


def rotated(n, d):
    q = [[1.0 if i == j else 0.0 for j in range(n)] for i in range(n)]
    for p_, q_, angle in ((0, 1, 0.7), (1, 2, 1.1), (2, 3, 0.4), (0, 3, 0.9)):
        g = [[1.0 if i == j else 0.0 for j in range(n)] for i in range(n)]
        g[p_][p_] = math.cos(angle)
        g[p_][q_] = -math.sin(angle)
        g[q_][p_] = math.sin(angle)
        g[q_][q_] = math.cos(angle)
        q = matmul(g, q)
    return matmul(matmul(transpose(q), d), q)


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def case(name, function, constraint, start, crit, cfg):
            r = H.call("Cmaes::minimize", function, constraint, start, crit, cfg)
            return {"name": name,
                    "inputs": {"settings": st, "costFunction": function, "constraint": constraint,
                               "initialValue": start, "endCriteria": crit, "configuration": cfg},
                    "expected": {k: v.enc(r[k]) for k in FIELDS}}

        out = []
        for n in (2, 10):
            f = quadratic([1.0 + 0.5 * i for i in range(n)], [10.0 ** (2.0 * i / n) for i in range(n)])
            out.append(case(f"convex quadratic {n}", f, NONE, [0.0] * n, criteria(5000, 100, 1e-14, 1e-14),
                            config(1.0, 42)))
        for n in (2, 10):
            out.append(case(f"Rosenbrock {n}", {"type": "extendedRosenbrock"}, NONE, [0.0] * n,
                            criteria(20000, 300, 1e-14, 1e-14), config(0.5, 123, 20 if n >= 10 else 0)))
        rastrigin_box = {"type": "BoundaryConstraint", "low": -5.12, "high": 5.12}
        out.append(case("Rastrigin 5, a population of 30", {"type": "rastrigin"}, rastrigin_box, [3.0] * 5,
                        criteria(2000, 200, 1e-12, 1e-12), config(4.0, 1, 30)))
        a = [[1.0, 0.0], [0.0, 30.0]]
        th = 0.7
        q = [[math.cos(th), -math.sin(th)], [math.sin(th), math.cos(th)]]
        arot = matmul(matmul(transpose(q), a), q)
        x0 = [2.0, 2.0]
        x0rot = [sum(transpose(q)[i][k] * x0[k] for k in range(2)) for i in range(2)]
        out.append(case("quadratic form", {"type": "quadraticForm", "matrix": a}, NONE, x0,
                        criteria(5000, 100, 1e-14, 1e-14), config(1.0, 99)))
        out.append(case("rotated quadratic form", {"type": "quadraticForm", "matrix": arot}, NONE, x0rot,
                        criteria(5000, 100, 1e-14, 1e-14), config(1.0, 99)))
        out.append(case("determinism", quadratic([1.0, -2.0, 0.5], [1.0, 3.0, 7.0]), NONE, [0.0] * 3,
                        criteria(500, 50, 1e-12, 1e-12), config(1.0, 2024)))
        out.append(case("box bounds", quadratic([3.0, -2.0], [1.0, 1.0]),
                        {"type": "BoundaryConstraint", "low": 0.0, "high": 1.0}, [0.5, 0.5],
                        criteria(3000, 100, 1e-14, 1e-14), config(0.3, 5)))
        out.append(case("twelve decades", quadratic([0.0] * 6, [10.0 ** (12.0 * i / 5) for i in range(6)]), NONE,
                        [1.0] * 6, criteria(5000, 200, 1e-16, 1e-16), config(1.0, 11)))
        out.append(case("a weight of 0", quadratic([0.0] * 5, [1.0, 1.0, 1.0, 1.0, 0.0]), NONE, [1.0] * 5,
                        criteria(5000, 200, 1e-16, 1e-16), config(0.5, 13)))
        d = [[1e12 ** (i / 3.0) if i == j else 0.0 for j in range(4)] for i in range(4)]
        out.append(case("rotated ill-conditioned form", {"type": "quadraticForm", "matrix": rotated(4, d)}, NONE,
                        [1.0] * 4, criteria(3000, 500, 1e-18, 1e-18), config(0.5, 17)))
        q3 = quadratic([3.0, -2.0, 0.5], [1.0, 4.0, 0.25])
        crit = criteria(500, 50, 1e-12, 1e-12)
        out.append(case("the configuration's bounds", q3, NONE, [0.5] * 3, crit,
                        config(0.3, 21, lowerBound=[0.0] * 3, upperBound=[1.0] * 3)))
        out.append(case("an initial mean", q3, NONE, [0.0] * 3, crit, config(0.5, 21, initialMean=[2.0, -1.0, 0.0])))
        out.append(case("a population of 2", q3, NONE, [0.0] * 3, crit, config(0.5, 21, 2)))
        for seed in (1, 3242, 4294967295):
            out.append(case(f"seed {seed}", q3, NONE, [0.0] * 3, crit, config(0.5, seed)))
        out.append(case("3 generations", q3, NONE, [0.0] * 3, criteria(3, 2, 1e-12, 1e-12), config(0.5, 21)))
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/cmaes.hpp", cases)
