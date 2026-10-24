# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of ql/payoff.hpp and ql/instruments/payoffs.hpp the wheel doesn't expose, each output
computed by the C++ harness: every payoff's name() and description(), whose numbers a C++ stream writes in its
default format (six significant digits, %g style); CashOrNothingPayoff::cashPayoff and GapPayoff::secondStrike; and
every member of NullPayoff and SuperFundPayoff, which the wheel doesn't wrap, and of SuperSharePayoff, whose wrapped
constructor takes an option type where QuantLib's takes the strike; and ql/instruments/forward.hpp's
ForwardTypePayoff, which the wheel doesn't wrap.

One case per payoff type, its input "payoffs" a list of specs as tools/cxx/src/instruments/payoffspec.hpp reads them,
and each output a list with one value per payoff:
- the striked payoffs (plain vanilla, percentage strike, asset or nothing, cash or nothing, gap) of both option
  types over strikes that a stream writes in each of its forms: integers, fractions to six digits, exponents below
  1e-4 and from 1e6, zero of both signs, ±inf and NaN; their name and description, and the cash payoff or second
  strike where the payoff has one;
- the floating payoff of both types: name and description;
- NullPayoff: name, description, and its value at three prices, always refused ("dummy payoff given");
- SuperFundPayoff and SuperSharePayoff over strike pairs, among them the ones their constructors refuse (SuperFund's
  non-positive strike; a second strike not above the first); name, description, option type, strike, second strike,
  SuperShare's cash payoff, and the value at 10 prices on and around the strikes. A payoff its constructor refuses
  gives {"throws": true} for each of its outputs;
- ForwardTypePayoff, long and short, over the short strikes and a negative one, which its constructor refuses:
  name, description and the value at the 10 prices.

tests/NanoQL.Tests/Vectors/Cxx/Instruments/PayoffsCxxVectorTests.cs reads it back."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

INF = math.inf
NAN = math.nan
CALL, PUT = 1, -1
LONG, SHORT = 0, 1
# The strikes a stream writes in each of its forms.
TEXT_STRIKES = [100.0, 1e-7, 1234567.0, 1 / 3, 0.0, -0.0, -2.5, 1e21, 123456.0, 0.0001, 1e-5, 99.99999, INF, -INF,
                NAN]
SHORT_STRIKES = [100.0, 1e-7, 1 / 3, 1234567.0, -0.0]
CASH = [10.0, 2.5e-6, 1e6, 1 / 7, 0.0]
SECOND = [90.0, 110.5, 1e-9, 7654321.0, -3.0]
SUPER_FUND = [(100.0, 110.0), (1e-7, 1.0), (1 / 3, 2 / 3), (50.0, 1234567.0), (0.0, 1.0), (-1.0, 1.0),
              (100.0, 100.0), (100.0, 90.0), (1.0, INF)]
SUPER_SHARE = [(100.0, 110.0, 10.0), (1e-7, 1.0, 2.5e-6), (1 / 3, 2 / 3, 1e6), (0.0, 1.0, 1.0), (-1.0, 1.0, 5.0),
               (-INF, 0.0, 1 / 7), (100.0, 100.0, 10.0), (100.0, 90.0, 10.0), (1.0, INF, 3.0)]
PRICES = [0.0, 1e-7, 0.5, 1.0, 50.0, 99.9999999, 100.0, 105.0, 110.0, 1e7]
NULL_PRICES = [0.0, 100.0, NAN]


def striked_specs():
    out = {"PlainVanillaPayoff": [], "PercentageStrikePayoff": [], "AssetOrNothingPayoff": [],
           "CashOrNothingPayoff": [], "GapPayoff": []}
    for t in (CALL, PUT):
        for k in TEXT_STRIKES:
            out["PlainVanillaPayoff"].append({"type": "PlainVanillaPayoff", "optionType": t, "strike": k})
        for k in SHORT_STRIKES:
            out["PercentageStrikePayoff"].append({"type": "PercentageStrikePayoff", "optionType": t, "moneyness": k})
            out["AssetOrNothingPayoff"].append({"type": "AssetOrNothingPayoff", "optionType": t, "strike": k})
        for k, cash in zip(SHORT_STRIKES, CASH):
            out["CashOrNothingPayoff"].append({"type": "CashOrNothingPayoff", "optionType": t, "strike": k,
                                               "cashPayoff": cash})
        for k, second in zip(SHORT_STRIKES, SECOND):
            out["GapPayoff"].append({"type": "GapPayoff", "optionType": t, "strike": k, "secondStrike": second})
    return out


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def each(method, specs, *args):
            return [v.attempt(lambda s=s: H.call(method, s, *args)) for s in specs]

        def case(name, specs, expected):
            return {"name": name, "inputs": {"settings": st, "payoffs": v.enc(specs)}, "expected": expected}

        out = []
        for name, specs in striked_specs().items():
            expected = {"name": each("Payoff::name", specs), "description": each("Payoff::description", specs)}
            if name == "CashOrNothingPayoff":
                expected["cashPayoff"] = each("CashOrNothingPayoff::cashPayoff", specs)
            if name == "GapPayoff":
                expected["secondStrike"] = each("GapPayoff::secondStrike", specs)
            out.append(case(name, specs, expected))
        floating = [{"type": "FloatingTypePayoff", "optionType": t} for t in (CALL, PUT)]
        out.append(case("FloatingTypePayoff", floating, {"name": each("Payoff::name", floating),
                                                         "description": each("Payoff::description", floating)}))
        null = [{"type": "NullPayoff"}]
        out.append({"name": "NullPayoff", "inputs": {"settings": st, "payoffs": null, "prices": v.enc(NULL_PRICES)},
                    "expected": {"name": each("Payoff::name", null), "description": each("Payoff::description", null),
                                 "evaluate": [[v.attempt(lambda s=s, p=p: H.call("Payoff::operator()", s, p))
                                               for p in NULL_PRICES] for s in null]}})
        for name, specs in (
                ("SuperFundPayoff", [{"type": "SuperFundPayoff", "strike": k, "secondStrike": k2}
                                     for k, k2 in SUPER_FUND]),
                ("SuperSharePayoff", [{"type": "SuperSharePayoff", "strike": k, "secondStrike": k2, "cashPayoff": c}
                                      for k, k2, c in SUPER_SHARE])):
            expected = {
                "name": each("Payoff::name", specs), "description": each("Payoff::description", specs),
                "optionType": each("TypePayoff::optionType", specs),
                "strike": each("StrikedTypePayoff::strike", specs),
                "secondStrike": each(name + "::secondStrike", specs)}
            if name == "SuperSharePayoff":
                expected["cashPayoff"] = each("SuperSharePayoff::cashPayoff", specs)
            expected["evaluate"] = [[v.attempt(lambda s=s, p=p: H.call("Payoff::operator()", s, p)) for p in PRICES]
                                    for s in specs]
            out.append({"name": name, "inputs": {"settings": st, "payoffs": v.enc(specs), "prices": PRICES},
                        "expected": expected})
        forward = [{"type": "ForwardTypePayoff", "position": position, "strike": k}
                   for position in (LONG, SHORT) for k in SHORT_STRIKES + [-2.5]]
        out.append({"name": "ForwardTypePayoff", "inputs": {"settings": st, "payoffs": v.enc(forward),
                                                        "prices": PRICES},
                    "expected": {"name": each("Payoff::name", forward),
                                 "description": each("Payoff::description", forward),
                                 "evaluate": [[v.attempt(lambda s=s, p=p: H.call("Payoff::operator()", s, p))
                                               for p in PRICES] for s in forward]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/payoffs.hpp", cases)
