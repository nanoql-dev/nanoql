# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PseudoRootFacade (ql/models/marketmodels/models/pseudorootfacade.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness.

The facade on seeded pseudo-roots: four and seven rates, one to the rates' factors, undisplaced and displaced, on
semi-annual and irregular rate times; and on the alpha-form calibration's pseudo-roots on QuantLib's case
(capletcoterminalalphacalibration.py): the model's reads (marketmodelspec.hpp's modelResults). Throws: a step's
pseudo-root of the wrong rows or columns, steps other than the rates, rates past the factors times the steps,
displacements other than the rates, rate times fewer than two.

The harness unit is tools/cxx/src/models/marketmodels/models/calibrations.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/PseudoRootFacadeCxxVectorTests.cs."""
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
NAME = "PseudoRootFacade"


def roots(rng, steps, rates, factors):
    return [[[rng.uniform(-0.1, 0.2) for _ in range(factors)] for _ in range(rates)] for _ in range(steps)]


def cases():
    rng = random.Random(20261027)
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in (("four semi-annual rates", [0.5, 1.0, 1.5, 2.0, 2.5]),
                             ("seven irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6, 5.0])):
            n = len(times) - 1
            for factors in sorted({1, 2, n}):
                for d in (0.0, 0.015):
                    pr = roots(rng, n, n, factors)
                    rates = [0.03 + 0.002 * i for i in range(n)]
                    args = [pr, times, rates, [d] * n]
                    out.append({"name": f"{label}, {factors} factors, displaced {d}",
                                "inputs": {"settings": st, "pseudoRoots": pr, "rateTimes": times, "initialRates": rates,
                                           "displacements": [d] * n},
                                "expected": v.enc(H.call(NAME, *args))})
        times = [0.5 * (i + 1) for i in range(11)]
        last = H.call("PiecewiseConstantAbcdVariance", {"a": 0.0, "b": 0.17, "c": 1.0, "d": 0.1, "resetIndex": 9,
                                                        "rateTimes": times})["totalVolatility"][9]
        c = {"rateTimes": times, "forwards": [0.03 + 0.0025 * i for i in range(10)], "longTermCorr": 0.5,
             "beta": 0.2, "abcd": [0.0, 0.17, 1.0, 0.1],
             "capletVols": [0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440, last],
             "displacement": 0.0}
        calibrated = H.call("CTSMMCapletAlphaFormCalibration.calibrate", c, [0.0] * 10, [1.0] * 10, [-1.0] * 10, False,
                            "LinearHyperbolic", 3, 10, 1e-4, 100, 1e-8)
        pr = calibrated["swapPseudoRoots"]
        rates = [0.03 + 0.0025 * i for i in range(10)]
        out.append({"name": "the alpha-form calibration's pseudo-roots",
                    "inputs": {"settings": st, "pseudoRoots": pr, "rateTimes": times, "initialRates": rates,
                               "displacements": [0.0] * 10},
                    "expected": v.enc(H.call(NAME, pr, times, rates, [0.0] * 10))})
        base = roots(rng, 4, 4, 2)
        times = [0.5, 1.0, 1.5, 2.0, 2.5]
        rates = [0.03] * 4
        refusals = {
            "wrongRows": (base[:3] + [base[3][:3]], times, rates, [0.0] * 4),
            "wrongColumns": (base[:3] + [[row[:1] for row in base[3]]], times, rates, [0.0] * 4),
            "stepsNotRates": (base[:3], times, rates, [0.0] * 4),
            "ratesPastFactorsTimesSteps": ([[row[:1] for row in m[:2]] for m in base[:1]], [0.5, 1.0, 1.5],
                                           [0.03, 0.03], [0.0, 0.0]),
            "displacements": (base, times, rates, [0.0] * 3),
            "oneRateTime": ([[[0.1]]], [0.5], [0.03], [0.0]),
        }
        for key, (p, t, r, d) in refusals.items():
            out.append({"name": f"refused: {key}",
                        "inputs": {"settings": st, "pseudoRoots": p, "rateTimes": t, "initialRates": r,
                                   "displacements": d},
                        "expected": {"throws": v.attempt(lambda p=p, t=t, r=r, d=d: H.call(NAME, p, t, r, d))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/pseudorootfacade.hpp", cases)
