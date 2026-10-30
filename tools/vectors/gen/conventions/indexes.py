# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""QuantLib's index presets, written from the wheel: the six regions, the 27 Ibor families with their convention and
end of month by the tenor's unit, the fixed-tenor presets as their families at their tenors, the one-day Libors the
wheel has, the BMA index, the overnight indexes, the 12 swap index families with their Ibor index by tenor, and the
inflation indexes. A Libor family's financial-centre calendar, which the wheel doesn't expose, is the one calendar
preset whose join with London's gives the index's value and maturity dates on every fixing date of 2020 to 2030. Each
name is nanoql's class name; calendars and day counters are named as calendars.xml and daycounters.xml name them.
ConventionSet.QuantLib reads the file; Vectors/Conventions/IndexPresetTests.cs checks each record against its coded
preset."""
import os
import re
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "conventions.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import conventions as c  # noqa: E402

OUTPUT = "src/NanoQL.Extensions/Conventions/Presets/indexes.xml"
PRESETS = os.path.join(c.REPO, "src", "NanoQL.Extensions", "Conventions", "Presets")

CONVENTIONS = ["Following", "ModifiedFollowing", "Preceding", "ModifiedPreceding", "Unadjusted",
               "HalfMonthModifiedFollowing", "Nearest"]
FREQUENCIES = {-1: "NoFrequency", 0: "Once", 1: "Annual", 2: "Semiannual", 3: "EveryFourthMonth", 4: "Quarterly",
               6: "Bimonthly", 12: "Monthly", 13: "EveryFourthWeek", 26: "Biweekly", 52: "Weekly", 365: "Daily",
               999: "OtherFrequency"}
UNITS = [("Days", ql.Days), ("Weeks", ql.Weeks), ("Months", ql.Months), ("Years", ql.Years)]

LIBORS = ["AUDLibor", "CADLibor", "CHFLibor", "DKKLibor", "GBPLibor", "JPYLibor", "NZDLibor", "SEKLibor", "USDLibor"]
FAMILIES = ["AUDLibor", "Bbsw", "Bibor", "Bkbm", "CADLibor", "CHFLibor", "Cdor", "DKKLibor", "EURLibor", "Euribor",
            "Euribor365", "GBPLibor", "JPYLibor", "Jibar", "Mosprime", "NZDLibor", "Nibor", "Pribor", "Robor",
            "SEKLibor", "Shibor", "THBFIX", "TRLibor", "Tibor", "USDLibor", "Wibor", "Zibor"]
# the fixed-tenor presets, each its family at its tenor
FIXED = [(f"Bbsw{n}M", "Bbsw", f"{n}M") for n in range(1, 7)] + [
    ("BiborSW", "Bibor", "1W"), ("Bibor1M", "Bibor", "1M"), ("Bibor2M", "Bibor", "2M"), ("Bibor3M", "Bibor", "3M"),
    ("Bibor6M", "Bibor", "6M"), ("Bibor1Y", "Bibor", "1Y")] + [
    (f"Bkbm{n}M", "Bkbm", f"{n}M") for n in range(1, 7)] + [
    ("EURLibor1M", "EURLibor", "1M"), ("EURLibor3M", "EURLibor", "3M"), ("EURLibor6M", "EURLibor", "6M"),
    ("EURLibor1Y", "EURLibor", "1Y"),
    ("Euribor1W", "Euribor", "1W"), ("Euribor1M", "Euribor", "1M"), ("Euribor3M", "Euribor", "3M"),
    ("Euribor6M", "Euribor", "6M"), ("Euribor1Y", "Euribor", "1Y")]
ONE_DAY_LIBORS = ["CADLiborON", "GBPLiborON", "USDLiborON"]
OVERNIGHT = ["Aonia", "Cdi", "Corra", "Destr", "Eonia", "Estr", "FedFunds", "Kofr", "Nzocr", "Saron", "Shir", "Sofr",
             "Sonia", "Swestr", "Tonar", "Zaronia"]
SWAPS = ["ChfLiborSwapIsdaFix", "EurLiborSwapIfrFix", "EurLiborSwapIsdaFixA", "EurLiborSwapIsdaFixB",
         "EuriborSwapIfrFix", "EuriborSwapIsdaFixA", "EuriborSwapIsdaFixB", "GbpLiborSwapIsdaFix",
         "JpyLiborSwapIsdaFixAm", "JpyLiborSwapIsdaFixPm", "UsdLiborSwapIsdaFixAm", "UsdLiborSwapIsdaFixPm"]
ZERO = ["AUCPI", "EUHICP", "EUHICPXT", "FRHICP", "UKHICP", "UKRPI", "USCPI", "ZACPI"]
YOY = ["YYEUHICP", "YYEUHICPXT", "YYFRHICP", "YYUKRPI", "YYUSCPI", "YYZACPI"]
# each region preset by its class, with an index of the region the wheel reaches it through
REGIONS = [("AustraliaRegion", lambda: ql.AUCPI(ql.Monthly, False)), ("EURegion", ql.EUHICP),
           ("FranceRegion", ql.FRHICP), ("UKRegion", ql.UKRPI), ("USRegion", ql.USCPI), ("ZARegion", ql.ZACPI)]
# the presets taking arguments: theirs are no fields of their records
ARGUMENTS = {"AUCPI": ("Frequency", "Revised")}


def names(file, element):
    """A presets file's QuantLib names, each with its preset's name."""
    text = open(os.path.join(PRESETS, file), encoding="utf-8").read()
    pattern = rf'<{element} name="([^"]+)">\s*<QuantLibName>([^<]*)</QuantLibName>'
    return {m.group(2): m.group(1) for m in re.finditer(pattern, text)}


CALENDARS = names("calendars.xml", "CalendarPreset")
DAY_COUNTERS = names("daycounters.xml", "DayCounterPreset")


def calendar(cal):
    return CALENDARS[cal.name()]


def day_counter(dc):
    return DAY_COUNTERS[dc.name()]


def boolean(b):
    return "true" if b else "false"


def unit_of(name, family):
    """The family's units, each with its convention and end of month, checked the same at two tenors of the unit."""
    units = []
    for unit_name, unit in UNITS:
        try:
            indexes = [family(ql.Period(n, unit)) for n in ((1, 2) if unit != ql.Years else (1, 2))]
        except RuntimeError:
            continue
        conv = {i.businessDayConvention() for i in indexes}
        eom = {i.endOfMonth() for i in indexes}
        assert len(conv) == 1 and len(eom) == 1, (name, unit_name)
        units.append((unit_name, conv.pop(), eom.pop()))
    return units


