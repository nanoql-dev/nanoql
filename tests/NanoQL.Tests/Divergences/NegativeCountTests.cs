// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's fixing and settlement days are <c>Natural</c>, an unsigned type, which can't hold a negative value; nanoql
/// takes an <see cref="int"/> and refuses a negative one on entry. From C++ a negative wraps to a large unsigned value,
/// which QuantLib negates back where it advances by it (<c>interestrateindex.hpp:43, 106</c>, <c>termstructure.cpp:42</c>),
/// and the wrap of 2^31 negates <c>INT_MIN</c>, which is undefined.
/// </summary>
public class NegativeCountTests
{
    [Fact]
    public void NegativeFixingAndSettlementDaysAreRefused()
    {
        var calendar = new TARGET();
        var dc = new Actual360();
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2024) };
        var sixMonths = new Period(6, TimeUnit.Months);

        AssertRefused(() => new IborIndex("Euribor", sixMonths, -1, null, calendar, BusinessDayConvention.ModifiedFollowing, true, dc));
        AssertRefused(() => new OvernightIndex("ESTR", -1, null, calendar, dc));
        AssertRefused(() => new TestRateIndex(-1));
        AssertRefused(() => FlatForward.Create(-1, calendar, 0.03, dc, context));
        AssertRefused(() => new TestCurve(-1));

        // Zero stays QuantLib's.
        Assert.Equal(0, new OvernightIndex("ESTR", 0, null, calendar, dc).FixingDays);
        Assert.Equal(0, new TestCurve(0).SettlementDays);
    }

    private static void AssertRefused(Func<object> create) =>
        Assert.Equal("settlementDays", Assert.Throws<ArgumentOutOfRangeException>(create).ParamName);

    private sealed class TestRateIndex(int settlementDays)
        : InterestRateIndex("Test", new Period(3, TimeUnit.Months), settlementDays, null, new TARGET(), new Actual360())
    {
        public override Date MaturityDate(Date valueDate) => valueDate + Tenor;

        public override double ForecastFixing(Date fixingDate, EvaluationContext context) => 0.0;
    }

    private sealed class TestCurve(int settlementDays)
        : YieldTermStructure(new Date(15, Month.January, 2024), new TARGET(), new Actual360(), settlementDays)
    {
        public override Date MaxDate => Date.MaxDate;

        protected override double DiscountImpl(double t) => 1.0;
    }
}
