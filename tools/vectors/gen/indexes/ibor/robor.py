# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ROBOR, the Romanian interbank offered rate (ql/indexes/ibor/robor.hpp): Robor at 1D and 3M,
one case each, as tools/vectors/gen/indexes/ibor/euribor.py writes them, with its DATES and index_forecast_case: the
name, conventions and fixing calendar, the fixing, value and maturity dates, and the forecasts on its two curves. The
wheel's classes are QuantLib-SWIG's proxies of the C++ classes of those names, their constructors QuantLib's. Each
case's "skip" input lists the outputs that move when the wheel's two Romanian calendars carry the holidays QuantLib's
master adds (#2919, tools/vectors/gen/time/calendars/romania.py's added_holidays(), taken ahead of the pin), as
[output, date index] or ["forecast", curve index, date index]; each is null, and the reader nulls the same entries.
Read back by tests/NanoQL.Tests/Vectors/Indexes/Ibor/RoborVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_euribor",
                                               os.path.join(v.GEN_ROOT, "indexes", "ibor", "euribor.py"))
eur = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(eur)
_spec = importlib.util.spec_from_file_location("wheel_romania",
                                               os.path.join(v.GEN_ROOT, "time", "calendars", "romania.py"))
romania = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(romania)

SPECS = [({"type": "Robor", "tenor": eur.tenor_spec((1, ql.Days))}, lambda h: ql.Robor(ql.Period(1, ql.Days), h)),
         ({"type": "Robor", "tenor": eur.tenor_spec((3, ql.Months))}, lambda h: ql.Robor(ql.Period(3, ql.Months), h))]
SKIPPED = ["fixingDate", "valueDate", "maturityDate"]


def moved(base, fixed):
    """The entries of a case's date and forecast outputs that differ between the two runs."""
    out = [[name, i] for name in SKIPPED for i, (a, b) in enumerate(zip(base[name], fixed[name])) if a != b]
    out += [["forecast", k, i] for k, (ra, rb) in enumerate(zip(base["forecast"], fixed["forecast"]))
            for i, (a, b) in enumerate(zip(ra, rb)) if a != b]
    return out


def cases():
    base = [eur.index_forecast_case(spec, make) for spec, make in SPECS]
    calendars = [ql.Romania(ql.Romania.Public), ql.Romania(ql.Romania.BVB)]
    added = romania.added_holidays()
    for c in calendars:
        for d in added:
            c.addHoliday(d)
    try:
        fixed = [eur.index_forecast_case(spec, make) for spec, make in SPECS]
    finally:
        for c in calendars:
            for d in added:
                c.removeHoliday(d)
    for b, f in zip(base, fixed):
        skip = moved(b["expected"], f["expected"])
        for entry in skip:
            if entry[0] == "forecast":
                b["expected"]["forecast"][entry[1]][entry[2]] = None
            else:
                b["expected"][entry[0]][entry[1]] = None
        b["inputs"]["skip"] = skip
    return base


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/robor.hpp", cases)
