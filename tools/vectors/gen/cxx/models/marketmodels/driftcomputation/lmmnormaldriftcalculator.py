# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LMMNormalDriftCalculator
(ql/models/marketmodels/driftcomputation/lmmnormaldriftcalculator.hpp, .cpp), which the wheel doesn't expose, each
output computed by the C++ harness: the drifts of a normal LIBOR market model by compute, computePlain and
computeReduced.

Pseudo-roots drawn from Python's Mersenne twister (seeded), full-factor and reduced to two factors, on ten
semi-annual, seven quarterly and six irregular rates and forwards around 2% to 6%; numeraires the last bond, the first
alive one, and in between; first alive rates 0 and 2. Each form's drifts from the first alive rate over zeros, computed
twice on one calculator ("drifts", "again": it keeps its scratch). Throws: each of the constructor's refusals.

The harness unit is tools/cxx/src/models/marketmodels/driftcomputation/driftcalculators.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/LMMNormalDriftCalculatorCxxVectorTests.cs."""
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
            taus = [times[i + 1] - times[i] for i in range(n)]
            fwds = [round(0.02 + 0.004 * i + rng.uniform(-0.002, 0.002), 6) for i in range(n)]
            for f in sorted({n, 2}):
                pseudo = pseudo_root(rng, n, f)
                for alive in (0, 2):
                    for numeraire in sorted({n, alive, (alive + n) // 2}):
                        expected = {}
                        for form in ("compute", "computePlain", "computeReduced"):
                            r = H.call("LMMNormalDriftCalculator.compute", pseudo, taus, numeraire, alive, fwds, form)
                            expected[form] = r["drifts"]
                            expected[form + "Again"] = r["again"]
                        out.append({"name": f"{label}, {f} factors, alive {alive}, numeraire {numeraire}",
                                    "inputs": {"settings": st, "call": "compute", "pseudo": pseudo, "taus": taus,
                                               "forwards": fwds, "numeraire": numeraire, "alive": alive},
                                    "expected": v.enc(expected)})
            pseudo = pseudo_root(rng, n, 2)
            refusals = {"noRates": (pseudo, [], n, 0), "pseudoRows": (pseudo[:-1], taus, n, 0),
                        "aliveOut": (pseudo, taus, n, n), "numerairePast": (pseudo, taus, n + 1, 0),
                        "numeraireBelowAlive": (pseudo, taus, 1, 2)}
            out.append({"name": f"{label}, refused constructions",
                        "inputs": {"settings": st, "call": "refused", "pseudo": pseudo, "taus": taus},
                        "expected": {k: v.attempt(lambda a=a: H.call("LMMNormalDriftCalculator.compute", a[0], a[1],
                                                                     a[2], a[3], [0.03] * len(a[1]), "compute"))
                                     for k, a in refusals.items()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/driftcomputation/lmmnormaldriftcalculator.hpp", cases)
