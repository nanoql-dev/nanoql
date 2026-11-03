# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""The shipped conventions' catalogue, docs/conventions-catalogue.md, written from the presets files: every record
ConventionSet.QuantLib reads, by kind, each with its name, QuantLib's name where the record gives one, its base and its
fields, in the files' order; and FpML's codes with Exhibit A's notice, since the page displays them outside software.
The page carries FpML's licence beside nanoql's, as aliases.xml does."""
import os
import sys
import xml.etree.ElementTree as ET

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "conventions.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import conventions as c  # noqa: E402

OUTPUT = "docs/conventions-catalogue.md"
PRESETS = os.path.join(c.REPO, "src", "NanoQL.Extensions", "Conventions", "Presets")
# The files in the order ConventionSet.QuantLib reads them (ConventionsXml's PresetFiles).
FILES = ["calendars.xml", "daycounters.xml", "currencies.xml", "indexes.xml", "indexes.cxx.xml", "instruments.xml",
         "instruments.cxx.xml", "commodities.cxx.xml", "aliases.xml"]
NS = "{" + c.NAMESPACE + "}"

# The kinds by section, each with its title and what its records are.
SECTIONS = [
    ("Calendars", [("CalendarPreset", "QuantLib's calendars, each by its type and market, its holiday edits empty.")]),
    ("Day counters", [("DayCounterPreset", "QuantLib's day counters, each by its type and convention.")]),
    ("Currencies", [("CurrencyPreset", "QuantLib's currencies, by their ISO codes.")]),
    ("Regions and indexes", [
        ("Region", "QuantLib's regions."),
        ("IborIndex", "QuantLib's Ibor index presets, each fixed tenor on its family."),
        ("IborFamily", "QuantLib's Ibor families, which take a tenor: the convention and end of month by its unit."),
        ("OvernightIndex", "QuantLib's overnight indexes."),
        ("SwapIndex", "QuantLib's swap index families, which take a tenor."),
        ("ZeroInflationIndex", "QuantLib's zero inflation indexes; a preset taking arguments gives the rest of its fields."),
        ("YoYInflationIndex", "QuantLib's quoted year-on-year inflation indexes."),
    ]),
    ("The market builders' defaults", [
        ("SwapConvention", "MakeVanillaSwap's defaults by the index's currency."),
        ("OisConvention", "MakeOIS's defaults, and their settlement days by the index's class."),
        ("CdsConvention", "MakeCreditDefaultSwap's defaults."),
        ("CapFloorConvention", "MakeCapFloor's defaults."),
        ("SwaptionConvention", "MakeSwaption's defaults."),
        ("CmsConvention", "MakeCms's defaults."),
        ("MultipleResetsSwapConvention", "MakeMultipleResetsSwap's defaults."),
        ("YoYInflationCapFloorConvention", "MakeYoYInflationCapFloor's defaults."),
        ("BondConvention", "The bonds' defaults, FixedRateBond's and its helper's, and BTP's conventions."),
    ]),
    ("Commodities", [
        ("UnitOfMeasure", "QuantLib's units of measure."),
        ("UnitOfMeasureConversion", "QuantLib's known conversion factors, on its null commodity type, two of them the right "
                                    "way round\n(`docs/divergences.md`, the known unit factors)."),
        ("CommoditySettings", "QuantLib's commodity settings."),
        ("QuantitySettings", "QuantLib's quantity settings."),
    ]),
]

NOTICE = """The FpML Specifications of this document are subject to the FpML Public
License (the “License”); you may not use the FpML Specifications except in
compliance with the License. You may obtain a copy of the License at
https://www.FpML.org.

The FpML Specifications distributed under the License are distributed on an
“AS IS” basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
the License for the specific language governing rights and limitations under
the License.

The Licensor of the FpML Specifications is the International Swaps and
Derivatives Association, Inc. All Rights Reserved."""


def records():
    """Each file's records in order: (file, element, name, base, [(field, text)])."""
    out = []
    for f in FILES:
        root = ET.parse(os.path.join(PRESETS, f)).getroot()
        for e in root:
            if not isinstance(e.tag, str) or not e.tag.startswith(NS):
                continue
            fields = []
            for x in e:
                tag = x.tag[len(NS):]
                if len(x):
                    text = ", ".join(f"{y.tag[len(NS):]} {y.text}" for y in x)
                else:
                    text = (x.text or "").strip()
                fields.append((tag, text))
            out.append((f, e.tag[len(NS):], e.get("name"), e.get("base"), fields))
    return out


