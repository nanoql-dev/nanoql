# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ExchangeRate (ql/exchangerate.hpp, .cpp): direct rates and their chains, and exchanges.

Direct rates between three pairs; chains of two rates in each of chain()'s four configurations (the sources shared, the
first's source the second's target, the first's target the second's source, the targets shared), each pair of rates in
both orders; a chain of a chain; and two rates sharing no currency, which chain() refuses. For each rate, its source,
target, value and type, and the exchange of an amount in each currency the rate or its chain involves and in one it
doesn't. Chains of two rates over one pair are left out: QuantLib returns a rate from a currency to itself, which
nanoql refuses (Divergences/ExchangeRateChainRefusalTests.cs).

A case holds "rates", a list of direct rates {"source", "target" (codes), "rate"}, and "chain", how they combine: null
for the one direct rate, [0, 1] for chain(rates[0], rates[1]), [[0, 1], 2] for chain(chain(...), rates[2]); and
"amounts", the amounts exchanged, {"currency", "value"}. The outputs are "source", "target" and "type" (QuantLib's
integer), "rate", and per amount "exchange" currencies and values (a code, or {"throws": true}). The wheel's calls, by
QuantLib-SWIG 1.44's exchangerates.i: ExchangeRate(source, target, rate), ExchangeRate.chain, source(), target(),
rate(), type() and exchange(Money). Read back by tests/NanoQL.Tests/Vectors/ExchangeRateVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
CURRENCIES = {"EUR": ql.EURCurrency, "USD": ql.USDCurrency, "GBP": ql.GBPCurrency, "JPY": ql.JPYCurrency,
              "CHF": ql.CHFCurrency, "SEK": ql.SEKCurrency}


def build(spec, rates):
    if spec is None:
        return rates[0]
    first, second = spec
    left = rates[first] if isinstance(first, int) else build(first, rates)
    return ql.ExchangeRate.chain(left, rates[second])


def case(name, rate_specs, chain, amount_codes):
    rates = [ql.ExchangeRate(CURRENCIES[s](), CURRENCIES[t](), r) for s, t, r in rate_specs]
    amounts = [(c, 1234.5) for c in amount_codes]
    with v.case_settings(R) as s:
        try:
            rate = build(chain, rates)
            expected = {"source": rate.source().code(), "target": rate.target().code(), "rate": rate.rate(),
                        "type": int(rate.type())}
        except RuntimeError:
            rate = None
            expected = {k: dict(v.THROWS) for k in ("source", "target", "rate", "type")}
        currencies, values = [], []
        for c, x in amounts:
            if rate is None:
                currencies.append(dict(v.THROWS))
                values.append(dict(v.THROWS))
                continue
            try:
                m = rate.exchange(ql.Money(x, CURRENCIES[c]()))
                currencies.append(m.currency().code())
                values.append(v.enc(m.value()))
            except RuntimeError:
                currencies.append(dict(v.THROWS))
                values.append(dict(v.THROWS))
        expected["exchangeCurrencies"] = currencies
        expected["exchangeValues"] = values
        return {"name": name,
                "inputs": {"rates": [{"source": s_, "target": t_, "rate": r_} for s_, t_, r_ in rate_specs],
                           "chain": chain, "amounts": [{"currency": c, "value": x} for c, x in amounts],
                           "settings": s},
                "expected": expected}


def cases():
    out = []
    for s_, t_, r_ in [("EUR", "USD", 1.2042), ("USD", "JPY", 151.37), ("GBP", "CHF", 1.1427)]:
        others = [c for c in ("EUR", "USD", "GBP", "JPY") if c not in (s_, t_)][:1]
        out.append(case(f"direct {s_}{t_}", [(s_, t_, r_)], None, [s_, t_] + others))
    eur_usd, eur_gbp = ("EUR", "USD", 1.2042), ("EUR", "GBP", 0.6612)
    usd_eur, gbp_eur = ("USD", "EUR", 1.0 / 1.2042), ("GBP", "EUR", 1.0 / 0.6612)
    usd_chf, chf_sek = ("USD", "CHF", 1.2847), ("CHF", "SEK", 1.0 / 0.1677)
    four = ["EUR", "USD", "GBP", "JPY"]
    configurations = [
        ("sources shared", eur_usd, eur_gbp),
        ("first's source the second's target", eur_usd, gbp_eur),
        ("first's target the second's source", usd_eur, eur_gbp),
        ("targets shared", usd_eur, gbp_eur),
    ]
    for label, a, b in configurations:
        out.append(case(f"chain, {label}", [a, b], [0, 1], four))
        out.append(case(f"chain reversed, {label}", [b, a], [0, 1], four))
    out.append(case("chain of a chain", [eur_usd, usd_chf, chf_sek], [[0, 1], 2], ["EUR", "USD", "CHF", "SEK", "GBP"]))
    out.append(case("chain of a chain, reversed", [chf_sek, usd_chf, usd_eur], [[0, 1], 2],
                    ["EUR", "USD", "CHF", "SEK", "GBP"]))
    out.append(case("not chainable", [eur_usd, ("GBP", "JPY", 191.3)], [0, 1], four))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/exchangerate.hpp", cases)
