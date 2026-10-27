# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's curve-state conversions (ql/models/marketmodels/curvestate.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness: forwardsFromDiscountRatios, coterminalFromDiscountRatios and
constantMaturityFromDiscountRatios.

Discount ratios of forwards on ten semi-annual, seven quarterly, six irregular and one rate, written from first valid
indexes 0, 2 and the last rate over buffers holding a marker (QuantLib's functions leave the entries below the index
as they are); the constant-maturity swaps at spannings 0, 1, 2, 3 and past the rates. Throws: each size check.

Left out (nanoql refuses them, the S-mm plan's MR3 (xx)): coterminal swaps of no rates, whose last annuity QuantLib
writes before its vectors; a constant-maturity first valid index at or past the swaps, written past them.

The harness unit is tools/cxx/src/models/marketmodels/curvestate.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/CurveStateCxxVectorTests.cs."""
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
    ("ten semi-annual rates", [0.5 * (i + 1) for i in range(11)]),
    ("seven quarterly rates", [0.25 * (i + 1) for i in range(8)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
    ("one rate", [1.0, 2.0]),
]
MARK = -7.0


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            taus = [times[i + 1] - times[i] for i in range(n)]
            ds = [1.0]
            for i in range(n):
                ds.append(ds[-1] / (1.0 + (0.027 + 0.0031 * i) * taus[i]))
            ds = [x * 1.02 / ds[-1] for x in ds]
            for first in sorted({0, min(2, n - 1), n - 1}):
                base = {"settings": st, "rateTimes": times, "ds": ds, "taus": taus, "first": first, "mark": MARK}
                out.append({"name": f"{label}, forwards from {first}",
                            "inputs": {**base, "call": "forwards"},
                            "expected": {"fwds": v.enc(H.call("forwardsFromDiscountRatios", first, ds, taus,
                                                               [MARK] * n))}})
                r = H.call("coterminalFromDiscountRatios", first, ds, taus, [MARK] * n, [MARK] * n)
                out.append({"name": f"{label}, coterminal swaps from {first}",
                            "inputs": {**base, "call": "coterminal"},
                            "expected": v.enc(r)})
                for span in sorted({0, 1, 2, 3, n + 3}):
                    r = H.call("constantMaturityFromDiscountRatios", span, first, ds, taus, [MARK] * n, [MARK] * n)
                    out.append({"name": f"{label}, constant-maturity swaps spanning {span} from {first}",
                                "inputs": {**base, "call": "constantMaturity", "span": span},
                                "expected": v.enc(r)})
            out.append({"name": f"{label}, size checks",
                        "inputs": {"settings": st, "call": "sizes", "ds": ds, "taus": taus},
                        "expected": {
                            "forwardsTaus": v.attempt(lambda: H.call("forwardsFromDiscountRatios", 0, ds, taus[:-1],
                                                                     [MARK] * n)),
                            "forwardsDs": v.attempt(lambda: H.call("forwardsFromDiscountRatios", 0, ds[:-1], taus,
                                                                   [MARK] * n)),
                            "coterminalTaus": v.attempt(lambda: H.call("coterminalFromDiscountRatios", 0, ds,
                                                                       taus[:-1], [MARK] * n, [MARK] * n)),
                            "coterminalAnnuities": v.attempt(lambda: H.call("coterminalFromDiscountRatios", 0, ds, taus,
                                                                            [MARK] * n, [MARK] * (n + 1))),
                            "coterminalDs": v.attempt(lambda: H.call("coterminalFromDiscountRatios", 0, ds + [1.0],
                                                                     taus, [MARK] * n, [MARK] * n)),
                            "constantMaturityTaus": v.attempt(lambda: H.call("constantMaturityFromDiscountRatios", 1,
                                                                             0, ds, taus[:-1], [MARK] * n,
                                                                             [MARK] * n)),
                            "constantMaturityAnnuities": v.attempt(lambda: H.call(
                                "constantMaturityFromDiscountRatios", 1, 0, ds, taus, [MARK] * n, [MARK] * (n + 1))),
                            "constantMaturityDs": v.attempt(lambda: H.call("constantMaturityFromDiscountRatios", 1, 0,
                                                                           ds[:-1], taus, [MARK] * n, [MARK] * n))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/curvestate.hpp", cases)
