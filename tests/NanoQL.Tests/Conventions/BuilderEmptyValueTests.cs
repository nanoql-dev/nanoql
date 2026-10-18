// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The builders take QuantLib's empty values as <see langword="null"/> and read them as QuantLib does: its empty
/// calendar and day counter, its <c>Null&lt;Natural&gt;()</c> and <c>Null&lt;Rate&gt;()</c>. Where QuantLib uses an
/// empty calendar unchecked, it builds wherever every use is an <see cref="BusinessDayConvention.Unadjusted"/>
/// adjustment or a move in weeks, months or years, and any other use is refused with QuantLib's text. And
/// <c>default(Date)</c>, which is never a date, is refused by the setter given it, under its own parameter's name. The
/// expected values are the QuantLib-Python 1.44 wheel's.
/// </summary>
public class BuilderEmptyValueTests
{
    private const string NoCalendar = "no calendar implementation provided";

    private static readonly Date Today = new(15, January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly FlatForward Curve = new(Today, 0.03, new Actual365Fixed());

    // QuantLib's withCalendar(Calendar()) is no calendar: NullCalendar, and Unadjusted by default, also after a
    // calendar was set (schedule.cpp:551-554, 606-632); TARGET would give 15 January 2029 and 14 January 2030.
    [Fact]
    public void MakeScheduleTakesQuantLibsEmptyCalendarAsNoCalendar()
    {
        Schedule s = new MakeSchedule().From(new Date(13, January, 2027)).To(new Date(13, January, 2030))
            .WithTenor(new Period(6, TimeUnit.Months)).WithCalendar(new TARGET()).WithCalendar(null).ToSchedule();

        Assert.Equal(
            [
                new Date(13, January, 2027), new Date(13, July, 2027), new Date(13, January, 2028),
                new Date(13, July, 2028), new Date(13, January, 2029), new Date(13, July, 2029),
                new Date(13, January, 2030),
            ],
            s.Dates);
        Assert.Equal(new NullCalendar(), s.Calendar);
        Assert.Equal("Null", s.Calendar!.ToString());
        Assert.Equal(BusinessDayConvention.Unadjusted, s.BusinessDayConvention);
        Assert.Equal(BusinessDayConvention.Unadjusted, s.TerminationDateBusinessDayConvention());
    }

    // QuantLib's empty first-period day counter is the rate's (fixedratecoupon.cpp:142-146, 205-208).
    [Fact]
    public void FixedRateLegTakesQuantLibsEmptyFirstPeriodDayCounterAsTheRates()
    {
        FixedRateLeg leg = new FixedRateLeg(Annual()).WithNotionals(100.0).WithCouponRates(0.05, new Actual360());
        ImmutableArray<CashFlow> reset =
            leg.WithFirstPeriodDayCounter(new Actual365Fixed()).WithFirstPeriodDayCounter(null).ToLeg();

        Assert.Equal(["Actual/360", "Actual/360", "Actual/360"], reset.Select(c => ((FixedRateCoupon)c).DayCounter.Name));
        Assert.Equal(leg.ToLeg().Select(c => c.Amount(Context)), reset.Select(c => c.Amount(Context)));
    }

    // A zero ex-coupon period, in any unit, never reads the calendar (fixedratecoupon.cpp:191; Period() == 0W,
    // period.cpp:350-356): QuantLib's empty calendar gives no ex-coupon dates.
    [Fact]
    public void FixedRateLegTakesQuantLibsEmptyExCouponCalendarWithAZeroPeriod()
    {
        FixedRateLeg leg = new FixedRateLeg(Annual()).WithNotionals(100.0).WithCouponRates(0.05, new Actual360());
        foreach (Period p in new[] { default, new Period(0, TimeUnit.Weeks) })
        {
            ImmutableArray<CashFlow> built = leg.WithExCouponPeriod(p, null, BusinessDayConvention.Following).ToLeg();
            Assert.All(built, c => Assert.Null(((Coupon)c).ExCouponDate));
        }
    }

    // Null<Natural>() as the lookback is no lookback, which QuantLib's own swap passes (overnightindexedswap.cpp:174).
    [Fact]
    public void OvernightLegTakesQuantLibsNullLookbackAsNone()
    {
        OvernightLeg leg = new OvernightLeg(SemiAnnual(), new Estr(Curve)).WithNotionals(100.0);
        FloatingRateCoupon[] reset = [.. leg.WithLookbackDays(2).WithLookbackDays(null).ToLeg().Cast<FloatingRateCoupon>()];
        FloatingRateCoupon[] plain = [.. leg.ToLeg().Cast<FloatingRateCoupon>()];

        Assert.Equal(plain.Select(c => c.FixingDays), reset.Select(c => c.FixingDays));
        Assert.Equal(plain.Select(c => c.FixingDate()), reset.Select(c => c.FixingDate()));
    }

    // The same through MakeOIS (makeois.hpp:148, makeois.cpp:386-389); a lookback of 2 gives 2 days and
    // 14 January 2026.
    [Fact]
    public void MakeOISTakesQuantLibsNullLookbackAsNone()
    {
        OvernightIndexedSwap s = new MakeOIS(new Period(1, TimeUnit.Years), new Estr(Curve)).WithFixedRate(0.03)
            .WithLookbackDays(2).WithLookbackDays(null).ToOvernightIndexedSwap(Context);
        var c = (FloatingRateCoupon)s.OvernightLeg[0];

        Assert.Equal(0, c.FixingDays);
        Assert.Equal(new Date(16, January, 2026), c.FixingDate());
    }

    // Null<Natural>() as settlement days is the index's default, which an effective date doesn't clash with
    // (makeois.cpp:56-76).
    [Fact]
    public void MakeOISTakesQuantLibsNullSettlementDaysAsTheDefault()
    {
        MakeOIS b = new MakeOIS(new Period(1, TimeUnit.Years), new Estr(Curve)).WithFixedRate(0.03);

        OvernightIndexedSwap a = b.WithEffectiveDate(new Date(15, January, 2027)).WithSettlementDays(1)
            .WithSettlementDays(null).ToOvernightIndexedSwap(Context);
        Assert.Equal(new Date(15, January, 2027), a.StartDate());
        Assert.Equal(new Date(17, January, 2028), a.MaturityDate());

        OvernightIndexedSwap d = b.WithSettlementDays(3).WithSettlementDays(null).ToOvernightIndexedSwap(Context);
        Assert.Equal(new Date(17, January, 2025), d.StartDate());
        Assert.Equal(new Date(19, January, 2026), d.MaturityDate());
    }

    // The same for MakeVanillaSwap: Null<Natural>() is the index's value date (makevanillaswap.cpp:65-83).
    [Fact]
    public void MakeVanillaSwapTakesQuantLibsNullSettlementDaysAsTheIndexsValueDate()
    {
        MakeVanillaSwap b = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve)).WithFixedRate(0.03);

