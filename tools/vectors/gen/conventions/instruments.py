# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""QuantLib's market builders' defaults, written from the wheel as the default records the builders read: the swap
builder's by currency (its fixed leg's tenor, with GBP's and AUD's thresholds, day counter, conventions, rules and end
of month), the OIS builder's with SONIA's and CORRA's settlement days, the CDS builder's with its calendar, the cap
builder's first caplet, the swaption builder's settlement and option convention, the multiple-resets swap builder's
fixed convention and averaging, and the fixed-rate bond constructors' defaults. A default record holds only what its
builder fixes, never what it derives from its index. Each value is read off a built instrument where the wheel shows
it, and otherwise taken from QuantLib's source, the line named, and checked on the wheel where it shows. Calendars and
day counters are named as calendars.xml and daycounters.xml name them. ConventionSet.QuantLib reads the file;
Vectors/Conventions/InstrumentPresetTests.cs checks each builder through it against the bare builder."""
import os
import re
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "conventions.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import conventions as c  # noqa: E402

OUTPUT = "src/NanoQL.Extensions/Conventions/Presets/instruments.xml"
PRESETS = os.path.join(c.REPO, "src", "NanoQL.Extensions", "Conventions", "Presets")

TODAY = ql.Date(15, ql.October, 2026)
CONVENTIONS = ["Following", "ModifiedFollowing", "Preceding", "ModifiedPreceding", "Unadjusted",
               "HalfMonthModifiedFollowing", "Nearest"]
RULES = {getattr(ql.DateGeneration, n): n for n in dir(ql.DateGeneration) if not n.startswith("_") and n != "thisown"}
FREQUENCIES = {1: "Annual", 2: "Semiannual", 4: "Quarterly", 12: "Monthly"}
AVERAGING = {ql.RateAveraging.Simple: "Simple", ql.RateAveraging.Compound: "Compound"}
SETTLEMENT_TYPES = {ql.Settlement.Physical: "Physical", ql.Settlement.Cash: "Cash"}
SETTLEMENT_METHODS = {ql.Settlement.PhysicalOTC: "PhysicalOTC", ql.Settlement.PhysicalCleared: "PhysicalCleared",
                      ql.Settlement.CollateralizedCashPrice: "CollateralizedCashPrice",
                      ql.Settlement.ParYieldCurve: "ParYieldCurve"}
# the swap builder's currencies (makevanillaswap.cpp:113-167), each with a currency object for its index
CURRENCIES = {"AUD": ql.AUDCurrency, "CHF": ql.CHFCurrency, "EUR": ql.EURCurrency, "GBP": ql.GBPCurrency,
              "HKD": ql.HKDCurrency, "JPY": ql.JPYCurrency, "SEK": ql.SEKCurrency, "THB": ql.THBCurrency,
              "USD": ql.USDCurrency}
# GBP's and AUD's thresholds (makevanillaswap.cpp:117-131): the comparison, the threshold, the tenor on its side,
# each checked on the wheel at the tenors either side
THRESHOLDS = {"GBP": ("AtMost", "1Y", "1Y", ["12M", "1Y"], ["13M", "2Y"]),
              "AUD": ("AtLeast", "4Y", "6M", ["48M", "4Y", "5Y"], ["47M", "1Y", "3Y"])}


def names(file, element):
    """A presets file's QuantLib names, each with its preset's name."""
    text = open(os.path.join(PRESETS, file), encoding="utf-8").read()
    pattern = rf'<{element} name="([^"]+)">\s*<QuantLibName>([^<]*)</QuantLibName>'
    return {m.group(2): m.group(1) for m in re.finditer(pattern, text)}


CALENDARS = names("calendars.xml", "CalendarPreset")
DAY_COUNTERS = names("daycounters.xml", "DayCounterPreset")


def boolean(b):
    return "true" if b else "false"


def period(p):
    return f"{p.length()}{['D', 'W', 'M', 'Y'][p.units()]}"


def curve():
    return ql.YieldTermStructureHandle(ql.FlatForward(TODAY, 0.03, ql.Actual365Fixed()))


def index(code):
    """A plain Ibor index of the currency, Modified Following without end of month, so the swap's floating leg shows
    the builder's own defaults."""
    return ql.IborIndex(f"{code}IDX", ql.Period("6M"), 2, CURRENCIES[code](), ql.TARGET(), ql.ModifiedFollowing, False,
                        ql.Actual360(), curve())


