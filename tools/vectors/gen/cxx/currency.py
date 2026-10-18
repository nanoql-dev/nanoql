# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's Currency (ql/currency.hpp) that the wheel doesn't expose
(docs/porting-playbook.md §9.4), each output computed by the C++ harness: the nine-argument constructor, whose last
argument, minorUnitCodes, the wheel's constructor doesn't take, and its inspectors, minorUnitCodes() among them: the
std::set the constructor copies the codes into, so sorted by byte and without duplicates; and operator<<, which writes the code, or "null currency" for the
empty currency.

Ten currencies, with no minor unit codes, QuantLib's own pairs (GBp and GBX, ZAc and ZAC, ILA, USc), unsorted lists
with duplicates, codes differing only in case, an empty code and non-ASCII codes (all in the Basic Multilingual Plane
below U+E000, where UTF-16's order, nanoql's ordinal order, is UTF-8's byte order), some with a triangulation
currency that has codes of its own; and the empty currency, written by operator<<. Each built currency gives the
constructor's inspectors (name, code, numeric code, symbol, fraction symbol, fractions per unit, emptiness, the
triangulation currency's code or null, the minor unit codes), its rounding applied to two values, and operator<<'s text twice: as "toString", nanoql's Currency.ToString(), and as "format", nanoql's
internal Currency.Format, which writes a currency into messages, the empty one included.

Each case's input "currency" is the harness's spec, by the inspectors' names (null for the empty currency): the
rounding a Rounding spec, null for Rounding(); the triangulation currency a currency spec or null; and the minor unit
codes as given to the constructor. Read back by tests/NanoQL.Tests/Vectors/Cxx/CurrencyCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

ROUNDED = [1.23456, -7.5]


def spec(name, code, numeric, symbol, fraction, per_unit, rounding, triangulation, minor):
    return {"name": name, "code": code, "numericCode": numeric, "symbol": symbol, "fractionSymbol": fraction,
            "fractionsPerUnit": per_unit, "rounding": rounding, "triangulationCurrency": triangulation,
            "minorUnitCodes": minor}


EUR = spec("European Euro", "EUR", 978, "", "", 100, {"precision": 2, "type": 3, "digit": 5}, None, [])
GBP = spec("British pound sterling", "GBP", 826, "£", "p", 100, None, None, ["GBp", "GBX"])
CURRENCIES = [
    ("no minor unit codes", EUR),
    ("pence", GBP),
    ("rand cents, given in reverse", spec("South-African rand", "ZAR", 710, "R", "", 100, None, None,
                                         ["ZAc", "ZAC"])),
    ("agorot and cents", spec("Israeli shekel", "ILS", 376, "NIS", "", 100, {"precision": 2, "type": 1, "digit": 5},
                              None, ["ILA"])),
    ("duplicates, unsorted", spec("U.S. dollar", "USD", 840, "$", "¢", 100, None, None,
                                  ["USc", "UScent", "USc", "USC", "USc"])),
    ("case and empty codes", spec("Bespoke", "BSP", 1, "b", "", 1000, {"precision": 3, "type": 2, "digit": 5}, None,
                                  ["b", "B", "", "bb", "Bb", "bB"])),
    ("non-ASCII codes", spec("Unicode", "UNI", 2, "€", "µ", 100, {"precision": 1, "type": 5, "digit": 9},
                             None, ["é", "e", "E", "É", "中", "µ", "z"])),
    ("triangulated through a currency with codes", spec("Legacy", "LGC", 3, "L", "", 1, None, GBP, ["LGc"])),
    ("triangulated, no codes", spec("Legacy two", "LG2", 4, "", "", 100,
                                    {"precision": 0, "type": 4, "digit": 1}, EUR, [])),
    ("empty strings", spec("", "", 0, "", "", 0, None, None, [])),
]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for name, c in CURRENCIES:
            inspectors = H.call("Currency", *c.values())
            expected = {k: inspectors[k] for k in ("name", "code", "numericCode", "symbol", "fractionSymbol",
                                                   "fractionsPerUnit", "empty", "triangulationCurrency",
                                                   "minorUnitCodes")}
            expected.update({
                "rounding": [v.attempt(lambda x=x: H.call("Currency::rounding", c, x)) for x in ROUNDED],
                "toString": H.call("operator<<(Currency)", c),
                "format": H.call("operator<<(Currency)", c)})
            out.append({"name": name, "inputs": {"settings": st, "currency": c, "rounded": ROUNDED},
                        "expected": expected})
        out.append({"name": "the empty currency", "inputs": {"settings": st, "currency": None, "rounded": []},
                    "expected": {"format": H.call("operator<<(Currency)", None)}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/currency.hpp", cases)