        VanillaSwap a = b.WithEffectiveDate(new Date(15, January, 2027)).WithSettlementDays(2).WithSettlementDays(null)
            .ToVanillaSwap(Context);
        Assert.Equal(new Date(15, January, 2027), a.StartDate());
        Assert.Equal(new Date(15, January, 2032), a.MaturityDate());

        VanillaSwap d = b.WithSettlementDays(0).WithSettlementDays(null).ToVanillaSwap(Context);
        Assert.Equal(new Date(17, January, 2025), d.StartDate());
        Assert.Equal(new Date(17, January, 2030), d.MaturityDate());
    }

    // default(Date) is refused by the member given it, under its own parameter's name, as the swap builders' date
    // setters refuse it.
    [Fact]
    public void MakeSchedulesDateSettersRefuseTheDefaultDate()
    {
        var ms = new MakeSchedule();
        foreach ((string param, Func<object> call) in new (string, Func<object>)[]
        {
            ("effectiveDate", () => ms.From(default)),
            ("terminationDate", () => ms.To(default)),
            ("d", () => ms.WithFirstDate(default(Date))),
            ("d", () => ms.WithNextToLastDate(default(Date))),
        })
        {
            ArgumentException e = Assert.Throws<ArgumentException>(call);
            Assert.Equal(typeof(ArgumentException), e.GetType());
            Assert.Equal(param, e.ParamName);
            Assert.StartsWith("default(Date) is not a valid date", e.Message, StringComparison.Ordinal);
        }
    }

    // Null<Rate>() as the one cap or floor is a list holding "none": no cap, and, for an Ibor leg, no default pricer,
    // as for any cap list (cashflowvectors.hpp:192-193, iborcoupon.cpp:322, overnightindexedcoupon.cpp:670-672).
    [Fact]
    public void TheFloatingLegsTakeQuantLibsNullRateAsNoCapOrFloor()
    {
        IborLeg leg = new IborLeg(SemiAnnual(), new Euribor6M(Curve)).WithNotionals(100.0);
        ImmutableArray<CashFlow> scalar = leg.WithCaps((double?)null).ToLeg(Context);
        ImmutableArray<CashFlow> list = leg.WithCaps([null]).ToLeg(Context);
        Assert.Equal(list.Select(c => c.GetType()), scalar.Select(c => c.GetType()));
        Assert.All(scalar, c => Assert.Null(((FloatingRateCoupon)c).Pricer));
        Assert.All(leg.WithFloors((double?)null).ToLeg(Context), c => Assert.Null(((FloatingRateCoupon)c).Pricer));
        Assert.All(leg.ToLeg(Context), c => Assert.NotNull(((FloatingRateCoupon)c).Pricer));

        OvernightLeg overnight = new OvernightLeg(SemiAnnual(), new Estr(Curve)).WithNotionals(100.0);
        Assert.All(overnight.WithFloors((double?)null).ToLeg(), c => Assert.IsType<OvernightIndexedCoupon>(c));
        Assert.All(overnight.WithCaps((double?)null).ToLeg(), c => Assert.IsType<OvernightIndexedCoupon>(c));
    }

    // QuantLib's empty calendar adjusts by Unadjusted and moves by weeks, months or years without reading its
    // implementation (calendar.cpp:84-89, 130-173), so a build that uses it no other way completes: a schedule built
    // on it, which answers QuantLib's empty calendar (the wheel's fixedSchedule().calendar().empty() is true); a swap
    // whose fixed leg is built on it; an ex-coupon date moved back two weeks; and an overnight leg whose schedule has
    // it, which pays on WeekendsOnly (overnightindexedcoupon.cpp:583-586): Friday 1 May 2026, a TARGET holiday.
    [Fact]
    public void ABuildThatOnlyUsesTheEmptyCalendarUnadjustedCompletes()
    {
        var s = new Schedule(
            new Date(31, January, 2025), new Date(31, January, 2027), new Period(6, TimeUnit.Months), null,
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false,
            Context);
        Assert.Equal(
            [
                new Date(31, January, 2025), new Date(31, July, 2025), new Date(31, January, 2026),
                new Date(31, July, 2026), new Date(31, January, 2027),
            ],
            s.Dates);
        Assert.Null(s.Calendar);

        VanillaSwap vanilla = new MakeVanillaSwap(new Period(5, TimeUnit.Years), new Euribor6M(Curve)).WithFixedRate(0.03)
            .WithFixedLegCalendar(null).WithFixedLegConvention(BusinessDayConvention.Unadjusted)
            .WithFixedLegTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .WithPaymentConvention(BusinessDayConvention.Unadjusted).ToVanillaSwap(Context);
        Assert.Equal(
            [
                new Date(17, January, 2026), new Date(17, January, 2027), new Date(17, January, 2028),
                new Date(17, January, 2029), new Date(17, January, 2030),
            ],
            vanilla.FixedLeg.Select(c => c.Date));
        Assert.Null(vanilla.FixedSchedule.Calendar);

        ImmutableArray<CashFlow> exCoupon = new FixedRateLeg(Annual()).WithNotionals(100.0)
            .WithCouponRates(0.05, new Actual360())
            .WithExCouponPeriod(new Period(2, TimeUnit.Weeks), null, BusinessDayConvention.Unadjusted).ToLeg();
        Assert.Equal(
            [new Date(3, January, 2028), new Date(1, January, 2029), new Date(1, January, 2030)],
            exCoupon.Select(c => ((Coupon)c).ExCouponDate));

        OvernightIndexedSwap ois = new MakeOIS(new Period(1, TimeUnit.Years), new Estr(Curve)).WithFixedRate(0.03)
            .WithEffectiveDate(new Date(1, May, 2025)).WithOvernightLegCalendar(null).WithEndOfMonth(false)
            .WithOvernightLegConvention(BusinessDayConvention.Unadjusted)
            .WithOvernightLegTerminationDateConvention(BusinessDayConvention.Unadjusted).ToOvernightIndexedSwap(Context);
        Assert.Equal([new Date(1, May, 2026)], ois.OvernightLeg.Select(c => c.Date));
        Assert.Equal([new Date(4, May, 2026)], ois.FixedLeg.Select(c => c.Date));
        Assert.Null(ois.OvernightSchedule.Calendar);
    }

    // Any other use reads the implementation, which QuantLib refuses with its text (calendar.hpp:213, 230): an
    // adjusting convention, the end-of-month adjustment, a move in days. An empty curve is refused the same way where
    // the engine on it prices (discountingswapengine.cpp:42), and a build with a fixed rate never prices; an expired
    // swap is never priced either, and has no par rate.
    [Fact]
    public void AnAdjustedUseOfTheEmptyCalendarIsRefused()
    {
        AssertRefused(NoCalendar, () => new Schedule(
            new Date(31, January, 2025), new Date(31, January, 2027), new Period(6, TimeUnit.Months), null,
            BusinessDayConvention.Following, BusinessDayConvention.Following, DateGenerationRule.Backward, false,
            Context));
        AssertRefused(NoCalendar, () => new Schedule(
            new Date(31, January, 2025), new Date(31, January, 2027), new Period(6, TimeUnit.Months), null,
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, true,
            Context));

        var fiveYears = new Period(5, TimeUnit.Years);
        var euribor = new Euribor6M(Curve);
        AssertRefused(NoCalendar, () => new MakeVanillaSwap(fiveYears, euribor).WithFixedRate(0.03)
            .WithFixedLegCalendar(null).ToVanillaSwap(Context));
        AssertRefused(NoCalendar, () => new MakeOIS(new Period(1, TimeUnit.Years), new Estr(Curve)).WithFixedRate(0.03)
            .WithCalendar(null).ToOvernightIndexedSwap(Context));
        AssertRefused(NoCalendar, () => new FixedRateLeg(Annual()).WithNotionals(100.0)
            .WithCouponRates(0.05, new Actual360())
            .WithExCouponPeriod(new Period(5, TimeUnit.Days), null, BusinessDayConvention.Unadjusted).ToLeg());

        MakeVanillaSwap noCurve = new MakeVanillaSwap(fiveYears, euribor).WithDiscountingTermStructure(null);
        AssertRefused("discounting term structure handle is empty", () => noCurve.ToVanillaSwap(Context));
        Assert.Equal(0.03, noCurve.WithFixedRate(0.03).ToVanillaSwap(Context).FixedRate);
        AssertRefused("result not available", () => new MakeVanillaSwap(new Period(1, TimeUnit.Years), euribor)
            .WithEffectiveDate(new Date(15, January, 2020)).WithDiscountingTermStructure(null).ToVanillaSwap(Context));
    }

    // Two empty calendars are equal, and an empty calendar is no calendar (calendar.hpp:274-277): nanoql's empty
    // calendar is null, so the schedules built on it hold equal calendars, which differ from NullCalendar's, and the
    // comparisons read no name, which QuantLib's empty calendar refuses.
    [Fact]
    public void TwoEmptyCalendarsAreEqual()
    {
        OvernightIndexedSwap ois = new MakeOIS(new Period(1, TimeUnit.Years), new Estr(Curve)).WithFixedRate(0.03)
            .WithEffectiveDate(new Date(1, May, 2025)).WithCalendar(null).WithEndOfMonth(false)
            .WithConvention(BusinessDayConvention.Unadjusted).WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
            .WithPaymentAdjustment(BusinessDayConvention.Unadjusted).ToOvernightIndexedSwap(Context);
        Assert.Equal([new Date(1, May, 2026)], ois.FixedLeg.Select(c => c.Date));
        Assert.Equal([new Date(1, May, 2026)], ois.OvernightLeg.Select(c => c.Date));

        Calendar? fixedCalendar = ois.FixedSchedule.Calendar;
        Calendar? overnightCalendar = ois.OvernightSchedule.Calendar;
        Assert.True(fixedCalendar == overnightCalendar);
        Assert.False(fixedCalendar != overnightCalendar);
        Assert.Equal(fixedCalendar, overnightCalendar);
        Assert.False(fixedCalendar == new NullCalendar());
        Assert.False(new NullCalendar() == fixedCalendar);
        Assert.False(new NullCalendar().Equals(fixedCalendar));
    }

    private static Schedule Annual() => new MakeSchedule().From(new Date(15, January, 2027))
        .To(new Date(15, January, 2030)).WithFrequency(Frequency.Annual).WithCalendar(new TARGET()).ToSchedule();

    private static Schedule SemiAnnual() => new MakeSchedule().From(new Date(15, January, 2026))
        .To(new Date(15, January, 2027)).WithTenor(new Period(6, TimeUnit.Months)).WithCalendar(new TARGET()).ToSchedule();

    private static void AssertRefused(string message, Func<object> build)
    {
        NanoQLException e = Assert.Throws<NanoQLException>(build);
        Assert.Equal(message, e.Message);
    }
}
