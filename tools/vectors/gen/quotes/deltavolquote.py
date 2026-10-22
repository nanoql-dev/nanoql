# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DeltaVolQuote (ql/quotes/deltavolquote.hpp, .cpp): a volatility quoted at a delta or at the
money, and its inspectors.

Quotes at deltas of ±0.1, ±0.25 and 0.5 under each delta convention, and at the money under each ATM convention, at
volatilities of 8 and 15% and maturities of a month and two years: each quote's value, maturity, delta type and ATM
type, and a delta quote's delta. An ATM quote's delta, which QuantLib leaves uninitialized, is left out (the S-rx plan's
SRXR3 (viii); tests/NanoQL.Tests/Divergences/DeltaVolQuoteAtmDeltaTests.cs).

A case's inputs are the "quote": {"kind": "delta", "delta", "vol", "maturity", "deltaType"} or {"kind": "atm", "vol",
"deltaType", "maturity", "atmType"}, the types QuantLib's integers (DeltaType Spot 0, Fwd 1, PaSpot 2, PaFwd 3;
AtmType AtmNull 0 to AtmPutCall50 6). The wheel's calls, by QuantLib-SWIG 1.44's: DeltaVolQuote's two constructors,
each volatility a SimpleQuote, and value(), delta(), maturity(), deltaType() and atmType(). Read back by
tests/NanoQL.Tests/Vectors/Quotes/DeltaVolQuoteVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

DELTA_TYPES = [("spot", 0), ("forward", 1), ("premium-adjusted spot", 2), ("premium-adjusted forward", 3)]
ATM_TYPES = [("AtmNull", 0), ("AtmSpot", 1), ("AtmFwd", 2), ("AtmDeltaNeutral", 3), ("AtmVegaMax", 4),
             ("AtmGammaMax", 5), ("AtmPutCall50", 6)]


def inspectors(q, with_delta):
    out = {"value": q.value(), "maturity": q.maturity(), "deltaType": int(q.deltaType()), "atmType": int(q.atmType())}
    if with_delta:
        out["delta"] = q.delta()
    return out


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for vol in (0.08, 0.15):
        for maturity in (1.0 / 12.0, 2.0):
            for dname, dt in DELTA_TYPES:
                for delta in (-0.1, -0.25, 0.1, 0.25, 0.5):
                    q = ql.DeltaVolQuote(delta, ql.QuoteHandle(ql.SimpleQuote(vol)), maturity, dt)
                    out.append({"name": f"{dname} delta {delta:g}, vol {vol:g}, maturity {maturity:g}",
                                "inputs": {"settings": st, "quote": {"kind": "delta", "delta": delta, "vol": vol,
                                                                     "maturity": maturity, "deltaType": dt}},
                                "expected": inspectors(q, True)})
                for aname, at in ATM_TYPES:
                    q = ql.DeltaVolQuote(ql.QuoteHandle(ql.SimpleQuote(vol)), dt, maturity, at)
                    out.append({"name": f"{dname} {aname}, vol {vol:g}, maturity {maturity:g}",
                                "inputs": {"settings": st, "quote": {"kind": "atm", "vol": vol, "deltaType": dt,
                                                                     "maturity": maturity, "atmType": at}},
                                "expected": inspectors(q, False)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/quotes/deltavolquote.hpp", cases)
