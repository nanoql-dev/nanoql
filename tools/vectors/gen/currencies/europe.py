# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's European currencies (ql/currencies/europe.hpp): the 42 currencies it declares, one case each,
in the header's order. Each gives its name, code, numeric code, symbol, fraction symbol and fractions per unit; its
triangulation currency's code, null where it has none (QuantLib's empty currency); and its rounding applied to five
values.

QuantLib's source writes CYP's symbol (U+00A3) and GBP's symbol (U+00A3) as single Latin-1 bytes, which the wheel
returns as lone surrogate escapes (U+DCA2 for the byte 0xA2). The generator decodes every symbol as Latin-1
(s.encode("utf-8", "surrogateescape").decode("latin-1")), so the file holds the characters QuantLib means.

Each case's inputs are the class name ("currency") and the five values ("rounded"). The wheel's currency classes are
QuantLib-SWIG's proxies of the C++ classes of those names, built with their parameterless constructors; their accessors
are the C++ members, and the wheel's Rounding.__call__ is operator(), as tools/vectors/gen/currency.py records. Read
back by tests/NanoQL.Tests/Vectors/Currencies/EuropeVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

# The classes of ql/currencies/europe.hpp, in its order.
CLASSES = [
    "BGLCurrency",
    "BGNCurrency",
    "BYRCurrency",
    "CHFCurrency",
    "CZKCurrency",
    "DKKCurrency",
    "EEKCurrency",
    "EURCurrency",
    "GBPCurrency",
    "GELCurrency",
    "HUFCurrency",
    "ISKCurrency",
    "LTLCurrency",
    "LVLCurrency",
    "MKDCurrency",
    "NOKCurrency",
    "PLNCurrency",
    "ROLCurrency",
    "RONCurrency",
    "RSDCurrency",
    "RUBCurrency",
    "SEKCurrency",
    "SITCurrency",
    "TRLCurrency",
    "TRYCurrency",
    "UAHCurrency",
    "ATSCurrency",
    "BEFCurrency",
    "CYPCurrency",
    "DEMCurrency",
    "ESPCurrency",
    "FIMCurrency",
    "FRFCurrency",
    "GRDCurrency",
    "HRKCurrency",
    "IEPCurrency",
    "ITLCurrency",
    "LUFCurrency",
    "MTLCurrency",
    "NLGCurrency",
    "PTECurrency",
    "SKKCurrency",
]
ROUNDED = [1.23456, -1.23456, 0.125, -7.5, 1234.5678]


def latin1(s):
    """A symbol as QuantLib's bytes mean it: the wheel's surrogate escapes decoded as Latin-1."""
    return s.encode("utf-8", "surrogateescape").decode("latin-1")


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for cls in CLASSES:
        c = getattr(ql, cls)()
        t = c.triangulationCurrency()
        out.append({"name": f"{c.code()}: {c.name()}", "inputs": {"settings": st, "currency": cls, "rounded": ROUNDED},
                    "expected": {
                        "name": c.name(),
                        "code": c.code(),
                        "numericCode": c.numericCode(),
                        "symbol": latin1(c.symbol()),
                        "fractionSymbol": latin1(c.fractionSymbol()),
                        "fractionsPerUnit": c.fractionsPerUnit(),
                        "triangulationCurrency": None if t.empty() else t.code(),
                        "rounding": [v.enc(c.rounding()(x)) for x in ROUNDED]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/currencies/europe.hpp", cases)