def swap_record(code):
    """MakeVanillaSwap.<code>: the fixed leg's tenor, its threshold, day counter, conventions, rules and end of month,
    and the floating leg's rule and end of month; the floating leg's others are its index's."""
    def build(tenor, **kw):
        return ql.MakeVanillaSwap(ql.Period(tenor), index(code), 0.03, **kw)

    probe = build("5Y", fixedLegTenor=ql.Period("1Y")) if code == "THB" else build("5Y")
    fixed, floating = probe.fixedSchedule(), probe.floatingSchedule()
    fields = []
    if code == "THB":
        # no tenor rule for THB, as QuantLib has none: its builder refuses without a fixed leg tenor
        try:
            build("5Y")
            raise AssertionError("THB's tenor")
        except RuntimeError as e:
            assert "unknown fixed leg default tenor" in str(e), e
    elif code in THRESHOLDS:
        comparison, threshold, at, inside, outside = THRESHOLDS[code]
        tenors = {period(build(t).fixedSchedule().tenor()) for t in outside}
        assert len(tenors) == 1, (code, tenors)
        assert {period(build(t).fixedSchedule().tenor()) for t in inside} == {at}, code
        fields.append(("FixedLegTenor", tenors.pop()))
        fields.append(("FixedLegTenorThreshold", [("Comparison", comparison), ("SwapTenor", threshold), ("FixedLegTenor", at)]))
    else:
        tenors = {period(build(t).fixedSchedule().tenor()) for t in ("1Y", "2Y", "5Y", "30Y")}
        assert len(tenors) == 1, (code, tenors)
        fields.append(("FixedLegTenor", tenors.pop()))
    fields += [("FixedLegConvention", CONVENTIONS[fixed.businessDayConvention()]),
               ("FixedLegTerminationDateConvention", CONVENTIONS[fixed.terminationDateBusinessDayConvention()]),
               ("FixedLegRule", RULES[fixed.rule()]),
               ("FixedLegEndOfMonth", boolean(fixed.endOfMonth())),
               ("FixedLegDayCounter", DAY_COUNTERS[probe.fixedDayCount().name()]),
               ("FloatingLegRule", RULES[floating.rule()]),
               ("FloatingLegEndOfMonth", boolean(floating.endOfMonth()))]
    return c.record("SwapConvention", f"MakeVanillaSwap.{code}", fields=fields)


def settlement_days(swap, cal):
    days = 0
    while cal.advance(TODAY, days, ql.Days) < swap.startDate():
        days += 1
    assert cal.advance(TODAY, days, ql.Days) == swap.startDate()
    return days


def ois_records():
    """MakeOIS, its settlement days a plain overnight index's, and MakeOIS.Sonia and MakeOIS.Corra, theirs."""
    plain = ql.OvernightIndex("ON", 0, ql.USDCurrency(), ql.UnitedStates(ql.UnitedStates.GovernmentBond), ql.Actual360(), curve())
    swap = ql.MakeOIS(ql.Period("5Y"), plain, 0.03)
    fixed, overnight = swap.fixedSchedule(), swap.floatingSchedule()
    assert fixed.tenor() == overnight.tenor(), "one payment frequency"
    fields = [("SettlementDays", str(settlement_days(swap, plain.fixingCalendar()))),
              ("PaymentLag", str(swap.paymentLag())),
              ("PaymentConvention", CONVENTIONS[swap.paymentConvention()]),
              ("PaymentFrequency", FREQUENCIES[swap.paymentFrequency()]),
              ("FixedLegFrequency", FREQUENCIES[fixed.tenor().frequency()]),
              ("FixedLegConvention", CONVENTIONS[fixed.businessDayConvention()]),
              ("FixedLegTerminationDateConvention", CONVENTIONS[fixed.terminationDateBusinessDayConvention()]),
              ("FixedLegRule", RULES[fixed.rule()]),
              ("OvernightLegConvention", CONVENTIONS[overnight.businessDayConvention()]),
              ("OvernightLegTerminationDateConvention", CONVENTIONS[overnight.terminationDateBusinessDayConvention()]),
              ("OvernightLegRule", RULES[overnight.rule()]),
              # makeois.hpp: telescopicValueDates_ = false, which the wheel doesn't show
              ("TelescopicValueDates", "false"),
              ("Averaging", AVERAGING[swap.averagingMethod()]),
              ("LockoutDays", str(swap.lockoutDays())),
              ("ObservationShift", boolean(swap.applyObservationShift()))]
    out = [c.record("OisConvention", "MakeOIS", fields=fields)]
    for name, cls in (("Corra", ql.Corra), ("Sonia", ql.Sonia)):
        idx = cls(curve())
        days = settlement_days(ql.MakeOIS(ql.Period("5Y"), idx, 0.03), idx.fixingCalendar())
        out.append(c.record("OisConvention", f"MakeOIS.{name}", base="MakeOIS", fields=[("SettlementDays", str(days))]))
    return out


