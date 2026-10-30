# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""QuantLib's calendar presets, written from the wheel: each calendar type and market nanoql's API names, as
TypeName or TypeName.Market, with QuantLib's name and no holiday edits. A market whose name QuantLib shares with an
earlier member of its type shares that member's implementation (Indonesia's BEJ, JSX and IDX, bejImpl), so it is an
alias of the first. Markets the wheel's enumerations don't name (Germany's Euwax, Indonesia's IDX) are built by their
integers, which the wheel's constructors take; QuantLib's deprecated markets nanoql doesn't port (Israel's Settlement,
Italy's Exchange) have no preset. ConventionSet.QuantLib reads the file; Vectors/Conventions/ConventionPresetTests.cs
checks each record against its coded preset."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "conventions.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import conventions as c  # noqa: E402

OUTPUT = "src/NanoQL.Extensions/Conventions/Presets/calendars.xml"

# Each type's markets as nanoql's API names them, in its enumeration's order, with QuantLib's Market integer; None
# for a type without markets.
MARKETS = {
    "Argentina": [("Merval", 0)],
    "Australia": [("Settlement", 0), ("ASX", 1)],
    "Austria": [("Settlement", 0), ("Exchange", 1)],
    "Botswana": None,
    "Brazil": [("Settlement", 0), ("Exchange", 1)],
    "Canada": [("Settlement", 0), ("TSX", 1)],
    "Chile": [("SSE", 0)],
    "China": [("SSE", 0), ("IB", 1)],
    "Croatia": [("ZSE", 0)],
    "CzechRepublic": [("PSE", 0)],
    "Denmark": None,
    "Finland": None,
    "France": [("Settlement", 0), ("Exchange", 1)],
    "Germany": [("Settlement", 0), ("FrankfurtStockExchange", 1), ("Xetra", 2), ("Eurex", 3), ("Euwax", 4)],
    "HongKong": [("HKEx", 0)],
    "Hungary": None,
    "Iceland": [("ICEX", 0)],
    "India": [("NSE", 0)],
    "Indonesia": [("BEJ", 0), ("JSX", 1), ("IDX", 2)],
    "Israel": [("TASE", 1), ("SHIR", 2), ("Telbor", 3)],
    "Italy": [("Settlement", 0)],
    "Japan": None,
    "Malaysia": [("KLSE", 0)],
    "Malta": [("MSE", 0)],
    "Mexico": [("BMV", 0)],
    "Montenegro": [("MNSE", 0)],
    "NewZealand": [("Wellington", 0), ("Auckland", 1)],
    "NorthMacedonia": [("MSE", 0)],
    "Norway": None,
    "NullCalendar": None,
    "Philippines": [("PSE", 0)],
    "Poland": [("Settlement", 0), ("WSE", 1)],
    "Romania": [("Public", 0), ("BVB", 1)],
    "Russia": [("Settlement", 0), ("MOEX", 1)],
    "SaudiArabia": [("Tadawul", 0)],
    "Serbia": [("BSE", 0)],
    "Singapore": [("SGX", 0)],
    "Slovakia": [("BSSE", 0)],
    "Slovenia": [("LSE", 0)],
    "SouthAfrica": None,
    "SouthKorea": [("Settlement", 0), ("KRX", 1)],
    "Sweden": None,
    "Switzerland": None,
    "TARGET": None,
    "Taiwan": [("TSEC", 0)],
    "Thailand": None,
    "Turkey": None,
    "Ukraine": [("USE", 0)],
    "UnitedKingdom": [("Settlement", 0), ("Exchange", 1), ("Metals", 2)],
    "UnitedStates": [("Settlement", 0), ("NYSE", 1), ("GovernmentBond", 2), ("NERC", 3), ("LiborImpact", 4),
                     ("FederalReserve", 5), ("SOFR", 6)],
    "Uzbekistan": [("UZSE", 0)],
    "WeekendsOnly": None,
}


def records():
    out = []
    for type_name in sorted(MARKETS):
        cls = getattr(ql, type_name)
        markets = MARKETS[type_name]
        if markets is None:
            out.append(c.record("CalendarPreset", type_name, fields=[("QuantLibName", cls().name())]))
            continue
        first_by_name = {}
        for member, value in markets:
            name = f"{type_name}.{member}"
            quantlib_name = cls(value).name()
            if quantlib_name in first_by_name:
                out.append(c.record("Alias", name, base=first_by_name[quantlib_name]))
            else:
                first_by_name[quantlib_name] = name
                out.append(c.record("CalendarPreset", name, fields=[("QuantLibName", quantlib_name)]))
    return out


if __name__ == "__main__":
    c.write(OUTPUT, records())
