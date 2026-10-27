# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's exponentialCorrelations (ql/models/marketmodels/correlations/expcorrelations.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the exponential correlation matrix of the
rates alive at a time.

Rate-time grids (eight semi-annual rates, six irregular, one rate), long-term correlations and decays (0.5, 0.2),
(0, 1.5) and (1, 0), exponents 1, 0.5 and 0, at times 0, between rate times and on one: the matrix. Throws: a
long-term correlation, decay or exponent outside its range, refused times. The wheel's ExponentialForwardCorrelation
is in tests/vectors/models/marketmodels/correlations/expcorrelations.json.

The harness unit is tools/cxx/src/models/marketmodels/correlations/correlations.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/ExpCorrelationsCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
GRIDS = [
    ("eight semi-annual rates", [0.5 * (i + 1) for i in range(9)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
    ("one rate", [1.0, 2.0]),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            for corr, beta in ((0.5, 0.2), (0.0, 1.5), (1.0, 0.0)):
                for gamma in (1.0, 0.5, 0.0):
                    for t in (0.0, (times[0] + times[1]) / 2, times[1]):
                        out.append({"name": f"{label}, L {corr}, beta {beta}, gamma {gamma}, at {t}",
                                    "inputs": {"settings": st, "call": "matrix", "rateTimes": times,
                                               "longTermCorr": corr, "beta": beta, "gamma": gamma, "t": t},
                                    "expected": {"matrix": v.enc(H.call("exponentialCorrelations", times, corr, beta,
                                                                        gamma, t))}})
            for rlabel, corr, beta, gamma in (("a long-term correlation below 0", -0.1, 0.2, 1.0),
                                              ("a negative decay", 0.5, -0.2, 1.0),
                                              ("an exponent below 0", 0.5, 0.2, -0.5)):
                out.append({"name": f"{label}, refused: {rlabel}",
                            "inputs": {"settings": st, "call": "matrix", "rateTimes": times, "longTermCorr": corr,
                                       "beta": beta, "gamma": gamma, "t": 0.0},
                            "expected": {"matrix": v.attempt(lambda: H.call("exponentialCorrelations", times, corr,
                                                                            beta, gamma, 0.0))}})
        out.append({"name": "refused: a time at zero",
                    "inputs": {"settings": st, "call": "matrix", "rateTimes": [0.0, 1.0], "longTermCorr": 0.5,
                               "beta": 0.2, "gamma": 1.0, "t": 0.0},
                    "expected": {"matrix": v.attempt(lambda: H.call("exponentialCorrelations", [0.0, 1.0], 0.5, 0.2,
                                                                    1.0, 0.0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/correlations/expcorrelations.hpp", cases)
