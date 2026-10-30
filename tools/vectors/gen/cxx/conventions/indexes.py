# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""QuantLib's index presets the wheel doesn't bind, written from the C++ harness: the five daily-tenor Libors, their
settlement days their constructors' argument and so no field of their records; EURLiborON, DailyTenorEURLibor at no
settlement days; and YYAUCPI, its frequency and revised flag its constructor's arguments. A Libor's fixing calendar,
London's joined with the currency's financial centre, is written as the two (EUR's is TARGET alone). Each name is
nanoql's class name; calendars and day counters are named as calendars.xml and daycounters.xml name them.
ConventionSet.QuantLib reads the file; Vectors/Conventions/IndexPresetTests.cs checks each record against its coded
preset."""
import importlib.util
import os
import re
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import conventions as c  # noqa: E402
import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("cxx_eurlibor",
                                               os.path.join(v.GEN_ROOT, "cxx", "indexes", "ibor", "eurlibor.py"))
eurlibor = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(eurlibor)

OUTPUT = "src/NanoQL.Extensions/Conventions/Presets/indexes.cxx.xml"
PRESETS = os.path.join(c.REPO, "src", "NanoQL.Extensions", "Conventions", "Presets")
CONVENTIONS = ["Following", "ModifiedFollowing", "Preceding", "ModifiedPreceding", "Unadjusted",
               "HalfMonthModifiedFollowing", "Nearest"]
UNITS = ["D", "W", "M", "Y"]
DAILY_TENOR_LIBORS = ["DailyTenorCHFLibor", "DailyTenorEURLibor", "DailyTenorGBPLibor", "DailyTenorJPYLibor",
                      "DailyTenorUSDLibor"]


def names(file, element):
    text = open(os.path.join(PRESETS, file), encoding="utf-8").read()
    pattern = rf'<{element} name="([^"]+)">\s*<QuantLibName>([^<]*)</QuantLibName>'
    return {m.group(2): m.group(1) for m in re.finditer(pattern, text)}


CALENDARS = names("calendars.xml", "CalendarPreset")
DAY_COUNTERS = names("daycounters.xml", "DayCounterPreset")


def daily_tenor(H, name):
    """A daily-tenor Libor's record, its settlement days left to its constructor."""
    e = eurlibor.preset_case(H, {"type": name, "settlementDays": 2}, name, 2)["expected"]
    length, unit = e["tenor"]
    joint = re.fullmatch(r"JoinHolidays\((.*), (.*)\)", e["fixingCalendar"])
    calendars = ([("FixingCalendar", CALENDARS[joint.group(1)])] if joint else
                 [("FixingCalendar", CALENDARS[e["fixingCalendar"]])])
    fields = [("Family", e["familyName"]), ("Tenor", f"{length}{UNITS[unit]}"), ("Currency", e["currency"])]
    fields += calendars + [("Convention", CONVENTIONS[e["businessDayConvention"]]),
                           ("EndOfMonth", "true" if e["endOfMonth"] else "false"),
                           ("DayCounter", DAY_COUNTERS[e["dayCounter"]])]
    if joint:
        fields.append(("FinancialCenterCalendar", CALENDARS[joint.group(2)]))
    return c.record("IborIndex", name, fields=fields)


def eurlibor_on(H):
    e = eurlibor.preset_case(H, {"type": "EURLiborON"}, "EURLiborON")["expected"]
    return c.record("IborIndex", "EURLiborON", base="DailyTenorEURLibor", fields=[("FixingDays", str(e["fixingDays"]))])


def yyaucpi(H):
    """YYAUCPI's record, its frequency and revised flag left to its constructor."""
    with H.case_settings(v.enc(eurlibor.eur.TODAY)):
        e = H.call("YYAUCPI::inspectors", 4, False, v.enc(eurlibor.eur.TODAY))
    lag = f"{e['availabilityLagLength']}{UNITS[e['availabilityLagUnits']]}"
    assert e["regionName"] == "Australia" and not e["ratio"], e
    return c.record("YoYInflationIndex", "YYAUCPI", fields=[
        ("Family", e["familyName"]), ("Region", "AustraliaRegion"), ("AvailabilityLag", lag), ("Currency", e["currency"])])


def records():
    with cxx.connect() as H:
        return [daily_tenor(H, n) for n in DAILY_TENOR_LIBORS] + [eurlibor_on(H), yyaucpi(H)]


if __name__ == "__main__":
    c.write(OUTPUT, records())
