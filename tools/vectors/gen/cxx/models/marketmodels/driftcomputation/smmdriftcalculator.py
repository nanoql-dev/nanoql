# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SMMDriftCalculator (ql/models/marketmodels/driftcomputation/smmdriftcalculator.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the drifts of a coterminal swap market model.

Pseudo-roots drawn from Python's Mersenne twister (seeded), full-factor and reduced to two factors, on ten
semi-annual, seven quarterly and six irregular rates; coterminal swap rates around 3% to 5%; displacements 0, 1% and
rising from 0.5% to 5%; numeraires the last bond, the first alive one, and in between; first alive rates 0 and 2. The
drifts of a CoterminalSwapCurveState set on the rates from index 0, computed twice on one calculator ("drifts",
"again"). Throws: each of the constructor's refusals.

Left out: a state set from the first alive rate, past index 0, which QuantLib refuses ("invalid index") and nanoql
computes (the S-mm plan's MR3 (xvi); tests/NanoQL.Tests/Divergences/SmmDriftAliveTests.cs).

The harness unit is tools/cxx/src/models/marketmodels/driftcomputation/driftcalculators.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/SMMDriftCalculatorCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
GRIDS = [
    ("ten semi-annual rates", [0.5 * (i + 1) for i in range(11)]),
    ("seven quarterly rates", [0.25 * (i + 1) for i in range(8)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
]


def pseudo_root(rng, n, f):
    return [[round(rng.uniform(-0.08, 0.12), 6) for _ in range(f)] for _ in range(n)]


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            rates = [round(0.03 + 0.002 * i + rng.uniform(-0.002, 0.002), 6) for i in range(n)]
            for dlabel, d in (("undisplaced", [0.0] * n), ("displaced 1%", [0.01] * n),
                              ("rising displacements", [0.005 + 0.045 * i / (n - 1) for i in range(n)])):
                for f in sorted({n, 2}):
                    pseudo = pseudo_root(rng, n, f)
                    for alive in (0, 2):
                        for numeraire in sorted({n, alive, (alive + n) // 2}):
                            r = H.call("SMMDriftCalculator.compute", pseudo, d, times, numeraire, alive, rates)
                            out.append({"name": f"{label}, {dlabel}, {f} factors, alive {alive}, numeraire {numeraire}",
                                        "inputs": {"settings": st, "call": "compute", "pseudo": pseudo,
                                                   "displacements": d, "rateTimes": times, "swapRates": rates,
                                                   "numeraire": numeraire, "alive": alive},
                                        "expected": v.enc(r)})
            pseudo = pseudo_root(rng, n, 2)
            z = [0.0] * n
            refusals = {"displacementsSize": (pseudo, z[:-1], n, 0), "pseudoRows": (pseudo[:-1], z, n, 0),
                        "aliveOut": (pseudo, z, n, n), "numerairePast": (pseudo, z, n + 1, 0),
                        "numeraireBelowAlive": (pseudo, z, 1, 2)}
            out.append({"name": f"{label}, refused constructions",
                        "inputs": {"settings": st, "call": "refused", "pseudo": pseudo, "rateTimes": times,
                                   "swapRates": rates},
                        "expected": {k: v.attempt(lambda a=a: H.call("SMMDriftCalculator.compute", a[0], a[1], times,
                                                                     a[2], a[3], rates))
                                     for k, a in refusals.items()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/driftcomputation/smmdriftcalculator.hpp", cases)
