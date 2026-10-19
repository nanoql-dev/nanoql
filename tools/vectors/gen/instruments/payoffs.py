# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the payoffs of ql/instruments/payoffs.hpp the wheel builds as QuantLib declares them:
PlainVanillaPayoff, PercentageStrikePayoff, AssetOrNothingPayoff, CashOrNothingPayoff, GapPayoff and
FloatingTypePayoff. The wheel's SuperSharePayoff takes an option type where QuantLib's constructor takes the strike,
so it is vectored through the C++ harness (tools/vectors/gen/cxx/instruments/payoffs.py), with NullPayoff and
SuperFundPayoff, which the wheel doesn't wrap, and the members it doesn't expose (name, description, cashPayoff,
secondStrike).

- "striked" cases: each striked payoff, for each option type and 5 strikes (moneyness for PercentageStrikePayoff), its
  option type, its strike, and its value at 20 prices: zero and below, on and around the strikes, far above them, and
  +inf and NaN, which std::max keeps. CashOrNothingPayoff pays 10, and GapPayoff's second strike is 90.
- "floating" cases: FloatingTypePayoff for each option type, its option type, its value at the 20 prices for each of
  the 5 strikes, and the one-argument operator(), which always refuses ("floating payoff not handled").

Each case's input "payoff" is the spec tools/cxx/src/instruments/payoffspec.hpp reads. A refused value is
{"throws": true}. Read back by tests/NanoQL.Tests/Vectors/Instruments/PayoffsVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

INF = math.inf
NAN = math.nan
TYPES = [("Call", ql.Option.Call), ("Put", ql.Option.Put)]
STRIKES = [0.0, 1e-7, 50.0, 100.0, 110.0]
MONEYNESS = [0.0, 0.8, 1.0, 1.25, 2.0]
PRICES = [0.0, 1e-8, 0.5, 1.0, 25.0, 49.999999, 50.0, 50.000001, 75.0, 90.0, 99.99999999, 100.0, 100.00000001,
          105.0, 110.0, 200.0, 1e6, -1.0, INF, NAN]
CASH = 10.0
SECOND_STRIKE = 90.0


def striked(tn, t, k):
    """(name, spec, payoff) for each striked payoff of the option type t and strike (or moneyness) k."""
    return [
        ("PlainVanillaPayoff", {"type": "PlainVanillaPayoff", "optionType": int(t), "strike": k},
         ql.PlainVanillaPayoff(t, k)),
        ("AssetOrNothingPayoff", {"type": "AssetOrNothingPayoff", "optionType": int(t), "strike": k},
         ql.AssetOrNothingPayoff(t, k)),
        ("CashOrNothingPayoff", {"type": "CashOrNothingPayoff", "optionType": int(t), "strike": k, "cashPayoff": CASH},
         ql.CashOrNothingPayoff(t, k, CASH)),
        ("GapPayoff", {"type": "GapPayoff", "optionType": int(t), "strike": k, "secondStrike": SECOND_STRIKE},
         ql.GapPayoff(t, k, SECOND_STRIKE))]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for tn, t in TYPES:
        payoffs = [p for k in STRIKES for p in striked(tn, t, k)]
        payoffs += [("PercentageStrikePayoff", {"type": "PercentageStrikePayoff", "optionType": int(t), "moneyness": m},
                     ql.PercentageStrikePayoff(t, m)) for m in MONEYNESS]
        for name, spec, payoff in payoffs:
            strike = spec.get("strike", spec.get("moneyness"))
            out.append({"name": f"{name} {tn} {strike!r}",
                        "inputs": {"settings": st, "kind": "striked", "payoff": spec, "prices": v.enc(PRICES)},
                        "expected": {"optionType": int(payoff.optionType()), "strike": v.enc(payoff.strike()),
                                     "evaluate": [v.attempt(lambda p=p: payoff(p)) for p in PRICES]}})
        floating = ql.FloatingTypePayoff(t)
        out.append({"name": f"FloatingTypePayoff {tn}",
                    "inputs": {"settings": st, "kind": "floating", "payoff": {"type": "FloatingTypePayoff",
                                                                             "optionType": int(t)},
                               "prices": v.enc(PRICES), "strikes": STRIKES},
                    "expected": {"optionType": int(floating.optionType()),
                                 "evaluate": [[v.attempt(lambda p=p, k=k: floating(p, k)) for p in PRICES]
                                              for k in STRIKES],
                                 "evaluatePrice": [v.attempt(lambda p=p: floating(p)) for p in PRICES[:3]]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/payoffs.hpp", cases)
