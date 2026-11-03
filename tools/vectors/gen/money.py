# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Money (ql/money.hpp, .cpp): its inspectors, rounding and text, and its arithmetic and
comparisons under each of Money::Settings' conversion types.

Single amounts in currencies of 0, 2 and 3 decimals and QuantLib's empty currency, at values that round up, down, to a
tie and to negative zero, at large and tiny magnitudes, and NaN and the infinities: the currency's code, the value, the
rounded value, the text (operator<<), the negation, and the amount times, by and divided by a number. Pairs of amounts,
in one currency and in two, under NoConversion, BaseCurrencyConversion (base EUR, GBP or none) and AutomatedConversion,
with EUR/USD and EUR/GBP rates in the registry (and a pair with no rate between them): the sum, the difference, the
ratio, ==, !=, <, <=, > and >=; close and close_enough, which the wheel doesn't bind for Money, are QuantLib's
testComparisons'. QuantLib's == and != throw on a currency mismatch under NoConversion, where nanoql's operators return
false and true (MoneyVectorTests maps the throws).

A single case holds {"money": {"currency" (a code, or null for the empty currency), "value"}, "factor"}; a pair case
holds "m1" and "m2" and its settings' exchangeRates and moneySettings. A money output is two outputs, "<name>Currency"
(a code, or null) and "<name>Value", both {"throws": true} where QuantLib throws. The wheel's calls, by QuantLib-SWIG
1.44's money.i: Money(value, currency), currency(), value(), rounded(), str(), and the operators, which call QuantLib's
but for <= and >=: the shim's are !(other < self) and !(self < other), where QuantLib's operator<= is apply's x <= y
(money.cpp:108-112) and operator>= is m2 <= m1 (money.hpp:240-242), which differ under a conversion that rounds. So
"le" is (m1 < m2) or (m1 == m2), and "ge" (m2 < m1) or (m2 == m1), each pair converting the same operand as
QuantLib's operator, equal to it for the cases' values, none a NaN. Read back by
tests/NanoQL.Tests/Vectors/MoneyVectorTests.cs."""
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
              "BHD": ql.BHDCurrency, "CHF": ql.CHFCurrency}


def currency(code):
    return ql.Currency() if code is None else CURRENCIES[code]()


def code(c):
    return None if c.empty() else c.code()


def money_spec(m):
    return {"currency": code(m.currency()), "value": v.enc(m.value())}


def money_out(name, f):
    try:
        m = f()
    except RuntimeError:
        return {name + "Currency": dict(v.THROWS), name + "Value": dict(v.THROWS)}
    return {name + "Currency": code(m.currency()), name + "Value": v.enc(m.value())}


def single_cases():
    out = []
    values = [1234567.891, 0.123456789, 100.0, 1e-7, -0.004, -1234.5678, 0.125, 2.675, 0.5, 1.5, 1e20, -2.5e-9,
              999999.5, 0.0, float("nan"), float("inf"), float("-inf")]
    for c in ["EUR", "JPY", "BHD", None]:
        for x in values:
            m = ql.Money(x, currency(c))
            factor = 3.7
            with v.case_settings(R) as s:
                expected = {"currency": code(m.currency()), "value": v.enc(m.value()),
                            "rounded": v.attempt(lambda: m.rounded().value()),
                            "text": v.attempt(lambda: str(m))}
                expected.update(money_out("negated", lambda: -m))
                expected.update(money_out("timesFactor", lambda: m * factor))
                expected.update(money_out("factorTimes", lambda: factor * m))
                expected.update(money_out("dividedBy", lambda: m / factor))
                out.append({"name": f"{c or 'empty'} {x!r}",
                            "inputs": {"money": money_spec(m), "factor": factor, "settings": s},
                            "expected": expected})
    return out


def pair_expected(m1, m2):
    expected = {}
    expected.update(money_out("sum", lambda: m1 + m2))
    expected.update(money_out("difference", lambda: m1 - m2))
    expected["ratio"] = v.attempt(lambda: m1 / m2)
    expected["eq"] = v.attempt(lambda: m1 == m2)
    expected["ne"] = v.attempt(lambda: m1 != m2)
    expected["lt"] = v.attempt(lambda: m1 < m2)
    expected["le"] = v.attempt(lambda: (m1 < m2) or (m1 == m2))
    expected["gt"] = v.attempt(lambda: m1 > m2)
    expected["ge"] = v.attempt(lambda: (m2 < m1) or (m2 == m1))
    return expected


def pair_cases():
    out = []
    eur, usd, gbp = ql.EURCurrency(), ql.USDCurrency(), ql.GBPCurrency()
    rates = [(ql.ExchangeRate(eur, usd, 1.2042), None, None), (ql.ExchangeRate(eur, gbp, 0.6612), None, None)]
    pairs = [(("EUR", 123.45), ("EUR", 123.45)), (("EUR", 1.0), ("EUR", 2.0)), (("EUR", 2.0), ("EUR", 1.0)),
             (("EUR", 1.0), ("USD", 1.2042)), (("EUR", 1.0), ("USD", 100.0)), (("EUR", 100.0), ("USD", 1.0)),
             (("GBP", 50000.0), ("USD", 500000.0)), (("USD", 500000.0), ("GBP", 50000.0)),
             (("USD", 123.456), ("EUR", 0.0)), (("GBP", 1.0 + 1e-15), ("EUR", 1.0 / 0.6612)),
             (("EUR", 1.0 + 1e-15), ("USD", 1.2042)), (("GBP", 2.5), ("GBP", -7.25)),
             (("EUR", 10.0), ("JPY", 1500.0)), (("JPY", 1500.0), ("JPY", 1500.0)), ((None, 3.0), (None, 4.5)),
             ((None, 3.0), ("EUR", 3.0))]
    settings = [("none", ql.Money.NoConversion, None), ("base EUR", ql.Money.BaseCurrencyConversion, "EUR"),
                ("base GBP", ql.Money.BaseCurrencyConversion, "GBP"),
                ("base unset", ql.Money.BaseCurrencyConversion, None),
                ("automated", ql.Money.AutomatedConversion, None)]
    for label, conversion, base in settings:
        for (c1, x1), (c2, x2) in pairs:
            m1, m2 = ql.Money(x1, currency(c1)), ql.Money(x2, currency(c2))
            with v.case_settings(R, exchange_rates=rates,
                                 money_settings=(conversion, None if base is None else currency(base))) as s:
                out.append({"name": f"{label}: {c1 or 'empty'} {x1!r}, {c2 or 'empty'} {x2!r}",
                            "inputs": {"m1": money_spec(m1), "m2": money_spec(m2), "settings": s},
                            "expected": pair_expected(m1, m2)})
    return out


def cases():
    return single_cases() + pair_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/money.hpp", cases)
