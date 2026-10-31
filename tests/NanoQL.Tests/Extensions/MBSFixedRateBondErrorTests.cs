// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Extensions;

/// <summary><see cref="MBSFixedRateBond"/>'s refusals: each exception's type, message and <c>ParamName</c>.</summary>
public class MBSFixedRateBondErrorTests
{
    private static readonly Date Start = new(1, February, 2007);

    [Fact]
    public void ANullDayCounterIsRefused()
    {
        var e = Assert.Throws<ArgumentNullException>(() => Pool(nullDayCounter: true));
        Assert.Equal("accrualDayCounter", e.ParamName);
    }

    [Fact]
    public void AConventionOutsideItsTypeIsRefused()
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => Pool(convention: (BusinessDayConvention)99));
        Assert.Equal("paymentConvention", e.ParamName);
        Assert.StartsWith("not a business-day convention", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QuantLibsEmptyCalendarTakesOnlyUnadjustedPayments()
    {
        var e = Assert.Throws<NanoQLException>(() => Pool(BusinessDayConvention.Following, emptyCalendar: true));
        Assert.Equal("no calendar implementation provided", e.Message);
        Assert.Equal(12, Pool(emptyCalendar: true).ExpectedCashflows.Length / 3);
    }

    [Fact]
    public void APaidOffPoolsYieldsAreRefused()
    {
        MBSFixedRateBond pool = Pool(model: new ConstantCPR(1.0));
        var settlement = new Date(1, June, 2007);
        var context = new EvaluationContext { EvaluationDate = settlement };
        string message = $"non tradable at {settlement} (maturity being {pool.MaturityDate()})";

        var monthly = Assert.Throws<ArgumentException>(() => pool.MonthlyYield(context));
        Assert.Equal("context", monthly.ParamName);
        Assert.StartsWith(message, monthly.Message, StringComparison.Ordinal);
        var bey = Assert.Throws<ArgumentException>(() => pool.BondEquivalentYield(context));
        Assert.Equal("context", bey.ParamName);
        var yield = Assert.Throws<ArgumentException>(() => pool.Yield(100, 14, context, settlement));
        Assert.Equal("settlementDate", yield.ParamName);
        Assert.StartsWith(message, yield.Message, StringComparison.Ordinal);
        var price = Assert.Throws<ArgumentException>(() => pool.Price(0.05, 14, context, settlement));
        Assert.Equal("settlementDate", price.ParamName);
    }

    [Fact]
    public void ANegativeDelayIsRefused()
    {
        MBSFixedRateBond pool = Pool();
        var context = new EvaluationContext { EvaluationDate = Start };
        var yield = Assert.Throws<ArgumentOutOfRangeException>(() => pool.Yield(100, -1, context));
        var price = Assert.Throws<ArgumentOutOfRangeException>(() => pool.Price(0.05, -1, context));
        Assert.Equal("delayDays", yield.ParamName);
        Assert.Equal("delayDays", price.ParamName);
    }

    private static MBSFixedRateBond Pool(
        BusinessDayConvention convention = BusinessDayConvention.Unadjusted,
        IPrepayModel? model = null,
        bool emptyCalendar = false,
        bool nullDayCounter = false) =>
        new(0, emptyCalendar ? null : new NullCalendar(), 1000000, Start, new Period(12, TimeUnit.Months),
            new Period(12, TimeUnit.Months), Frequency.Monthly, 0.05, 0.045,
            nullDayCounter ? null! : new Thirty360(Thirty360Convention.BondBasis), model ?? new ConstantCPR(0.06),
            convention);
}