def cds_record():
    """MakeCreditDefaultSwap: its coupons, rule, convention, day counters, accrual flags, cash settlement days and
    calendar."""
    cds = ql.MakeCreditDefaultSwap(ql.Period("5Y"), 0.01)
    coupons = [ql.as_fixed_rate_coupon(cf) for cf in cds.coupons()]
    full = coupons[1]
    assert ql.Period(full.accrualEndDate() - full.accrualStartDate(), ql.Days) is not None
    months = (full.accrualEndDate().year() - full.accrualStartDate().year()) * 12 + full.accrualEndDate().month() - full.accrualStartDate().month()
    # makecds.cpp: rule_(DateGeneration::CDS), Following, WeekendsOnly; checked: the coupon dates are the 20ths of
    # March, June, September and December, rolled Following on weekends only
    for cp in coupons[:-1]:
        d = cp.accrualEndDate()
        assert d.month() in (3, 6, 9, 12) and ql.WeekendsOnly().adjust(ql.Date(20, d.month(), d.year())) == d, d
    upfront_days = 0
    while ql.WeekendsOnly().advance(cds.tradeDate(), upfront_days, ql.Days) < cds.upfrontPayment().date():
        upfront_days += 1
    assert upfront_days == cds.cashSettlementDays()
    fields = [("Calendar", CALENDARS[ql.WeekendsOnly().name()]),
              ("CouponFrequency", FREQUENCIES[12 // months]),
              ("PaymentConvention", "Following"),
              ("Rule", "CDS"),
              ("DayCounter", DAY_COUNTERS[full.dayCounter().name()]),
              ("LastPeriodDayCounter", DAY_COUNTERS[coupons[-1].dayCounter().name()]),
              ("SettlesAccrual", boolean(cds.settlesAccrual())),
              ("PaysAtDefaultTime", boolean(cds.paysAtDefaultTime())),
              ("RebatesAccrual", boolean(cds.rebatesAccrual())),
              ("CashSettlementDays", str(cds.cashSettlementDays()))]
    return c.record("CdsConvention", "MakeCreditDefaultSwap", fields=fields)


def cap_record():
    """MakeCapFloor: whether the first caplet is excluded, the cap's caplets against its swap's floating coupons."""
    idx = ql.Euribor6M(curve())
    cap = ql.MakeCapFloor(ql.CapFloor.Cap, ql.Period("5Y"), idx, 0.03)
    swap = ql.MakeVanillaSwap(ql.Period("5Y"), idx, 0.0, fixedLegTenor=ql.Period("1Y"))
    excluded = len(cap.floatingLeg()) == len(swap.floatingLeg()) - 1
    assert excluded or len(cap.floatingLeg()) == len(swap.floatingLeg())
    return c.record("CapFloorConvention", "MakeCapFloor", fields=[("FirstCapletExcluded", boolean(excluded))])


def swaption_record():
    """MakeSwaption: the settlement type and method, and the option tenor's convention."""
    idx = ql.EuriborSwapIsdaFixA(ql.Period("10Y"), curve())
    swaption = ql.MakeSwaption(idx, ql.Period("1Y"), 0.03)
    # makeswaption.cpp: optionConvention_(ModifiedFollowing); checked: the exercise is the advance of the adjusted
    # evaluation date by it
    cal = idx.fixingCalendar()
    assert swaption.exercise().dates()[0] == cal.advance(cal.adjust(TODAY), ql.Period("1Y"), ql.ModifiedFollowing)
    return c.record("SwaptionConvention", "MakeSwaption", fields=[
        ("SettlementType", SETTLEMENT_TYPES[swaption.settlementType()]),
        ("SettlementMethod", SETTLEMENT_METHODS[swaption.settlementMethod()]),
        ("OptionConvention", "ModifiedFollowing")])


def multiple_resets_record():
    """MakeMultipleResetsSwap: the fixed leg's convention and the resets' averaging."""
    swap = ql.MakeMultipleResetsSwap(ql.Period("5Y"), ql.Euribor3M(curve()), 4, fixedRate=0.03)
    return c.record("MultipleResetsSwapConvention", "MakeMultipleResetsSwap", fields=[
        ("FixedLegConvention", CONVENTIONS[swap.fixedSchedule().businessDayConvention()]),
        ("Averaging", AVERAGING[swap.averagingMethod()])])


def bond_record():
    """FixedRateBond: the constructors' defaults a set's bonds take, the payment convention, redemption, ex-coupon
    convention and end of month, and the helper's price type."""
    schedule = ql.Schedule(ql.Date(15, ql.March, 2026), ql.Date(15, ql.March, 2036), ql.Period("6M"),
                           ql.UnitedStates(ql.UnitedStates.GovernmentBond), ql.Unadjusted, ql.Unadjusted,
                           ql.DateGeneration.Backward, False)
    bond = ql.FixedRateBond(2, 100.0, schedule, [0.04], ql.ActualActual(ql.ActualActual.ISMA))
    # fixedratebond.hpp: paymentConvention = Following, redemption = 100.0, exCouponConvention = Unadjusted,
    # exCouponEndOfMonth = false; checked: a coupon whose date falls on a Sunday is paid the Monday after
    cal = schedule.calendar()
    sunday = [cf for cf in bond.cashflows() if ql.as_coupon(cf) is not None
              and ql.as_coupon(cf).accrualEndDate().weekday() == ql.Sunday]
    assert sunday and all(cf.date() == cal.adjust(ql.as_coupon(cf).accrualEndDate(), ql.Following) for cf in sunday)
    redemption = bond.redemption().amount()
    # fixedratebondhelper.hpp: priceType = Bond::Price::Clean; checked: a curve bootstrapped on the helper alone prices
    # the bond's clean price at the quote, its dirty price off it
    helper = ql.FixedRateBondHelper(ql.QuoteHandle(ql.SimpleQuote(100.0)), 2, 100.0, schedule, [0.04],
                                    ql.ActualActual(ql.ActualActual.ISMA))
    bootstrapped = ql.PiecewiseFlatForward(TODAY, [helper], ql.Actual365Fixed())
    bond.setPricingEngine(ql.DiscountingBondEngine(ql.YieldTermStructureHandle(bootstrapped)))
    assert abs(bond.cleanPrice() - 100.0) < 1e-8 and abs(bond.dirtyPrice() - 100.0) > 1e-4
    assert redemption == int(redemption)
    return c.record("BondConvention", "FixedRateBond", fields=[
        ("PaymentConvention", "Following"),
        ("Redemption", str(int(redemption))),
        ("ExCouponConvention", "Unadjusted"),
        ("ExCouponEndOfMonth", "false"),
        ("PriceType", "Clean")])


def records():
    ql.Settings.instance().evaluationDate = TODAY
    return ([swap_record(code) for code in sorted(CURRENCIES)] + ois_records() + [cds_record(), cap_record(), swaption_record(),
            multiple_resets_record(), bond_record()])


if __name__ == "__main__":
    c.write(OUTPUT, records())
