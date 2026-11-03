# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VegaBumpCluster and VegaBumpCollection
(ql/models/marketmodels/pathwisegreeks/vegabumpcluster.hpp, .cpp), which the wheel doesn't expose, each output computed
by the C++ harness: a cluster's intersection with another and its compatibility with a model; the default collection's
clusters and checks.

FlatVol 20% on six semi-annual rates and five irregular ones, one and three factors, stepping at every rate time but the
last; clusters drawn within the model's factors, rates and steps and past them; the default collection factor by factor
and by rate.

Left out (nanoql's checks are true where no element fails;
tests/NanoQL.Tests/Divergences/VegaBumpCollectionCheckTests.cs): the checks of given clusters.

The harness unit is tools/cxx/src/models/marketmodels/pathwisegreeks/pathwisegreeks.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/VegaBumpClusterCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "products", "multistep"))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import pathwiseaccountingengine as pe  # noqa: E402
import vectors as v  # noqa: E402

R = 46322


def draw(rnd, factors, n, steps):
    f = rnd.randrange(0, factors + 1)
    r = rnd.randrange(0, n)
    s = rnd.randrange(0, steps)
    return [f, f + rnd.randrange(1, 3), r, r + rnd.randrange(1, 3), s, min(s + rnd.randrange(1, 3), steps)]


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in mf.GRIDS:
            n = len(times) - 1
            for factors in (1, 3):
                m = pe.model(times, 0.0, factors)
                for _ in range(12):
                    c, d = draw(rnd, factors, n, n), draw(rnd, factors, n, n)
                    r = H.call("VegaBumpCluster.checks", c, d, m)
                    out.append({"name": f"{label}, {factors} factors: cluster {c} against {d}",
                                "inputs": {"settings": st, "kind": "Cluster", "cluster": c, "comparee": d, "model": m},
                                "expected": {"doesIntersect": r["doesIntersect"], "isCompatible": r["isCompatible"]}})
                for factorwise in (True, False):
                    r = H.call("VegaBumpCollection.bumps", m, factorwise)
                    out.append({"name": f"{label}, {factors} factors: the default collection, factorwise {factorwise}",
                                "inputs": {"settings": st, "kind": "Collection", "model": m,
                                           "factorwise": factorwise},
                                "expected": {"numberBumps": r["numberBumps"], "allBumps": r["allBumps"],
                                             "isFull": r["isFull"], "isNonOverlapping": r["isNonOverlapping"],
                                             "isSensible": r["isSensible"]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/pathwisegreeks/vegabumpcluster.hpp", cases)
