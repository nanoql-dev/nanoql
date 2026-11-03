# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FwdPeriodAdapter (ql/models/marketmodels/models/fwdperiodadapter.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness.

The adapter on FlatVol models (marketmodelspec.hpp) of twelve semi-annual and nine quarterly rates, an exponential
correlation, the default evolution times, full and reduced factors, displaced 1% and on rising displacements: in periods
of 2 at offsets 0 and 1, of 3 at offsets 0 and 2, and of 4 at offset 1, with one displacement given and with one per
long rate. Its reads: the long initial rates and displacements, the evolution's rate times, evolution times and first
alive rates, and its counts. Left out: the pseudo-roots, on QuantLib's Y matrix
(tests/NanoQL.Tests/Divergences/ForwardForwardMappingsTests.cs), and the displacements' default with none given
(tests/NanoQL.Tests/Divergences/FwdPeriodAdapterTests.cs). Throws: a period of 0, an offset not below the period, a
wrong count of displacements, a long rate time that isn't an evolution time.

The harness unit is tools/cxx/src/models/marketmodels/models/adapters.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/FwdPeriodAdapterCxxVectorTests.cs."""
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
    ("twelve semi-annual rates", [0.5 * (i + 1) for i in range(13)]),
    ("nine quarterly rates", [0.25 * (i + 1) for i in range(10)]),
]
PERIODS = [(2, 0), (2, 1), (3, 0), (3, 2), (4, 1)]


def model(times, evol, factors, d):
    n = len(times) - 1
    corr = {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0, "times": []}
    return {"type": "FlatVol", "vols": [0.18 + 0.005 * i for i in range(n)], "corr": corr, "rateTimes": times,
            "evolutionTimes": evol, "factors": factors, "initialRates": [0.03 + 0.001 * i for i in range(n)],
            "displacements": d}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            for factors in sorted({n, 3}):
                for dlabel, d in (("displaced 1%", [0.01] * n), ("rising displacements",
                                                                  [0.001 * (i + 1) for i in range(n)])):
                    m = model(times, list(times[:-1]), factors, d)
                    for period, offset in PERIODS:
                        k = (n - offset) // period
                        for glabel, given in (("one displacement", [0.015]),
                                              ("one per long rate", [0.01 + 0.002 * i for i in range(k)])):
                            out.append({"name": f"{label}, {factors} factors, {dlabel}, period {period}, offset "
                                                f"{offset}, {glabel}",
                                        "inputs": {"settings": st, "call": "adapter", "model": m, "period": period,
                                                   "offset": offset, "newDisplacements": given},
                                        "expected": v.enc(H.call("FwdPeriodAdapter", m, period, offset, given))})
            m = model(times, list(times[:-1]), n, [0.01] * n)
            refusals = {"periodZero": [0, 0, [0.01]], "offsetNotBelowPeriod": [2, 2, [0.01]],
                        "displacementCount": [2, 0, [0.01, 0.01]],
                        "skippedLongRateTime": None}
            skip = model(times, list(times[:-1])[1::2], n, [0.01] * n)
            out.append({"name": f"{label}, refused adapters",
                        "inputs": {"settings": st, "call": "refused", "model": m, "skipModel": skip,
                                   "refusals": {k: a for k, a in refusals.items() if a is not None}},
                        "expected": {**{k: v.attempt(lambda a=a: H.call("FwdPeriodAdapter", m, *a))
                                        for k, a in refusals.items() if a is not None},
                                     "skippedLongRateTime": v.attempt(lambda: H.call("FwdPeriodAdapter", skip, 3, 0,
                                                                                     [0.01]))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/fwdperiodadapter.hpp", cases)
