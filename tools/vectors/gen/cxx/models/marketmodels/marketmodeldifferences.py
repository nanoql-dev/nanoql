# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's market-model differences (ql/models/marketmodels/marketmodeldifferences.hpp, .cpp), which
the wheel doesn't expose, each output computed by the C++ harness: rateVolDifferences and rateInstVolDifferences of two
FlatVol or AbcdVol models on the same initial rates and evolution, at each rate's index.

Models on six semi-annual rates and on five irregular ones, evolved on their rate times, at one, three and full factors;
and two models whose initial rates differ, and two whose evolution times differ (refused).

Left out (nanoql divides by the rate's reset time and checks the index against the rates, the S-mmp plan's MMPR4
(xiii) and (xiv); tests/NanoQL.Tests/Divergences/MarketModelDifferencesTests.cs): evolution times off the rate times.

The harness unit is tools/cxx/src/models/marketmodels/marketmodeldifferences.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MarketModelDifferencesCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46322
GRIDS = [
    ("six semi-annual rates", [0.5 * (i + 1) for i in range(7)]),
    ("five irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 3.1]),
]


def model(times, factors, vol, rates=None, evolution=None, abcd=None):
    n = len(times) - 1
    corr = {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0, "times": []}
    m = {"type": "FlatVol", "vols": [vol + 0.01 * i for i in range(n)], "corr": corr, "rateTimes": times,
         "evolutionTimes": evolution or times[:-1], "factors": factors,
         "initialRates": rates or [0.03 + 0.002 * i for i in range(n)], "displacements": [0.0] * n}
    if abcd is not None:
        m["type"] = "AbcdVol"
        m["abcd"] = abcd
        m["ks"] = m.pop("vols")
    return m


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            for factors in (1, 3, n):
                pairs = [("FlatVol", model(times, factors, 0.25), model(times, factors, 0.2)),
                         ("AbcdVol", model(times, factors, 1.1, abcd=[0.02, 0.1, 0.6, 0.15]),
                          model(times, factors, 0.9, abcd=[0.02, 0.1, 0.6, 0.15]))]
                for kind, m1, m2 in pairs:
                    out.append({"name": f"{label}, {factors} factors, {kind}",
                                "inputs": {"settings": st, "model1": m1, "model2": m2},
                                "expected": {
                                    "rateVolDifferences": [v.enc(x) for x in H.call("rateVolDifferences", m1, m2)],
                                    "rateInstVolDifferences": [
                                        [v.enc(x) for x in H.call("rateInstVolDifferences", m1, m2, i)]
                                        for i in range(n)]}})
            m1 = model(times, 3, 0.25)
            for name, m2 in (("other initial rates", model(times, 3, 0.2, rates=[0.04] * n)),
                             ("other evolution times", model(times, 3, 0.2, evolution=times[:2]))):
                out.append({"name": f"{label}: {name}", "inputs": {"settings": st, "model1": m1, "model2": m2},
                            "expected": {
                                "rateVolDifferences": v.attempt(lambda m2=m2: H.call("rateVolDifferences", m1, m2)),
                                "rateInstVolDifferences": v.attempt(lambda m2=m2: H.call(
                                    "rateInstVolDifferences", m1, m2, 0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/marketmodeldifferences.hpp", cases)
