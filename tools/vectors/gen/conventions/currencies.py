# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""QuantLib's currency presets, written from the wheel: each of its 111 currencies by its ISO code, with QuantLib's
name, numeric code, symbol, fraction symbol, fractions per unit, rounding and triangulation currency's code.

The wheel's Rounding exposes no type, precision or digit, only its operator() (__call__), so each currency's rounding
is the one candidate among QuantLib's rounding classes (UpRounding, DownRounding, ClosestRounding, FloorTruncation,
CeilingTruncation, each at precisions 0 to 16 and digits 0 to 9, or no rounding) that rounds every probe value as the
currency's does; the generator asserts it is unique. QuantLib's source writes CYP's and GBP's symbols as Latin-1
bytes, which the wheel returns as surrogate escapes, so every symbol is decoded as Latin-1, as
gen/currencies/europe.py does. ConventionSet.QuantLib reads the file; Vectors/Conventions/ConventionPresetTests.cs
checks each record against its coded preset."""
import inspect
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "conventions.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import conventions as c  # noqa: E402

OUTPUT = "src/NanoQL.Extensions/Conventions/Presets/currencies.xml"

# The rounding classes by nanoql's RoundingType names.
ROUNDINGS = [("Up", ql.UpRounding), ("Down", ql.DownRounding), ("Closest", ql.ClosestRounding),
             ("Floor", ql.FloorTruncation), ("Ceiling", ql.CeilingTruncation)]

# Values whose rounding tells every candidate apart: each digit after each decimal place, either sign.
PROBES = [s * (k + d / 10.0) / 10.0 ** p for p in range(0, 9) for k in (1, 7) for d in range(10) for s in (1, -1)]
PROBES += [s * x for x in (1.23456, 0.125, 7.5, 1234.5678, 0.12345678912345) for s in (1, -1)]


def latin1(s):
    return s.encode("utf-8", "surrogateescape").decode("latin-1")


def rounding(r):
    if all(r(x) == x for x in PROBES):
        return [("Type", "None")]
    found = [(name, p, d) for name, cls in ROUNDINGS for p in range(17) for d in range(10)
             if all(cls(p, d)(x) == r(x) for x in PROBES)]
    assert len(found) == 1, found
    name, p, d = found[0]
    return [("Type", name), ("Precision", str(p)), ("Digit", str(d))]


def records():
    codes = sorted(n[:-len("Currency")] for n, o in vars(ql).items()
                   if inspect.isclass(o) and n.endswith("Currency") and issubclass(o, ql.Currency)
                   and o is not ql.Currency)
    assert len(codes) == 111, len(codes)
    out = []
    for code in codes:
        cur = getattr(ql, code + "Currency")()
        assert cur.code() == code
        tri = cur.triangulationCurrency()
        fields = [("QuantLibName", cur.name()), ("NumericCode", str(cur.numericCode())),
                  ("Symbol", latin1(cur.symbol())), ("FractionSymbol", latin1(cur.fractionSymbol())),
                  ("FractionsPerUnit", str(cur.fractionsPerUnit())),
                  ("Rounding", rounding(cur.rounding()))]
        if not tri.empty():
            fields.append(("TriangulationCurrency", tri.code()))
        out.append(c.record("CurrencyPreset", code, fields=fields))
    return out


if __name__ == "__main__":
    c.write(OUTPUT, records())
