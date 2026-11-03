# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SwaptionPseudoDerivative and CapPseudoDerivative
(ql/models/marketmodels/pathwisegreeks/swaptionpseudojacobian.hpp, .cpp), which the wheel doesn't expose, each output
computed by the C++ harness: a swaption's and a cap's implied volatility in a market model and their derivatives with
respect to each step's pseudo-root elements.

FlatVol 20% on six semi-annual rates and five irregular ones, one and three factors, stepping at every rate
time but the last; swaptions on every start and end, displacements 0 and 2%; caps on every start and end at the mean
forward and 1% from it, displacement 0, and at the mean forward, displacement 2%.

Left out (refused by nanoql where QuantLib sums whole steps past the expiry or reads the first displacement alone, and
solved where QuantLib's bracket misses the cap's root; tests/NanoQL.Tests/Conventions/MarketModelGreeksTests.cs,
Divergences/CapPseudoDerivativeBracketTests.cs): evolutions that don't stop at the expiry, unequal displacements, caps
whose root QuantLib's bracket misses (it refuses them).

The harness unit is tools/cxx/src/models/marketmodels/pathwisegreeks/pathwisegreeks.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/SwaptionPseudoJacobianCxxVectorTests.cs."""
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


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in mf.GRIDS:
            n = len(times) - 1
            mean = sum(mf.forwards_of(times)) / n
            for vol in (0.2,):
                for factors in (1, 3):
                    for d in (0.0, 0.02):
                        m = pe.model(times, d, factors, vol)
                        name = f"{label}, FlatVol {vol}, {factors} factors, displacement {d}"
                        for start in range(n):
                            for end in range(start + 1, n + 1):
                                r = H.call("SwaptionPseudoDerivative.values", m, start, end)
                                out.append({"name": f"{name}: swaption ({start}, {end})",
                                            "inputs": {"settings": st, "kind": "Swaption", "model": m,
                                                       "startIndex": start, "endIndex": end},
                                            "expected": {k: v.enc(r[k]) for k in
                                                         ("impliedVolatility", "variance", "expiry",
                                                          "varianceDerivatives", "volatilityDerivatives")}})
                        strikes = [mean - 0.01, mean, mean + 0.01] if d == 0.0 else [mean]
                        for strike in strikes:
                            for start in range(n - 1):
                                for end in (start + 1, n):
                                    try:
                                        r = H.call("CapPseudoDerivative.values", m, strike, start, end, 0.95)
                                    except RuntimeError as e:
                                        if "root not bracketed" not in str(e):
                                            raise
                                        continue
                                    out.append({"name": f"{name}: cap ({start}, {end}) at {strike!r}",
                                                "inputs": {"settings": st, "kind": "Cap", "model": m, "strike": strike,
                                                           "startIndex": start, "endIndex": end, "firstDF": 0.95},
                                                "expected": {k: v.enc(r[k]) for k in
                                                             ("impliedVolatility", "priceDerivatives",
                                                              "volatilityDerivatives")}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/pathwisegreeks/swaptionpseudojacobian.hpp", cases)
