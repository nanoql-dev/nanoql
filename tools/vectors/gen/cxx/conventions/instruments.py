# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""QuantLib's defaults the wheel doesn't bind, written from the C++ harness: MakeCms's and MakeYoYInflationCapFloor's
default records, and BTP's preset record. Each value is QuantLib's source's, the line named, and checked on the
harness's existing methods: the builder given the record's values explicitly builds what the bare builder builds, on a
case where each value shows (a stubbed CMS swap priced, a cap's coupons), and a changed value builds something else;
BTP's cash flows and settlement date are a wheel FixedRateBond's on the record's conventions. Calendars and day
counters are named as calendars.xml and daycounters.xml name them. ConventionSet.QuantLib reads the file;
Vectors/Conventions/InstrumentPresetTests.cs checks each builder and BTP through it against its coded defaults."""
import importlib.util
import os
import re
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import conventions as c  # noqa: E402
import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


cms = _load("cxx_makecms", "cxx/instruments/makecms.py")
yoy = _load("cxx_makeyoyinflationcapfloor", "cxx/instruments/makeyoyinflationcapfloor.py")
btp = _load("cxx_btp", "cxx/instruments/bonds/btp.py")
irx = cms.irx

OUTPUT = "src/NanoQL.Extensions/Conventions/Presets/instruments.cxx.xml"
PRESETS = os.path.join(c.REPO, "src", "NanoQL.Extensions", "Conventions", "Presets")
MF = int(ql.ModifiedFollowing)
BACKWARD = int(ql.DateGeneration.Backward)


def names(file, element):
    text = open(os.path.join(PRESETS, file), encoding="utf-8").read()
    pattern = rf'<{element} name="([^"]+)">\s*<QuantLibName>([^<]*)</QuantLibName>'
    return {m.group(2): m.group(1) for m in re.finditer(pattern, text)}


CALENDARS = names("calendars.xml", "CalendarPreset")
DAY_COUNTERS = names("daycounters.xml", "DayCounterPreset")


def cms_record(H):
    """makecms.cpp:36-49: a 3M CMS leg, Modified Following for its dates and termination, backward, no end of month,
    Actual/360; the floating leg backward without end of month."""
    explicit = [["withCmsLegTenor", [3, cms.M]], ["withCmsLegConvention", MF], ["withCmsLegTerminationDateConvention", MF],
                ["withCmsLegRule", BACKWARD], ["withCmsLegEndOfMonth", False], ["withCmsLegDayCount", irx.A360],
                ["withFloatingLegRule", BACKWARD], ["withFloatingLegEndOfMonth", False]]

    def build(months, setters):
        # stubbed swaps from a month end, priced, to the last day of April (a 30-day month's end, where the end-of-month
        # flag shows) and to a month-end Saturday (where the termination convention shows)
        effective = ql.Date(31, ql.March, 2026).serialNumber()
        with H.case_settings(v.enc(cms.TODAY)):
            return H.call("MakeCms::operator ext::shared_ptr<Swap>", [months, cms.M], cms.cp.CMS10, None,
                          [["withEffectiveDate", effective]] + setters, cms.PRICER)

    cases = (61, 62)
    bare = [build(m, []) for m in cases]
    assert [build(m, explicit) for m in cases] == bare, "MakeCms's defaults"
    for i, (setter, value) in enumerate(explicit):
        changed = {"withCmsLegTenor": [6, cms.M], "withCmsLegDayCount": irx.A365}.get(setter)
        if changed is None:
            changed = (not value) if isinstance(value, bool) else (int(ql.Following) if value == MF else int(ql.DateGeneration.Forward))
        assert [build(m, explicit[:i] + [[setter, changed]] + explicit[i + 1:]) for m in cases] != bare, setter
    return c.record("CmsConvention", "MakeCms", fields=[
        ("CmsLegTenor", "3M"), ("CmsLegConvention", "ModifiedFollowing"), ("CmsLegTerminationDateConvention", "ModifiedFollowing"),
        ("CmsLegRule", "Backward"), ("CmsLegEndOfMonth", "false"), ("CmsLegDayCounter", DAY_COUNTERS[ql.Actual360().name()]),
        ("FloatingLegRule", "Backward"), ("FloatingLegEndOfMonth", "false")])


def yoy_record(H):
    """makeyoyinflationcapfloor.cpp:31-35: payments on 30/360 (Bond Basis), Modified Following, and no fixing days."""
    explicit = {"paymentDayCounter": irx.THIRTY360_BB, "paymentAdjustment": MF, "fixingDays": 0}

    def build(**setters):
        with H.case_settings(v.enc(yoy.TODAY)):
            return H.call("MakeYoYInflationCapFloor::build", yoy.ic.RATIO, yoy.builder(yoy.CAP, 5, strike=0.03, **setters))

    bare = build()
    assert build(**explicit) == bare, "MakeYoYInflationCapFloor's defaults"
    for setter, changed in (("paymentDayCounter", irx.A365), ("paymentAdjustment", int(ql.Preceding)), ("fixingDays", 2)):
        assert build(**{**explicit, setter: changed}) != bare, setter
    names_seen = {cp["dayCounter"] for cp in bare["coupons"]}
    assert names_seen == {ql.Thirty360(ql.Thirty360.BondBasis).name()}, names_seen
    return c.record("YoYInflationCapFloorConvention", "MakeYoYInflationCapFloor", fields=[
        ("PaymentDayCounter", DAY_COUNTERS[names_seen.pop()]), ("PaymentAdjustment", "ModifiedFollowing"), ("FixingDays", "0")])


def btp_record(H):
    """btp.cpp:68-85: two settlement days, a semiannual schedule back from the maturity on NullCalendar, unadjusted, at
    month end, Actual/Actual (ISMA) coupons paid Modified Following on TARGET."""
    start, maturity, rate = ql.Date(1, ql.September, 2024), ql.Date(1, ql.March, 2034), 0.0395
    with H.case_settings(v.enc(btp.TODAY)):
        r = H.call("BTP", btp.btp(maturity, rate, start=start), btp.DISCOUNT, 100.0, [None])
    ql.Settings.instance().evaluationDate = btp.TODAY
    schedule = ql.Schedule(start, maturity, ql.Period(6, ql.Months), ql.NullCalendar(), ql.Unadjusted, ql.Unadjusted,
                           ql.DateGeneration.Backward, True)
    bond = ql.FixedRateBond(2, 100.0, schedule, [rate], ql.ActualActual(ql.ActualActual.ISMA), ql.ModifiedFollowing,
                            100.0, ql.Date(), ql.TARGET())
    expected = btp.bond_expected(r[:6])
    assert expected["settlementDate"] == v.enc(bond.settlementDate()), (expected["settlementDate"], bond.settlementDate())
    assert expected["cashflowDates"] == [v.enc(cf.date()) for cf in bond.cashflows()]
    assert all(abs(a - cf.amount()) <= 1e-12 for a, cf in zip(expected["cashflowAmounts"], bond.cashflows()))
    return c.record("BondConvention", "BTP", fields=[
        ("SettlementDays", "2"), ("Frequency", "Semiannual"), ("Calendar", CALENDARS[ql.NullCalendar().name()]),
        ("Convention", "Unadjusted"), ("TerminationDateConvention", "Unadjusted"), ("Rule", "Backward"), ("EndOfMonth", "true"),
        ("AccrualDayCounter", DAY_COUNTERS[ql.ActualActual(ql.ActualActual.ISMA).name()]), ("PaymentConvention", "ModifiedFollowing"),
        ("PaymentCalendar", CALENDARS[ql.TARGET().name()])])


def records():
    with cxx.connect() as H:
        return [cms_record(H), yoy_record(H), btp_record(H)]


if __name__ == "__main__":
    c.write(OUTPUT, records())
