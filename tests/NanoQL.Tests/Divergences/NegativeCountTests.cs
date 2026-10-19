// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's fixing and settlement days are <c>Natural</c>, an unsigned type, which can't hold a negative value; nanoql
/// takes an <see cref="int"/> and refuses a negative one on entry. From C++ a negative wraps to a large unsigned value,
/// which QuantLib negates back where it advances by it (<c>interestrateindex.hpp:43, 106</c>, <c>termstructure.cpp:42</c>),
/// and the wrap of 2^31 negates <c>INT_MIN</c>, which is undefined. The cash-flow solvers' maximum numbers of iterations
/// are <c>Size</c>, unsigned too (<c>cashflows.hpp:273, 436</c>), where a negative wraps to a count no solve reaches;
/// nanoql refuses it on entry likewise.
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

    [Fact]
    public void NegativeSettlementDaysOfAVolatilityStructureAreRefused()
    {
        var calendar = new TARGET();
        var dc = new Actual365Fixed();
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2024) };

        // QuantLib's volatility bases take their settlement days as a Natural (voltermstructure.hpp:55,
        // optionletvolatilitystructure.hpp:62, constantoptionletvol.hpp:39, 55) and advance by it in
        // termstructure.cpp:42.
        AssertRefused(() => ConstantOptionletVolatility.Create(-1, calendar, BusinessDayConvention.Following, 0.2, dc, context));
        AssertRefused(() => ConstantOptionletVolatility.Create(int.MinValue, calendar, BusinessDayConvention.Following, 0.2, dc, context));
        AssertRefused(() => new TestVolatility(-1));

        // Zero stays QuantLib's.
        Assert.Equal(
            0,
            ConstantOptionletVolatility.Create(0, calendar, BusinessDayConvention.Following, 0.2, dc, context).SettlementDays);
        Assert.Equal(0, new TestVolatility(0).SettlementDays);
    }

    [Fact]
    public void NegativeMaximumIterationsOfTheCashFlowSolversAreRefused()
    {
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        var dc = new Actual365Fixed();
        ImmutableArray<CashFlow> leg = [new Redemption(100.0, today + 365)];
        var curve = new FlatForward(today, 0.03, dc);

        foreach (Func<object> solve in new Func<object>[]
        {
            () => CashFlows.Yield(leg, 97.0, dc, Compounding.Compounded, Frequency.Annual, context, maxIterations: -1),
            () => CashFlows.Yield(leg, 97.0, dc, Compounding.Compounded, Frequency.Annual, context, maxIterations: int.MinValue),
            () => CashFlows.ZSpread(leg, 97.0, curve, Compounding.Continuous, Frequency.Annual, context, maxIterations: -1),
        })
        {
            Assert.Equal("maxIterations", Assert.Throws<ArgumentOutOfRangeException>(solve).ParamName);
        }

        // Zero stays QuantLib's: no evaluation is allowed, and the solver can't bracket.
        Assert.Throws<NanoQLException>(
            () => CashFlows.Yield(leg, 97.0, dc, Compounding.Compounded, Frequency.Annual, context, maxIterations: 0));
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

    private sealed class TestVolatility(int settlementDays)
        : OptionletVolatilityStructure(
            new Date(15, Month.January, 2024), new TARGET(), BusinessDayConvention.Following, new Actual365Fixed(), settlementDays)
    {
        public override Date MaxDate => Date.MaxDate;

        public override double MinStrike() => double.MinValue;

        public override double MaxStrike() => double.MaxValue;

        protected override SmileSection SmileSectionImpl(double optionTime) => new FlatSmileSection(optionTime, 0.2, DayCounter);

        protected override double VolatilityImpl(double optionTime, double strike) => 0.2;
    }
}
