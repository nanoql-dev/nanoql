// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Settlement days are an <c>int</c>, or an <c>int?</c> where QuantLib may leave them unset, so
/// <see cref="int.MaxValue"/> is a value. QuantLib reads its null sentinel, <c>Null&lt;Natural&gt;()</c>, which equals
/// <c>INT_MAX</c>, as "not given": a swap rate helper given it starts at the index's spot date
/// (<c>ratehelpers.cpp:573-575</c>), and an OIS rate helper at its swap builder's default spot
/// (<c>oisratehelper.cpp:167-169</c>), each 18 June 2025 from 16 June; and a swaption helper starts its swap at the
/// index's value date (<c>swaptionhelper.cpp:161-167</c>), whose settlement days' value it never reads. nanoql counts
/// that many days from the evaluation date, and the date leaves its range; its swaption helper takes the path of any
/// settlement days given.
/// </summary>
public class SettlementDaysSentinelTests
{
    [Fact]
    public void SettlementDaysOfIntMaxValueAreAValue()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };

        SwapRateHelper Helper(int? settlementDays) => new(
            0.03,
            new Period(5, TimeUnit.Years),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M(),
            context,
            settlementDays: settlementDays);

        // QuantLib's value for INT_MAX, which nanoql gives for no settlement days
        Assert.Equal(new Date(18, Month.June, 2025), Helper(null).EarliestDate);
        Assert.Throws<ArgumentOutOfRangeException>(() => Helper(int.MaxValue));
    }

    [Fact]
    public void OisSettlementDaysOfIntMaxValueAreAValue()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };

        OISRateHelper Helper(int settlementDays) =>
            new(settlementDays, new Period(1, TimeUnit.Years), 0.03, new Estr(), context);

        // QuantLib's value for INT_MAX, the swap builder's default of 2 days for ESTR
        Assert.Equal(new Date(18, Month.June, 2025), Helper(2).EarliestDate);
        Assert.Throws<ArgumentOutOfRangeException>(() => Helper(int.MaxValue));
    }

    /// <summary>
    /// A floating-rate bond's fixing days: QuantLib reads <c>fixingDays = Null&lt;Natural&gt;()</c>, its default, as the
    /// index's (<c>floatingratebond.hpp:50</c>, through the Ibor leg), so a bond given <c>INT_MAX</c> fixes its first
    /// coupon two TARGET days before its start, on 16 June 2025; one day fewer leaves the dates' range. nanoql's fixing
    /// days are an <c>int?</c>, whose <see langword="null"/> is the index's, so <see cref="int.MaxValue"/> is a value,
    /// and the fixing date leaves the range.
    /// </summary>
    [Fact]
    public void AFloatingRateBondsFixingDaysOfIntMaxValueAreAValue()
    {
        var today = new Date(16, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var index = new Euribor6M(new FlatForward(today, 0.03, new Actual365Fixed()));
        var schedule = new Schedule(
            new Date(18, Month.June, 2025), new Date(18, Month.June, 2027), new Period(6, TimeUnit.Months), new TARGET(),
            BusinessDayConvention.Following, BusinessDayConvention.Following, DateGenerationRule.Backward, false, context);

        NanoQL.Instruments.FloatingRateBond Bond(int? fixingDays) =>
            new(2, 100.0, schedule, index, new Actual360(), context, fixingDays: fixingDays);

        // QuantLib's value for INT_MAX, which nanoql gives for no fixing days
        var first = (NanoQL.Coupons.FloatingRateCoupon)Bond(null).Cashflows[0];
        Assert.Equal(new Date(16, Month.June, 2025), first.FixingDate());
        Assert.Equal(2, first.FixingDays);
        Assert.ThrowsAny<ArgumentException>(() => Bond(int.MaxValue));
    }

    /// <summary>
    /// For an exercise on Saturday 18 January 2025, QuantLib's helper with settlement days of the sentinel starts its
    /// swap at the index's value date, 22 January, as with none; with any value, 0, 2 or 5 alike, it starts the index's
    /// fixing days after the exercise, 21 January.
    /// </summary>
    [Fact]
    public void ASwaptionHelpersSettlementDaysOfIntMaxValueAreAValue()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var curve = new FlatForward(context.EvaluationDate, 0.03, new Actual365Fixed());

        Date Start(int? settlementDays) => new SwaptionHelper(
                new Date(18, Month.January, 2025),
                new Period(5, TimeUnit.Years),
                0.2,
                new Euribor6M(curve),
                new Period(1, TimeUnit.Years),
                new Thirty360(Thirty360Convention.BondBasis),
                new Actual360(),
                curve,
                context,
                settlementDays: settlementDays)
            .Underlying.FixedSchedule.Dates[0];

        // QuantLib's value for INT_MAX, which nanoql gives for no settlement days
        Assert.Equal(new Date(22, Month.January, 2025), Start(null));
        Assert.Equal(new Date(21, Month.January, 2025), Start(int.MaxValue));
        Assert.Equal(new Date(21, Month.January, 2025), Start(2));
    }
}
