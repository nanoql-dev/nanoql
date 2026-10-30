# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""QuantLib's day counter presets, written from the wheel: each day counter type, with its convention or its flag as
nanoql's API names them (Thirty360.BondBasis, Actual360.IncludeLastDay), and QuantLib's name. A convention whose name
QuantLib shares with an earlier member of its enumeration shares that member's implementation (ActualActual's Bond
and ISMA, Thirty360's ISMA and BondBasis), so it is an alias of the first. Business252 carries the calendar whose
business days it counts, QuantLib's default Brazilian settlement calendar. ConventionSet.QuantLib reads the file;
Vectors/Conventions/ConventionPresetTests.cs checks each record against its coded preset."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "conventions.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import conventions as c  # noqa: E402

OUTPUT = "src/NanoQL.Extensions/Conventions/Presets/daycounters.xml"

# Each preset by nanoql's name, with the wheel's day counter, in nanoql's order: a type, then its conventions in
# their enumeration's order, or its flag.
PRESETS = [
    ("Actual360", ql.Actual360()),
    ("Actual360.IncludeLastDay", ql.Actual360(True)),
    ("Actual364", ql.Actual364()),
    ("Actual36525", ql.Actual36525()),
    ("Actual36525.IncludeLastDay", ql.Actual36525(True)),
    ("Actual365Fixed", ql.Actual365Fixed()),
    ("Actual365Fixed.Canadian", ql.Actual365Fixed(ql.Actual365Fixed.Canadian)),
    ("Actual365Fixed.NoLeap", ql.Actual365Fixed(ql.Actual365Fixed.NoLeap)),
    ("Actual366", ql.Actual366()),
    ("Actual366.IncludeLastDay", ql.Actual366(True)),
] + [("ActualActual." + m, ql.ActualActual(getattr(ql.ActualActual, m)))
     for m in ["ISMA", "Bond", "ISDA", "Historical", "Actual365", "AFB", "Euro"]] + [
    ("Business252", ql.Business252()),
    ("OneDayCounter", ql.OneDayCounter()),
    ("SimpleDayCounter", ql.SimpleDayCounter()),
] + [("Thirty360." + m, ql.Thirty360(getattr(ql.Thirty360, m)))
     for m in ["USA", "BondBasis", "European", "EurobondBasis", "Italian", "German", "ISMA", "ISDA", "NASD"]] + [
    ("Thirty365", ql.Thirty365()),
]


def records():
    out = []
    first_by_name = {}
    for name, dc in PRESETS:
        quantlib_name = dc.name()
        if quantlib_name in first_by_name:
            out.append(c.record("Alias", name, base=first_by_name[quantlib_name]))
            continue
        first_by_name[quantlib_name] = name
        fields = [("QuantLibName", quantlib_name)]
        if name == "Business252":
            # QuantLib's default calendar, Brazil(Settlement), whose name the day counter's carries
            assert quantlib_name == "Business/252(" + ql.Brazil(ql.Brazil.Settlement).name() + ")"
            fields.append(("Calendar", "Brazil.Settlement"))
        out.append(c.record("DayCounterPreset", name, fields=fields))
    return out


if __name__ == "__main__":
    c.write(OUTPUT, records())
