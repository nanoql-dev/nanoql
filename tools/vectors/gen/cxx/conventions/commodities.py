# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The commodity presets, which the wheel doesn't bind, written from the C++ harness: the generic region and its two
indexes, QuantLib's eight units of measure, its seven known conversion factors and the commodity and quantity settings'
defaults. Each value is QuantLib's source's, the line named, and checked on the harness's existing methods where they
show it: the generic indexes' family and region, their frequency, revised flag, lag and currency their constructor's
arguments; each unit's code, which the harness's units are; and each known factor with its direction, found by a direct
lookup. Two known factors are nanoql's, stored the right way round (docs/divergences.md,
Divergences/CommodityKnownFactorsTests.cs): the harness's QuantLib stores them backward, which the generator asserts.
ConventionSet.QuantLib reads the file; Vectors/Conventions/CommodityPresetTests.cs checks each record against its coded
class."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import conventions as c  # noqa: E402
import cxx  # noqa: E402

OUTPUT = "src/NanoQL.Extensions/Conventions/Presets/commodities.cxx.xml"

# petroleumunitsofmeasure.hpp:33-95 and unitofmeasure.hpp:138-145, in QuantLib's order: each class's Data(name, code,
# type[, triangulation unit])
UNITS = [
    ("BarrelUnitOfMeasure", "Barrels", "BBL", "Volume", None),  # :35
    ("MTUnitOfMeasure", "Metric Tonnes", "MT", "Mass", None),  # :44
    ("MBUnitOfMeasure", "1000 Barrels", "MB", "Volume", "BarrelUnitOfMeasure"),  # :53-54
    ("GallonUnitOfMeasure", "US Gallons", "GAL", "Volume", "BarrelUnitOfMeasure"),  # :63-64
    ("LitreUnitOfMeasure", "Litres", "l", "Volume", "BarrelUnitOfMeasure"),  # :73-74
    ("KilolitreUnitOfMeasure", "Kilolitres", "kl", "Volume", "BarrelUnitOfMeasure"),  # :83-84
    ("TokyoKilolitreUnitOfMeasure", "Tokyo Kilolitres", "KL_tk", "Volume", "BarrelUnitOfMeasure"),  # :93-94
    ("LotUnitOfMeasure", "Lot", "Lot", "Quantity", None),  # unitofmeasure.hpp:142
]
CODES = {cls: code for cls, _, code, _, _ in UNITS}

# unitofmeasureconversionmanager.cpp:106-133, addKnownConversionFactors, in its order: (source, target, factor) as
# nanoql stores them, and whether QuantLib stores the pair the other way round
KNOWN = [
    ("MBUnitOfMeasure", "BarrelUnitOfMeasure", 1000, False),
    ("BarrelUnitOfMeasure", "GallonUnitOfMeasure", 42, False),
    ("MBUnitOfMeasure", "GallonUnitOfMeasure", 1000 * 42, True),  # QuantLib's (GAL, MB, 1000 * 42), :115-118
    ("GallonUnitOfMeasure", "LitreUnitOfMeasure", 3.78541, True),  # QuantLib's (l, GAL, 3.78541), :119-122
    ("BarrelUnitOfMeasure", "LitreUnitOfMeasure", 158.987, False),
    ("KilolitreUnitOfMeasure", "BarrelUnitOfMeasure", 6.28981, False),
    ("TokyoKilolitreUnitOfMeasure", "BarrelUnitOfMeasure", 6.28981, False),
]
DIRECT = 0


def number(x):
    """A factor as XmlConvert writes it: an integral value without its fraction."""
    s = repr(float(x))
    return s[:-2] if s.endswith(".0") else s


def generic_records(H):
    """genericindexes.hpp:33-73: GenericCPI and YYGenericCPI, family CPI and YY_CPI, on the Generic region (GENERIC);
    their frequency, revised flag, lag and currency are their constructor's arguments, which the harness echoes back."""
    for frequency, revised, lag, currency in [(ql.Monthly, False, [3, int(ql.Months)], "EUR"),
                                              (ql.Quarterly, True, [2, int(ql.Months)], "GBP")]:
        r = H.call("GenericIndexes::values", int(frequency), revised, lag, currency)
        for key, family in [("cpi", "CPI"), ("yy", "YY_CPI")]:
            name, fam, freq, rev, length, units, region, code, ccy = r[key]
            assert (fam, region, code) == (family, "Generic", "GENERIC"), r[key]
            assert (freq, rev, [length, units], ccy) == (int(frequency), revised, lag, currency), r[key]
    return [
        c.record("Region", "GenericRegion", fields=[("RegionName", "Generic"), ("Code", "GENERIC")]),
        c.record("ZeroInflationIndex", "GenericCPI", fields=[("Family", "CPI"), ("Region", "GenericRegion")]),
        c.record("YoYInflationIndex", "YYGenericCPI", fields=[("Family", "YY_CPI"), ("Region", "GenericRegion")]),
    ]


def unit_records():
    out = []
    for cls, name, code, kind, tri in UNITS:
        fields = [("UnitName", name), ("Code", code), ("UnitType", kind)]
        out.append(c.record("UnitOfMeasure", cls, fields=fields + ([("TriangulationUnit", tri)] if tri else [])))
    return out


def conversion_records(H):
    """Each known factor by a direct lookup on QuantLib's null commodity type after clear(): its factor, and its
    direction, the reverse of nanoql's for the two QuantLib stores reversed."""
    queries = [["<NULL>", CODES[s], CODES[t], DIRECT] for s, t, _, _ in KNOWN]
    found = H.call("UnitOfMeasureConversionManager::lookups", [], queries)["lookups"]
    out = []
    for (s, t, factor, backward), f in zip(KNOWN, found):
        source, target = (CODES[t], CODES[s]) if backward else (CODES[s], CODES[t])
        assert f[0] == factor and f[1:4] == [source, target, DIRECT], (s, t, f)
        out.append(c.record("UnitOfMeasureConversion", f"{CODES[s]}-{CODES[t]}", fields=[
            ("Source", s), ("Target", t), ("Factor", number(factor))]))
    assert set(CODES.values()) - {"MT", "Lot"} == {u for r in queries for u in r[1:3]}
    return out


def settings_records():
    """commoditysettings.cpp:28, USD and barrels; quantity.cpp:27-29, no conversion and QuantLib's empty unit, which a
    record leaves out."""
    return [
        c.record("CommoditySettings", "CommoditySettings", fields=[("Currency", "USD"), ("UnitOfMeasure", "BarrelUnitOfMeasure")]),
        c.record("QuantitySettings", "QuantitySettings", fields=[("ConversionType", "NoConversion")]),
    ]


def records():
    with cxx.connect() as H:
        return generic_records(H) + unit_records() + conversion_records(H) + settings_records()


if __name__ == "__main__":
    c.write(OUTPUT, records())
