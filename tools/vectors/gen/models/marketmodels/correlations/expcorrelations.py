# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ExponentialForwardCorrelation (ql/models/marketmodels/correlations/expcorrelations.hpp,
.cpp): its times, rate times, number of rates and correlation matrices.

Rate-time grids (eight semi-annual rates, seven quarterly, six irregular, two rates), long-term correlations and
decays (0.5, 0.2), (0, 1.5) and (1, 0), exponents 1 (the time-homogeneous case, on the default times), 0.5 (on the
default times and on finer times, the rate times with their midpoints, which hold every rate time but the last) and 0:
times(), rateTimes(), numberOfRates(), correlations() and correlation(i) at the first and last periods. Throws: an exponent of 1 on other
times, a last correlation time after the last rate time, a long-term correlation, decay or exponent outside its range,
decreasing rate times.

Left out (the S-mm plan's MR3 and MR4; tests/NanoQL.Tests/Divergences/ExponentialCorrelationTests.cs): one rate, which
QuantLib refuses as "at least two values" and nanoql accepts; correlation times skipping a rate time at an exponent
other than 1, which nanoql refuses, QuantLib's own FIXME's check, and QuantLib prices with a dead rate's period. The
refusal's text of a last correlation time past the last rate time differs (MR3 (xv)), and only the throw is recorded.

The wheel's calls, by QuantLib-SWIG 1.44: ExponentialForwardCorrelation(rateTimes, longTermCorr, beta, gamma, times)
and PiecewiseConstantCorrelation's members. Read back by
tests/NanoQL.Tests/Vectors/Models/MarketModels/ExponentialForwardCorrelationVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
GRIDS = [
    ("eight semi-annual rates", [0.5 * (i + 1) for i in range(9)]),
    ("seven quarterly rates", [0.25 * (i + 1) for i in range(8)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
    ("two rates", [1.0, 2.0, 3.0]),
]


def rows(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def reads(c):
    return {"times": list(c.times()), "rateTimes": list(c.rateTimes()), "numberOfRates": c.numberOfRates(),
            "correlations": [rows(m) for m in c.correlations()],
            "correlation": [rows(c.correlation(i)) for i in sorted({0, len(c.times()) - 1})]}


def cases():
    st = v.settings(R)
    out = []
    for label, times in GRIDS:
        fix = times[:-1]
        finer = sorted(set(fix) | {(a + b) / 2 for a, b in zip([0.0] + fix[:-1], fix)})
        for corr, beta in ((0.5, 0.2), (0.0, 1.5), (1.0, 0.0)):
            for gamma, gtimes in ((1.0, [("default times", [])]),
                                  (0.5, [("default times", []), ("finer times", finer)]),
                                  (0.0, [("default times", [])])):
                for tlabel, t in gtimes:
                    c = ql.ExponentialForwardCorrelation(times, corr, beta, gamma, t)
                    out.append({"name": f"{label}, L {corr}, beta {beta}, gamma {gamma}, {tlabel}",
                                "inputs": {"settings": st, "call": "correlation", "rateTimes": times,
                                           "longTermCorr": corr, "beta": beta, "gamma": gamma, "times": t},
                                "expected": v.enc(reads(c))})
        refusals = [("gamma 1 on other times", 0.5, 0.2, 1.0, finer),
                    ("a last time after the last rate time", 0.5, 0.2, 0.5, fix + [times[-1] + 0.5]),
                    ("a long-term correlation above 1", 1.5, 0.2, 0.5, []),
                    ("a negative decay", 0.5, -0.1, 0.5, []),
                    ("an exponent above 1", 0.5, 0.2, 1.5, [])]
        for rlabel, corr, beta, gamma, t in refusals:
            out.append({"name": f"{label}, refused: {rlabel}",
                        "inputs": {"settings": st, "call": "refused", "rateTimes": times, "longTermCorr": corr,
                                   "beta": beta, "gamma": gamma, "times": t},
                        "expected": {"construction": v.attempt(
                            lambda: ql.ExponentialForwardCorrelation(times, corr, beta, gamma, t))}})
    out.append({"name": "refused: decreasing rate times",
                "inputs": {"settings": st, "call": "refused", "rateTimes": [1.0, 0.5, 2.0], "longTermCorr": 0.5,
                           "beta": 0.2, "gamma": 1.0, "times": []},
                "expected": {"construction": v.attempt(
                    lambda: ql.ExponentialForwardCorrelation([1.0, 0.5, 2.0], 0.5, 0.2, 1.0, []))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/correlations/expcorrelations.hpp", cases)
