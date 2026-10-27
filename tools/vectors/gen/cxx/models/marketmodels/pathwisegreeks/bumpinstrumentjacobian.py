# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VolatilityBumpInstrumentJacobian and OrthogonalizedBumpFinder
(ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.hpp, .cpp), which the wheel doesn't expose, each output
computed by the C++ harness: swaptions' and caps' implied volatilities' derivatives along a collection's bumps, their
one-percent bumps, and the orthogonalized pseudo-root bumps.

FlatVol 20% on six semi-annual rates and five irregular ones, one and two factors, stepping at every rate time but the
last; the default collection factor by factor and by rate; coterminal swaptions and caps over thirds of the rates at the
first forward, testPathwiseMarketVegas' instruments, and the swaptions alone; the finder at its multiplier 50 and
tolerance 1e-6, and at 2 and 1e-2.

Left out (nanoql refuses at every read where a pseudo-derivative refuses, the S-mmp plan's MMPR4 (xx);
tests/NanoQL.Tests/Divergences/VolatilityBumpInstrumentJacobianTests.cs): refused instruments.

The harness unit is tools/cxx/src/models/marketmodels/pathwisegreeks/pathwisegreeks.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/BumpInstrumentJacobianCxxVectorTests.cs."""
import os
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


def instruments(times):
    n = len(times) - 1
    f0 = mf.forwards_of(times)
    swaptions = [[i, n] for i in range(n)]
    caps = [[i, min(i + 2, n), f0[0]] for i in range(0, n - 1, 2)]
    return [("swaptions and caps", swaptions, caps), ("swaptions", swaptions, [])]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in mf.GRIDS:
            for factors in (1, 2):
                m = pe.model(times, 0.0, factors)
                for factorwise in (True, False):
                    for ilabel, swaptions, caps in instruments(times):
                        name = f"{label}, {factors} factors, factorwise {factorwise}: {ilabel}"
                        r = H.call("VolatilityBumpInstrumentJacobian.values", m, factorwise, swaptions, caps)
                        out.append({"name": name,
                                    "inputs": {"settings": st, "kind": "Jacobian", "model": m,
                                               "factorwise": factorwise, "swaptions": swaptions, "caps": caps},
                                    "expected": {k: v.enc(r[k]) for k in
                                                 ("derivativesVolatility", "onePercentBumps", "allOnePercentBumps")}})
                        for cut, tol in ((50.0, 1e-6), (2.0, 1e-2)):
                            r = H.call("OrthogonalizedBumpFinder.bumps", m, factorwise, swaptions, caps, cut, tol)
                            out.append({"name": f"{name}, the finder at {cut!r} and {tol!r}",
                                        "inputs": {"settings": st, "kind": "Finder", "model": m,
                                                   "factorwise": factorwise, "swaptions": swaptions, "caps": caps,
                                                   "multiplierCutOff": cut, "tolerance": tol},
                                        "expected": {"bumps": v.enc(r)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/pathwisegreeks/bumpinstrumentjacobian.hpp", cases)