def cell(text):
    return text.replace("|", "\\|") if text else ""


def table(rows):
    """A markdown table of one kind's records: its name, its base where any has one, then every field any record gives,
    in their first record's order."""
    columns = []
    for _, _, _, _, fields in rows:
        for field, _ in fields:
            if field not in columns:
                columns.append(field)
    based = any(b for _, _, _, b, _ in rows)
    head = ["Name"] + (["Base"] if based else []) + columns
    lines = ["| " + " | ".join(head) + " |", "|" + "---|" * len(head)]
    for _, _, name, base, fields in rows:
        values = {}
        for field, text in fields:
            values[field] = (values[field] + "; " + text) if field in values else text
        row = [f"`{name}`"] + ([cell(base)] if based else []) + [cell(values.get(col, "")) for col in columns]
        lines.append("| " + " | ".join(row) + " |")
    return lines


def page():
    rs = records()
    lines = ["<!--"] + [f"  {h}" if h else "" for h in [
        "SPDX-FileCopyrightText: 2026 The nanoql Authors",
        "SPDX-FileCopyrightText: International Swaps and Derivatives Association, Inc.",
        "SPDX-License-Identifier: Apache-2.0 AND LicenseRef-FpML-Public-License-3.0",
        "",
        "Generated by tools/vectors/gen/conventions/catalogue.py from the shipped presets files; regenerate, don't edit.",
    ]] + ["-->", "", "# The shipped conventions", ""]
    lines += [
        "Every record `ConventionSet.QuantLib` reads, by kind: QuantLib's presets and its builders' defaults, each with",
        "QuantLib's values, generated from QuantLib and checked against it (`docs/conventions.md`). A record named after a",
        "coded preset is that preset's; a record named after a builder, `MakeVanillaSwap.EUR` or `MakeOIS`, holds the",
        "options its builder fixes. Each table lists a record's name, its base where any record of the kind has one, and the",
        "fields it gives, in the files' order; a field left out comes from the record's base, or QuantLib's reference.",
        "",
    ]
    for title, kinds in SECTIONS:
        lines += [f"## {title}", ""]
        for element, about in kinds:
            rows = [r for r in rs if r[1] == element]
            if not rows:
                continue
            files = sorted({r[0] for r in rows}, key=FILES.index)
            lines += [f"### {element} ({len(rows)})", "", about, f"From {', '.join(f'`{f}`' for f in files)}.", ""]
            lines += table(rows) + [""]

    shared = [r for r in rs if r[1] == "Alias" and r[0] != "aliases.xml"]
    lines += ["## Aliases", "", "### QuantLib's shared implementations", "",
              "Presets whose implementation QuantLib shares, each an alias of the first.", ""]
    lines += ["| Name | Base | File |", "|---|---|---|"]
    lines += [f"| `{n}` | `{b}` | `{f}` |" for f, _, n, b, _ in shared] + [""]
    fpml = [r for r in rs if r[0] == "aliases.xml"]
    lines += ["### FpML's codes", "",
              "FpML's codes from four of its lists, the `day-count-fraction`, `business-center` and `floating-rate-index` coding",
              "schemes and the `BusinessDayConventionEnum`, each an alias of the nanoql name it maps to exactly, from `aliases.xml`.",
              "The codes are FpML's, under the FpML® Public License, Version 3.0 (`LICENSES/LicenseRef-FpML-Public-License-3.0.txt`);",
              "the mapping of each code to a nanoql name is nanoql's own work, not part of the FpML Specifications, under the Apache",
              "License, Version 2.0. The licence's Exhibit A notice:", "", "```text", NOTICE, "```", ""]
    lines += ["| Code | nanoql's name |", "|---|---|"]
    lines += [f"| `{n}` | `{b}` |" for _, _, n, b, _ in fpml]
    return "\n".join(lines) + "\n"


if __name__ == "__main__":
    root = sys.argv[1] if len(sys.argv) > 1 else c.REPO
    path = os.path.join(root, *OUTPUT.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(page().encode("utf-8"))
# REUSE-IgnoreEnd