def financial_center(name, index):
    """The calendar preset whose join with London's gives the Libor's value and maturity dates over 2020 to 2030:
    London's own where the centre is London, whose join any calendar its holidays cover gives too."""
    london = ql.UnitedKingdom(ql.UnitedKingdom.Exchange)
    fixings = [d for d in (ql.Date(1, 1, 2020) + i for i in range(0, 11 * 365, 3)) if index.isValidFixingDate(d)]
    expected = [(index.valueDate(d), index.maturityDate(index.valueDate(d))) for d in fixings]
    matches = []
    for preset in CALENDARS.values():
        t, _, market = preset.partition(".")
        if market and not hasattr(getattr(ql, t), market):
            continue  # a market the wheel doesn't name (Germany's Euwax) is no Libor's centre
        joint = ql.JointCalendar(london, _calendar(preset), ql.JoinHolidays)
        ok = True
        for d, (v, m) in zip(fixings, expected):
            vd = joint.adjust(index.fixingCalendar().advance(d, index.fixingDays(), ql.Days))
            if vd != v or joint.advance(vd, index.tenor(), index.businessDayConvention(), index.endOfMonth()) != m:
                ok = False
                break
        if ok:
            matches.append(preset)
    if "UnitedKingdom.Exchange" in matches:
        # a centre that is London joins to London's own dates, as every calendar whose holidays London's covers does
        return "UnitedKingdom.Exchange"
    assert len(matches) == 1, (name, matches)
    return matches[0]


def _calendar(preset):
    """The wheel's calendar of a nanoql calendar preset's name."""
    t, _, market = preset.partition(".")
    cls = getattr(ql, t)
    return cls(getattr(cls, market)) if market else cls()


def region_records():
    out = []
    for name, index in REGIONS:
        r = index().region()
        out.append(c.record("Region", name, fields=[("RegionName", r.name()), ("Code", r.code())]))
    return out


def family_records():
    out = []
    for name in FAMILIES:
        family = getattr(ql, name)
        units = unit_of(name, family)
        at = family(ql.Period(1, ql.Months))
        fields = [("Family", at.familyName()), ("FixingDays", str(at.fixingDays()))]
        if units[0][0] == "Days":
            one_day = family(ql.Period(1, ql.Days)).fixingDays()
            if one_day != at.fixingDays():
                fields.append(("FixingDaysAtOneDay", str(one_day)))
        fields.append(("Currency", at.currency().code()))
        if name == "EURLibor":
            # its fixing calendar is London's joined with TARGET, its value and maturity calendar TARGET
            m = re.fullmatch(r"JoinHolidays\((.*), (.*)\)", at.fixingCalendar().name())
            fields += [("FixingCalendar", CALENDARS[m.group(1)]), ("FinancialCenterCalendar", CALENDARS[m.group(2)])]
        else:
            fields.append(("FixingCalendar", calendar(at.fixingCalendar())))
        if name in LIBORS:
            fields.append(("FinancialCenterCalendar", financial_center(name, family(ql.Period(3, ql.Months)))))
        fields.append(("DayCounter", day_counter(at.dayCounter())))
        for unit_name, conv, eom in units:
            fields.append(("Unit", [("TimeUnit", unit_name), ("Convention", CONVENTIONS[conv]),
                                    ("EndOfMonth", boolean(eom))]))
        out.append(c.record("IborFamily", name, fields=fields))
    return out


