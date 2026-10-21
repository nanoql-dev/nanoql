# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ExchangeRateManager (ql/currencies/exchangeratemanager.hpp, .cpp), nanoql's ExchangeRates:
direct, inverse, triangulated and chained lookups, dated entries and the known rates.

Registries: QuantLib's known rates alone (the euro's legacy currencies, triangulated through EUR, and the
redenominated TRL, ROL, PEI and PEH, PEN to PEH a chain); the three registries of QuantLib's exchangerate tests (direct
rates on two dates, EUR/USD under ITL's triangulation, and the five-currency set its chains of two to five rates run
through); overlapping dated entries added in both orders, the latest added taking precedence; legacy currencies chained
with added rates; two chains to one target, where QuantLib takes the first by its pairs' order; and chains through BGL
and BGN, two currencies QuantLib names alike ("Bulgarian lev"), whose pairs and chains QuantLib tells apart by their
numeric codes. Lookups between
those currencies, Direct and Derived, on dates inside, outside and at the edges of the entries' ranges, and with no date
(the evaluation date). Each lookup's source, target, rate and type, and its exchange of 100 units of its source, or
{"throws": true} for each where QuantLib refuses it.

Left out (the S-rx plan's §2.5): currencies coded 1,000 or more and currencies sharing a numeric code (CNY and CNH),
whose lookups nanoql fixes (SRXR3 (iv), (v); Divergences/ExchangeRateKeyTests.cs, SharedNumericCodeTests.cs). No
other ported currency shares a code with another. Also left out: a lookup between BGL and BGN, which QuantLib's name
equality answers as one currency's, at 1.0, where nanoql tells them apart (Divergences/CurrencyNameAndCodeTests.cs).

A case holds its settings' exchangeRates (the rates added after clear()) and "lookups", a list of {"source", "target"
(codes), "date" (a serial, or null for the evaluation date), "type" (QuantLib's integer)}; the outputs are arrays, one
element per lookup: "sources", "targets", "rates", "types", "exchanged" (the value of 100 units of the source,
exchanged). The wheel's calls, by QuantLib-SWIG 1.44's exchangerates.i: ExchangeRateManager.instance().clear(), add(rate,
startDate, endDate) and lookup(source, target, date, type), a null date passed as Date(). Read back by
tests/NanoQL.Tests/Vectors/Currencies/ExchangeRateManagerVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

CURRENCIES = {c: getattr(ql, c + "Currency") for c in
              ["EUR", "USD", "GBP", "JPY", "CHF", "SEK", "ITL", "DEM", "ATS", "FRF", "GRD", "TRY", "TRL", "RON", "ROL",
               "PEN", "PEI", "PEH", "CAD", "BGL", "BGN"]}
D, X = ql.ExchangeRate.Direct, ql.ExchangeRate.Derived


def rate(s, t, r):
    return ql.ExchangeRate(CURRENCIES[s](), CURRENCIES[t](), r)


def lookups_case(name, evaluation_date, rates, lookups):
    with v.case_settings(evaluation_date, exchange_rates=rates) as s:
        manager = ql.ExchangeRateManager.instance()
        out = {"sources": [], "targets": [], "rates": [], "types": [], "exchanged": []}
        for src, tgt, date, kind in lookups:
            try:
                r = manager.lookup(CURRENCIES[src](), CURRENCIES[tgt](), date if date is not None else ql.Date(), kind)
                exchanged = r.exchange(ql.Money(100.0, CURRENCIES[src]()))
                values = [r.source().code(), r.target().code(), v.enc(r.rate()), int(r.type()),
                          v.enc(exchanged.value())]
            except RuntimeError:
                values = [dict(v.THROWS)] * 5
            for key, value in zip(out, values):
                out[key].append(value)
        return {"name": name,
                "inputs": {"lookups": [{"source": a, "target": b, "date": v.enc(d) if d is not None else None,
                                        "type": int(k)} for a, b, d, k in lookups],
                           "settings": s},
                "expected": out}


def cases():
    out = []
    y1998, y2000, y2002, y2006 = (ql.Date(1, 6, y) for y in (1998, 2000, 2002, 2006))
    known = [("ATS", "DEM", y2000, X), ("ATS", "DEM", y1998, X), ("ATS", "DEM", y2000, D), ("EUR", "ITL", y2000, X),
             ("ITL", "EUR", y2000, X), ("ITL", "EUR", y2000, D), ("DEM", "EUR", y2000, D), ("FRF", "ITL", y2002, X),
             ("GRD", "DEM", y2000, X), ("GRD", "DEM", y2002, X), ("TRY", "TRL", y2006, X), ("TRL", "TRY", y2006, X),
             ("TRY", "TRL", y2002, X), ("RON", "ROL", y2006, D), ("PEN", "PEH", ql.Date(1, 1, 1992), X),
             ("PEH", "PEN", ql.Date(1, 1, 1992), X), ("PEN", "PEH", ql.Date(1, 1, 1990), X),
             ("PEI", "PEH", ql.Date(1, 1, 1990), X), ("EUR", "USD", y2000, X), ("USD", "USD", y2000, X),
             ("USD", "USD", y2000, D), ("ITL", "ITL", y1998, D), ("ATS", "EUR", ql.Date(31, 12, 1998), X),
             ("ATS", "EUR", ql.Date(1, 1, 1999), X), ("USD", "ITL", y2000, X)]
    out.append(lookups_case("known rates", ql.Date(15, 10, 2026), [], known))

    aug4, aug5, aug3 = ql.Date(4, 8, 2004), ql.Date(5, 8, 2004), ql.Date(3, 8, 2004)
    direct = [(rate("EUR", "USD", 1.1983), aug4, None), (rate("USD", "EUR", 1.0 / 1.2042), aug5, None)]
    out.append(lookups_case("QuantLib's direct lookup", ql.Date(16, 9, 2015), direct,
                            [("EUR", "USD", aug4, D), ("EUR", "USD", aug5, D), ("USD", "EUR", aug4, D),
                             ("USD", "EUR", aug5, D), ("EUR", "USD", aug3, D), ("EUR", "USD", aug3, X),
                             ("EUR", "USD", None, X), ("EUR", "GBP", aug4, D), ("EUR", "GBP", aug4, X)]))

    triangulated = [(rate("EUR", "USD", 1.1983), aug4, None), (rate("EUR", "USD", 1.2042), aug5, None)]
    out.append(lookups_case("QuantLib's triangulated lookup", ql.Date(16, 9, 2015), triangulated,
                            [("ITL", "USD", aug4, X), ("ITL", "USD", aug5, X), ("USD", "ITL", aug4, X),
                             ("USD", "ITL", aug5, X), ("ITL", "USD", aug4, D), ("DEM", "USD", aug5, X),
                             ("USD", "DEM", aug3, X), ("ITL", "DEM", aug4, X), ("USD", "ITL", None, X)]))

    smart = [(rate("EUR", "USD", 1.1983), aug4, None), (rate("USD", "EUR", 1.0 / 1.2042), aug5, None),
             (rate("GBP", "EUR", 1.0 / 0.6596), aug4, None), (rate("EUR", "GBP", 0.6612), aug5, None),
             (rate("USD", "CHF", 1.2847), aug4, None), (rate("CHF", "USD", 1.0 / 1.2774), aug5, None),
             (rate("SEK", "CHF", 0.1674), aug4, None), (rate("CHF", "SEK", 1.0 / 0.1677), aug5, None),
             (rate("SEK", "JPY", 14.5450), aug4, None), (rate("JPY", "SEK", 1.0 / 14.6110), aug5, None)]
    pairs = [("USD", "SEK"), ("SEK", "USD"), ("EUR", "SEK"), ("SEK", "EUR"), ("EUR", "JPY"), ("JPY", "EUR"),
             ("GBP", "JPY"), ("JPY", "GBP"), ("GBP", "CHF"), ("CHF", "GBP"), ("JPY", "CHF"), ("ITL", "JPY"),
             ("JPY", "ITL"), ("JPY", "CAD"), ("CAD", "JPY")]
    out.append(lookups_case("QuantLib's smart lookup", ql.Date(16, 9, 2015), smart,
                            [(a, b, d, X) for a, b in pairs for d in (aug4, aug5)]
                            + [("USD", "SEK", aug3, X), ("USD", "SEK", aug4, D), ("GBP", "JPY", None, X)]))

    wide, narrow = ((rate("EUR", "USD", 1.10), ql.Date(1, 1, 2026), ql.Date(31, 12, 2026)),
                    (rate("EUR", "USD", 1.15), ql.Date(1, 6, 2026), ql.Date(30, 6, 2026)))
    dates = [ql.Date(31, 12, 2025), ql.Date(1, 1, 2026), ql.Date(31, 5, 2026), ql.Date(1, 6, 2026),
             ql.Date(15, 6, 2026), ql.Date(30, 6, 2026), ql.Date(1, 7, 2026), ql.Date(31, 12, 2026),
             ql.Date(1, 1, 2027)]
    for label, order in (("wide then narrow", [wide, narrow]), ("narrow then wide", [narrow, wide])):
        out.append(lookups_case(f"overlapping entries, {label}", ql.Date(15, 6, 2026), order,
                                [(a, b, d, k) for d in dates for a, b in (("EUR", "USD"), ("USD", "EUR"))
                                 for k in (D,)] + [("EUR", "USD", None, X), ("USD", "DEM", ql.Date(15, 6, 2026), X)]))

    mixed = [(rate("USD", "JPY", 151.37), None, None), (rate("EUR", "USD", 1.0856), None, None),
             (rate("GBP", "CHF", 1.1427), None, None), (rate("CHF", "EUR", 1.0412), None, None)]
    today = ql.Date(15, 10, 2026)
    out.append(lookups_case("legacy currencies chained", today, mixed,
                            [("JPY", "ITL", None, X), ("ITL", "JPY", None, X), ("DEM", "GBP", None, X),
                             ("GBP", "FRF", None, X), ("ATS", "JPY", today, X), ("JPY", "GBP", today, X),
                             ("GBP", "JPY", today, X), ("ITL", "GBP", ql.Date(1, 1, 1998), X)]))

    competing = [(rate("USD", "CHF", 0.80), None, None), (rate("CHF", "SEK", 11.5), None, None),
                 (rate("USD", "EUR", 0.92), None, None), (rate("EUR", "SEK", 11.6), None, None),
                 (rate("USD", "GBP", 0.75), None, None), (rate("GBP", "SEK", 13.4), None, None)]
    out.append(lookups_case("competing chains", today, competing,
                            [(a, b, None, X) for a, b in (("USD", "SEK"), ("SEK", "USD"), ("CHF", "GBP"),
                                                          ("EUR", "GBP"), ("GBP", "CHF"), ("SEK", "ITL"))]))

    lev = [(rate("BGL", "USD", 0.0005), None, None), (rate("USD", "BGN", 1.8), None, None),
           (rate("BGN", "JPY", 85.0), None, None)]
    out.append(lookups_case("currencies sharing a name", today, lev,
                            [(a, b, None, X) for a, b in (("BGL", "JPY"), ("JPY", "BGL"), ("USD", "JPY"),
                                                          ("JPY", "USD"))]))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/currencies/exchangeratemanager.hpp", cases)
