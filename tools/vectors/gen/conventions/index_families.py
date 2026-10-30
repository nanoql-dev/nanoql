# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's 27 Ibor families at every tenor the presets' probes used: each family at 1D, 1W, 2W, 1M, 2M,
3M, 4M, 6M, 9M, 1Y and 2Y, its name, family name, tenor, fixing days, currency, fixing calendar, convention, end of
month and day counter, or {"throws": true} where the family takes no tenor in the unit. A family's record in
indexes.xml holds its fields by the tenor's unit, so these check the record at each tenor against QuantLib's
constructor. tests/NanoQL.Tests/Vectors/Conventions/IndexFamiliesVectorTests.cs reads it back, through the coded
family and through ConventionSet.QuantLib."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

OUTPUT = "tests/vectors/conventions/index_families.json"
FAMILIES = ["AUDLibor", "Bbsw", "Bibor", "Bkbm", "CADLibor", "CHFLibor", "Cdor", "DKKLibor", "EURLibor", "Euribor",
            "Euribor365", "GBPLibor", "JPYLibor", "Jibar", "Mosprime", "NZDLibor", "Nibor", "Pribor", "Robor",
            "SEKLibor", "Shibor", "THBFIX", "TRLibor", "Tibor", "USDLibor", "Wibor", "Zibor"]
TENORS = ["1D", "1W", "2W", "1M", "2M", "3M", "4M", "6M", "9M", "1Y", "2Y"]
TODAY = ql.Date(15, ql.October, 2026)


def cases():
    out = []
    for family in FAMILIES:
        for tenor in TENORS:
            with v.case_settings(TODAY) as st:
                try:
                    i = getattr(ql, family)(ql.Period(tenor))
                    expected = {"name": i.name(), "familyName": i.familyName(),
                                "tenor": [i.tenor().length(), i.tenor().units()],
                                "fixingDays": i.fixingDays(), "currency": i.currency().code(),
                                "fixingCalendar": i.fixingCalendar().name(),
                                "businessDayConvention": i.businessDayConvention(), "endOfMonth": i.endOfMonth(),
                                "dayCounter": i.dayCounter().name()}
                except RuntimeError:
                    expected = {"index": {"throws": True}}
                out.append({"name": f"{family} {tenor}", "inputs": {"settings": st, "family": family, "tenor": tenor},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor", cases)