def same_ibor(a, b):
    return (a.familyName(), a.tenor(), a.fixingDays(), a.currency().code(), a.fixingCalendar().name(),
            a.businessDayConvention(), a.endOfMonth(), a.dayCounter().name()) == (
        b.familyName(), b.tenor(), b.fixingDays(), b.currency().code(), b.fixingCalendar().name(),
        b.businessDayConvention(), b.endOfMonth(), b.dayCounter().name())


def fixed_records():
    out = []
    for name, family, tenor in FIXED:
        assert same_ibor(getattr(ql, name)(), getattr(ql, family)(ql.Period(tenor))), name
        out.append(c.record("IborIndex", name, base=family, fields=[("Tenor", tenor)]))
    return out


def one_day_records():
    out = []
    for name in ONE_DAY_LIBORS:
        i = getattr(ql, name)()
        m = re.fullmatch(r"JoinHolidays\((.*), (.*)\)", i.fixingCalendar().name())
        out.append(c.record("IborIndex", name, fields=[
            ("Family", i.familyName()), ("Tenor", str(i.tenor())), ("FixingDays", str(i.fixingDays())),
            ("Currency", i.currency().code()), ("FixingCalendar", CALENDARS[m.group(1)]),
            ("Convention", CONVENTIONS[i.businessDayConvention()]), ("EndOfMonth", boolean(i.endOfMonth())),
            ("DayCounter", day_counter(i.dayCounter())), ("FinancialCenterCalendar", CALENDARS[m.group(2)])]))
    return out


def bma_record():
    b = ql.BMAIndex()
    return c.record("IborIndex", "BMAIndex", fields=[
        ("Family", b.familyName()), ("Tenor", str(b.tenor())), ("FixingDays", str(b.fixingDays())),
        ("Currency", b.currency().code()), ("FixingCalendar", calendar(b.fixingCalendar())),
        ("DayCounter", day_counter(b.dayCounter()))])


def overnight_records():
    out = []
    for name in OVERNIGHT:
        i = getattr(ql, name)()
        assert i.businessDayConvention() == ql.Following and not i.endOfMonth(), name
        out.append(c.record("OvernightIndex", name, fields=[
            ("Family", i.familyName()), ("FixingDays", str(i.fixingDays())), ("Currency", i.currency().code()),
            ("FixingCalendar", calendar(i.fixingCalendar())), ("DayCounter", day_counter(i.dayCounter()))]))
    return out


def swap_records():
    out = []
    for name in SWAPS:
        family = getattr(ql, name)
        long_, short = family(ql.Period(10, ql.Years)), family(ql.Period(1, ql.Years))
        for t in (2, 5, 30):
            s = family(ql.Period(t, ql.Years))
            assert s.iborIndex().name() == long_.iborIndex().name() and s.fixedLegTenor() == long_.fixedLegTenor()
        fields = [("Family", long_.familyName()), ("SettlementDays", str(long_.fixingDays())),
                  ("Currency", long_.currency().code()), ("FixingCalendar", calendar(long_.fixingCalendar())),
                  ("FixedLegTenor", str(long_.fixedLegTenor())),
                  ("FixedLegConvention", CONVENTIONS[long_.fixedLegConvention()]),
                  ("FixedLegDayCounter", day_counter(long_.dayCounter())),
                  ("IborIndex", long_.iborIndex().familyName()), ("IborIndexTenor", str(long_.iborIndex().tenor()))]
        if short.iborIndex().name() != long_.iborIndex().name():
            fields += [("ShortIborIndex", short.iborIndex().familyName()),
                       ("ShortIborIndexTenor", str(short.iborIndex().tenor()))]
        if short.fixedLegTenor() != long_.fixedLegTenor():
            fields.append(("ShortFixedLegTenor", str(short.fixedLegTenor())))
        out.append(c.record("SwapIndex", name, fields=fields))
    return out


def inflation_fields(name, i):
    region = next(n for n, index in REGIONS if index().region().name() == i.region().name())
    fields = [("Family", i.familyName()), ("Region", region), ("Revised", boolean(i.revised())),
              ("Frequency", FREQUENCIES[i.frequency()]), ("AvailabilityLag", str(i.availabilityLag())),
              ("Currency", i.currency().code())]
    return [f for f in fields if f[0] not in ARGUMENTS.get(name, ())]


def inflation_records():
    out = []
    for name in ZERO:
        i = getattr(ql, name)(ql.Monthly, False) if name in ARGUMENTS else getattr(ql, name)()
        out.append(c.record("ZeroInflationIndex", name, fields=inflation_fields(name, i)))
    for name in YOY:
        out.append(c.record("YoYInflationIndex", name, fields=inflation_fields(name, getattr(ql, name)())))
    return out


def records():
    return (region_records() + family_records() + fixed_records() + one_day_records() + [bma_record()]
            + overnight_records() + swap_records() + inflation_records())


if __name__ == "__main__":
    c.write(OUTPUT, records())
