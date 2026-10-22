# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GenericRiskStatistics (ql/math/statistics/riskstatistics.hpp), the empirical risk measures
over GeneralStatistics, through the wheel's RiskStatistics: data sets of losses and gains (Gaussian samples around a
negative, a zero and a positive mean, with unit and with random weights), a set of gains only, sets whose tail is an
atom of equal losses, a set of 1 and of 0 samples; each read through semiVariance, semiDeviation, downsideVariance,
downsideDeviation, and at targets and percentiles regret, shortfall, averageShortfall, potentialUpside, valueAtRisk
and expectedShortfall, the percentiles' refusals outside [0.9, 1) among them.

expectedShortfall is left out where QuantLib refuses "no data below the target" on a set that isn't empty: with no
loss, and where the tail is an atom at the value-at-risk's level, the port returns the level
(Divergences/ExpectedShortfallNoLossTests.cs). The wheel's RiskStatistics binds add, reset, the general members and
these, each the C++ class's. Read back by tests/NanoQL.Tests/Vectors/Numerics/Statistics/
RiskStatisticsVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TARGETS = [-1.0, -0.1, 0.0, 0.25, 1.0]
PERCENTILES = [0.9, 0.95, 0.975, 0.99, 0.999, 0.5, 1.0]


def data_sets(rnd):
    sets = []
    for mean in (-0.5, 0.0, 0.5):
        data = [rnd.gauss(mean, 1.0) for _ in range(1000)]
        sets.append((f"Gaussian around {mean!r}, unit weights", data, [1.0] * len(data)))
        sets.append((f"Gaussian around {mean!r}, random weights", data, [rnd.uniform(0.1, 3.0) for _ in data]))
    gains = [abs(rnd.gauss(1.0, 0.5)) for _ in range(200)]
    sets.append(("gains only", gains, [1.0] * len(gains)))
    for losses in (5, 6, 10):
        data = [-10.0] * losses + [rnd.uniform(0.0, 5.0) for _ in range(100 - losses)]
        rnd.shuffle(data)
        sets.append((f"{losses} losses of -10 among 100", data, [1.0] * len(data)))
    sets.append(("one", [-0.5], [1.0]))
    sets.append(("empty", [], []))
    return sets


def shortfall_mean(s, p):
    """expectedShortfall, left out (None) where QuantLib refuses "no data below the target" on a set with samples."""
    try:
        return v.enc(s.expectedShortfall(p))
    except RuntimeError as e:
        if "no data below the target" in str(e):
            return None
        return dict(v.THROWS)


def cases():
    st = v.settings(ql.Date(22, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for name, data, weights in data_sets(rnd):
        s = ql.RiskStatistics()
        for x, w in zip(data, weights):
            s.add(x, w)
        expected = {m: v.attempt(lambda m=m: getattr(s, m)())
                    for m in ("semiVariance", "semiDeviation", "downsideVariance", "downsideDeviation")}
        for m in ("regret", "shortfall", "averageShortfall"):
            expected[m] = [v.attempt(lambda m=m, t=t: getattr(s, m)(t)) for t in TARGETS]
        for m in ("potentialUpside", "valueAtRisk"):
            expected[m] = [v.attempt(lambda m=m, p=p: getattr(s, m)(p)) for p in PERCENTILES]
        es = [shortfall_mean(s, p) for p in PERCENTILES]
        kept = [i for i, x in enumerate(es) if x is not None]
        expected["expectedShortfall"] = [es[i] for i in kept]
        out.append({"name": name,
                    "inputs": {"settings": st, "data": data, "weights": weights, "targets": TARGETS,
                               "percentiles": PERCENTILES,
                               "expectedShortfallPercentiles": [PERCENTILES[i] for i in kept]},
                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/statistics/riskstatistics.hpp", cases)
