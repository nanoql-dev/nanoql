# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SwapForwardMappings (ql/models/marketmodels/swapforwardmappings.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness.

LIBOR, coterminal and constant-maturity curve states (spanning 2) on seven semi-annual, five irregular rates and one
rate, displacements 0 and 1.5%: annuities and swap derivatives over every swap, the coterminal Jacobian and Z matrix,
the coinitial Jacobian and the coinitial Z matrix's entries from each row's diagonal on, and the constant-maturity
Jacobians and Z matrices at spannings 0, 1, 2, 3 and past the rates. swaptionImpliedVolatility on FlatVol models of
equal displacements whose evolution times hold each swaption's expiry: every rate time; every other one with the
swaptions expiring on them; and finer ones. Throws: a start index not before the end.

Left out (the S-mm plan's MR3 (xxii) and MR4 (a); tests/NanoQL.Tests/Divergences/CoinitialZedMatrixTests.cs and
SwaptionImpliedVolatilityGridTests.cs): the coinitial Z matrix's entries left of its diagonal, which nanoql scales and
QuantLib leaves raw; swaptions whose expiry the evolution times don't hold, and models of unequal displacements, which
nanoql refuses.

The harness unit is tools/cxx/src/models/marketmodels/swapforwardmappings.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/SwapForwardMappingsCxxVectorTests.cs."""
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
    ("seven semi-annual rates", [0.5 * (i + 1) for i in range(8)]),
    ("five irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 3.1]),
    ("one rate", [1.0, 2.0]),
]
SPANS = [0, 1, 2, 3, 10]


def flat_vol(times, evol, d):
    n = len(times) - 1
    if n > 1:
        corr = {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0,
                "times": []}
    else:
        corr = {"type": "TimeHomogeneous", "rateTimes": times, "fwdCorrelation": [[1.0]]}
    return {"type": "FlatVol", "vols": [0.2 + 0.005 * i for i in range(n)], "corr": corr, "rateTimes": times,
            "evolutionTimes": evol, "factors": n, "initialRates": [0.03 + 0.0025 * i for i in range(n)],
            "displacements": [d] * n}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            fwds = [0.03 + 0.0025 * i for i in range(n)]
            cot = [0.035 - 0.001 * i for i in range(n)]
            for kind, values, spanning in (("LMMForwards", fwds, 0), ("Coterminal", cot, 0), ("CMSwap", fwds, 2)):
                for d in (0.0, 0.015):
                    out.append({"name": f"{label}, {kind}, displacement {d}",
                                "inputs": {"settings": st, "call": "all", "kind": kind, "rateTimes": times,
                                           "values": values, "spanning": spanning, "displacement": d,
                                           "spans": SPANS},
                                "expected": v.enc(H.call("SwapForwardMappings.all", kind, times, values, spanning, d,
                                                         SPANS))})
            fix = times[:-1]
            finer = sorted(set(fix) | {(a + b) / 2 for a, b in zip([0.0] + fix[:-1], fix)})
            grids = [("every rate time", fix, range(n)), ("finer times", finer, range(n)),
                     ("every other rate time", fix[1::2] if n > 1 else fix, range(1, n, 2) if n > 1 else range(n))]
            for glabel, evol, starts in grids:
                for d in (0.0, 0.01):
                    m = flat_vol(times, list(evol), d)
                    pairs = [(s, e) for s in starts for e in range(s + 1, n + 1)]
                    out.append({"name": f"{label}, swaption volatilities on {glabel}, displacement {d}",
                                "inputs": {"settings": st, "call": "swaptionVolatility", "model": m,
                                           "pairs": [list(p) for p in pairs]},
                                "expected": {"volatilities": v.enc([H.call(
                                    "SwapForwardMappings::swaptionImpliedVolatility", m, s, e) for s, e in pairs]),
                                    "reversed": v.attempt(lambda: H.call(
                                        "SwapForwardMappings::swaptionImpliedVolatility", m, n, n))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/swapforwardmappings.hpp", cases)
