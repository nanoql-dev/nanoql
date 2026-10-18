# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Currency (ql/currency.hpp): 20 bespoke currencies built with the eight-argument constructor,
among them currencies with and without a triangulation currency, with empty symbols, with non-ASCII symbols, and
two with USD's name, one with USD's code and one with another, which compare equal. Each currency gives its name,
code, numeric code, symbol, fraction symbol and fractions per unit; its rounding applied to five values; its
triangulation currency's emptiness and code (null when it is empty); its own emptiness; and its == and != against each
of the 20 currencies, in file order, and against the empty currency.

Each case's inputs are "currency", the constructor's arguments by the accessors' names: the rounding is null for
Rounding() or {"precision", "type", "digit"}, the type as Rounding::Type's integer, and the triangulation currency is
the index of an earlier case's currency, or null for none. The minor unit codes, which the wheel's constructor doesn't
take, are tools/vectors/gen/cxx/currency.py's. Read back by tests/NanoQL.Tests/Vectors/CurrencyVectorTests.cs.

The wheel's Currency is QuantLib-SWIG's proxy of C++ QuantLib::Currency (QuantLib-SWIG 1.44, SWIG/currencies.i): its
constructor is QuantLib's without the last argument, minorUnitCodes; the accessors and empty() are the C++ members;
__eq__ and __ne__, %extend methods, return QuantLib's operator== and operator!=. Its __str__ gives the name, not
operator<<'s code, so the text is the harness's. A rounding is built with the wheel's Rounding(), the proxy of C++
QuantLib::Rounding's default constructor, or with the class of its type (SWIG/rounding.i: UpRounding, DownRounding,
ClosestRounding, FloorTruncation, CeilingTruncation, each the C++ class of that name, whose constructor passes its
type on); the wheel's Rounding.__call__ is operator()."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

# Rounding::Type's integers (ql/math/rounding.hpp:48-69), which the wheel doesn't expose, and the class of each.
KINDS = {1: ql.UpRounding, 2: ql.DownRounding, 3: ql.ClosestRounding, 4: ql.FloorTruncation, 5: ql.CeilingTruncation}


def rounding_spec(kind, precision, digit=5):
    return {"precision": precision, "type": kind, "digit": digit}


# name, code, numeric code, symbol, fraction symbol, fractions per unit, rounding, triangulation (an earlier index)
CURRENCIES = [
    ("U.S. dollar", "USD", 840, "$", "¢", 100, None, None),
    ("U.S. dollar", "XUS", 999, "US$", "c", 100, rounding_spec(3, 2), None),
    ("European Euro", "EUR", 978, "", "", 100, rounding_spec(3, 2), None),
    ("German mark", "DEM", 276, "DM", "", 100, None, 2),
    ("Italian lira", "ITL", 380, "L", "", 1, None, 2),
    ("Japanese yen", "JPY", 392, "¥", "", 100, rounding_spec(3, 0), None),
    ("Pound sterling", "GBP", 826, "£", "p", 100, rounding_spec(3, 2), None),
    ("Some Currency", "CCY", 100, "#", "", 100, None, None),
    ("Bespoke up", "BUP", 1, "bu", "c", 1000, rounding_spec(1, 3), 0),
    ("Bespoke down", "BDN", 2, "bd", "c", 10, rounding_spec(2, 1), 0),
    ("Bespoke floor", "BFL", 3, "bf", "", 100, rounding_spec(4, 2, 7), None),
    ("Bespoke ceiling", "BCE", 4, "bc", "", 100, rounding_spec(5, 2, 3), None),
    ("Bespoke closest digit 9", "BC9", 5, "", "", 100, rounding_spec(3, 4, 9), 6),
    ("Bespoke closest digit 1", "BC1", 6, "", "", 100, rounding_spec(3, 16, 1), None),
    ("Unicode €", "UNI", 7, "€", "µ", 100, rounding_spec(3, 3), 2),
    ("u.s. dollar", "USL", 8, "$", "", 100, None, None),
    ("", "", 0, "", "", 0, None, None),
    ("Negative code", "NEG", -1, "-", "-", -100, rounding_spec(2, 0), None),
    ("Trailing space ", "TSP", 9, " ", " ", 100, rounding_spec(3, 1), 1),
    ("Chain", "CHN", 10, "ch", "", 100, rounding_spec(3, 3), 3),
]
ROUNDED = [1.23456, -1.23456, 0.125, -7.5, 1234.5678]


def make_rounding(spec):
    if spec is None:
        return ql.Rounding()
    return KINDS[spec["type"]](spec["precision"], spec["digit"])


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    built = []
    for name, code, numeric, symbol, fraction, per_unit, rounding, tri in CURRENCIES:
        triangulation = built[tri] if tri is not None else ql.Currency()
        built.append(ql.Currency(name, code, numeric, symbol, fraction, per_unit, make_rounding(rounding),
                                 triangulation))
    empty = ql.Currency()
    out = []
    for (name, code, numeric, symbol, fraction, per_unit, rounding, tri), c in zip(CURRENCIES, built):
        t = c.triangulationCurrency()
        out.append({"name": f"{code or 'empty code'}: {name or 'empty name'}", "inputs": {
            "settings": st,
            "currency": {"name": name, "code": code, "numericCode": numeric, "symbol": symbol,
                         "fractionSymbol": fraction, "fractionsPerUnit": per_unit, "rounding": rounding,
                         "triangulationCurrency": tri},
            "rounded": ROUNDED}, "expected": {
            "name": c.name(),
            "code": c.code(),
            "numericCode": c.numericCode(),
            "symbol": c.symbol(),
            "fractionSymbol": c.fractionSymbol(),
            "fractionsPerUnit": c.fractionsPerUnit(),
            "rounding": [v.enc(c.rounding()(x)) for x in ROUNDED],
            "triangulationCurrencyEmpty": t.empty(),
            "triangulationCurrency": None if t.empty() else t.code(),
            "empty": c.empty(),
            "equals": [c == o for o in built],
            "notEquals": [c != o for o in built],
            "equalsEmpty": c == empty,
            "notEqualsEmpty": c != empty}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/currency.hpp", cases)
