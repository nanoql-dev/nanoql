# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's RatePseudoRootJacobianNumerical, RatePseudoRootJacobian and RatePseudoRootJacobianAllElements
(ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.hpp, .cpp), which the wheel doesn't expose, each output
computed by the C++ harness: the derivatives of a log-normal Euler step's end rates along pseudo-root bumps.

Pseudo-roots and Gaussians drawn at random, three factors, each step's first alive rate its numeraire; the end rates the
numerical form's from a zero bump; bumps of each element, 1e-6, and random directions. The numerical form on six
semi-annual rates and five irregular ones, displacements 0 and 2%; the analytic forms on the semi-annual rates,
displacement 0.

Left out (nanoql's analytic derivative sums each term over its own period and carries the displaced rate, the S-mmp
plan's MMPR4 (iv); tests/NanoQL.Tests/Divergences/RatePseudoRootJacobianTests.cs): the analytic forms on unequal
periods or displacements. On periods of 0.5 and no displacement QuantLib's arithmetic and nanoql's agree bit for bit.

The harness unit is tools/cxx/src/models/marketmodels/pathwisegreeks/pathwisegreeks.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/RatePseudoRootJacobianCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "products", "multistep"))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import vectors as v  # noqa: E402

R = 46322
F = 3


def bumps(rnd, n):
    out = []
    for r in (0, n // 2, n - 1):
        for f in range(F):
            out.append([[1e-6 if (k == r and g == f) else 0.0 for g in range(F)] for k in range(n)])
    out.append([[rnd.uniform(-0.01, 0.01) for _ in range(F)] for _ in range(n)])
    return out


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in mf.GRIDS:
            n = len(times) - 1
            taus = mf.accruals_of(times)
            f0 = mf.forwards_of(times)
            for d in (0.0, 0.02):
                for alive in (0, n // 2):
                    root = [[0.0 if k < alive else rnd.uniform(0.02, 0.15) for _ in range(F)] for k in range(n)]
                    g = [rnd.gauss(0.0, 1.0) for _ in range(F)]
                    dfs = [1.0] + [1.0 / (1.0 + f * t) for f, t in zip(f0, taus)]
                    zero = [[[0.0] * F for _ in range(n)]]
                    disp = [d] * n
                    end = H.call("RatePseudoRootJacobian.bumps", "Numerical", root, alive, alive, taus, zero, disp, f0,
                                 dfs, [0.0] * n, g)[0]
                    end = [end[k] if k >= alive else f0[k] for k in range(n)]
                    b = bumps(rnd, n)
                    kinds = ["Numerical"]
                    if label == mf.GRIDS[0][0] and d == 0.0:
                        kinds += ["Analytic", "AllElements"]
                    for kind in kinds:
                        args = [kind, root, alive, alive, taus, b, disp, f0, dfs, end, g]
                        r = H.call("RatePseudoRootJacobian.bumps", *args)
                        out.append({"name": f"{label}, displacement {d}, alive from {alive}: {kind}",
                                    "inputs": {"settings": st, "kind": kind, "pseudoRoot": root, "aliveIndex": alive,
                                               "numeraire": alive, "taus": taus, "pseudoBumps": b,
                                               "displacements": disp, "oldRates": f0, "oneStepDFs": dfs,
                                               "newRates": end, "gaussians": g},
                                    "expected": {"bumps": v.enc(r)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/pathwisegreeks/ratepseudorootjacobian.hpp", cases)
