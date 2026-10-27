# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LMMDriftCalculator (ql/models/marketmodels/driftcomputation/lmmdriftcalculator.hpp, .cpp):
the drifts of a displaced log-normal LIBOR market model by compute, computePlain and computeReduced, on forwards and
on a curve state.

Pseudo-roots drawn from Python's Mersenne twister (seeded), full-factor and reduced to two factors, on ten
semi-annual, seven quarterly and six irregular rates; displacements 0, 1% and rising from 0.5% to 5%; numeraires the
last bond, the first alive one, and in between; first alive rates 0 and 2. Each form's drifts on the forwards from the
first alive rate, the entries below it the zeros passed; compute's on an LMMCurveState set on them, and again on the
forwards (the calculator keeps its scratch). Throws: each of the constructor's
refusals.

The wheel's calls, by QuantLib-SWIG 1.44: LMMDriftCalculator(pseudo, displacements, taus, numeraire, alive),
compute, computePlain and computeReduced on a DoubleVector of forwards or an LMMCurveState, into a DoubleVector. Read
back by tests/NanoQL.Tests/Vectors/Models/MarketModels/LMMDriftCalculatorVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
GRIDS = [
    ("ten semi-annual rates", [0.5 * (i + 1) for i in range(11)]),
    ("seven quarterly rates", [0.25 * (i + 1) for i in range(8)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
]
FORMS = ["compute", "computePlain", "computeReduced"]


def pseudo_root(rng, n, f):
    return [[round(rng.uniform(-0.08, 0.12), 6) for _ in range(f)] for _ in range(n)]


def matrix(rows):
    m = ql.Matrix(len(rows), len(rows[0]))
    for i, r in enumerate(rows):
        for j, x in enumerate(r):
            m[i][j] = x
    return m


def cases():
    rng = random.Random(v.SEED)
    st = v.settings(R)
    out = []
    for label, times in GRIDS:
        n = len(times) - 1
        taus = [times[i + 1] - times[i] for i in range(n)]
        fwds = [round(0.02 + 0.004 * i + rng.uniform(-0.002, 0.002), 6) for i in range(n)]
        for dlabel, d in (("undisplaced", [0.0] * n), ("displaced 1%", [0.01] * n),
                          ("rising displacements", [0.005 + 0.045 * i / (n - 1) for i in range(n)])):
            for f in sorted({n, 2}):
                pseudo = pseudo_root(rng, n, f)
                for alive in (0, 2):
                    for numeraire in sorted({n, alive, (alive + n) // 2}):
                        c = ql.LMMDriftCalculator(matrix(pseudo), d, taus, numeraire, alive)
                        cs = ql.LMMCurveState(times)
                        cs.setOnForwardRates(fwds)
                        expected = {}
                        for form in FORMS:
                            drifts = ql.DoubleVector(n, 0.0)
                            getattr(c, form)(fwds, drifts)
                            expected[form] = list(drifts)
                        drifts = ql.DoubleVector(n, 0.0)
                        c.compute(cs, drifts)
                        expected["computeState"] = list(drifts)
                        drifts = ql.DoubleVector(n, 0.0)
                        c.compute(fwds, drifts)
                        expected["computeAgain"] = list(drifts)
                        out.append({"name": f"{label}, {dlabel}, {f} factors, alive {alive}, numeraire {numeraire}",
                                    "inputs": {"settings": st, "call": "compute", "pseudo": pseudo,
                                               "displacements": d, "rateTimes": times, "taus": taus,
                                               "forwards": fwds, "numeraire": numeraire, "alive": alive},
                                    "expected": v.enc(expected)})
        pseudo = pseudo_root(rng, n, 2)
        refusals = {
            "noRates": (pseudo, [0.0] * n, [], n, 0),
            "displacementsSize": (pseudo, [0.0] * (n - 1), taus, n, 0),
            "pseudoRows": (pseudo[:-1], [0.0] * n, taus, n, 0),
            "aliveOut": (pseudo, [0.0] * n, taus, n, n),
            "numerairePast": (pseudo, [0.0] * n, taus, n + 1, 0),
            "numeraireBelowAlive": (pseudo, [0.0] * n, taus, 1, 2),
        }
        out.append({"name": f"{label}, refused constructions",
                    "inputs": {"settings": st, "call": "refused", "pseudo": pseudo, "rateTimes": times, "taus": taus},
                    "expected": {k: v.attempt(lambda a=a: ql.LMMDriftCalculator(matrix(a[0]), a[1], a[2], a[3], a[4]))
                                 for k, a in refusals.items()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/driftcomputation/lmmdriftcalculator.hpp", cases)
