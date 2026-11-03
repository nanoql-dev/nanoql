// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The swap indexes' refusals, whose type, message and <see cref="ArgumentException.ParamName"/> a vector's
/// <c>{"throws": true}</c> can't assert (playbook §9.4); and the index QuantLib's constructor taking a discounting
/// curve builds on its empty handle: its discounting is exogenous, so it builds, and its forecast, the par rate of
/// its swap, refuses the missing curve, as QuantLib's does.
/// </summary>
public class SwapIndexErrorTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly DayCounter Thirty360 = new Thirty360(Thirty360Convention.BondBasis);
    private static readonly Date Fixing = new(16, Month.January, 2025);

    [Fact]
    public void AnExogenousIndexOnANullCurveBuildsAndRefusesAParBuild()
    {
        SwapIndex exogenous = Index(new Euribor6M(Curve), exogenous: true);
        Assert.True(exogenous.ExogenousDiscount);
        Assert.Null(exogenous.DiscountingTermStructure);
        Assert.Same(Curve, exogenous.ForwardingTermStructure);
        Assert.Equal(
            "discounting term structure handle is empty",
            Assert.Throws<NanoQLException>(() => exogenous.ForecastFixing(Fixing, Context)).Message);
        Assert.Equal(
            "discounting term structure handle is empty",
            Assert.Throws<NanoQLException>(() => exogenous.Fixing(Fixing, Context)).Message);

        // Its clones on a forwarding curve and with another tenor keep the exogenous empty handle.
        Assert.True(exogenous.Clone(Curve).ExogenousDiscount);
        Assert.Throws<NanoQLException>(() => exogenous.Clone(Curve).Fixing(Fixing, Context));
        Assert.Throws<NanoQLException>(() => exogenous.Clone(new Period(2, TimeUnit.Years)).Fixing(Fixing, Context));

        // The preset's constructor taking a discounting curve is exogenous on a null one too.
        var preset = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), Curve, null);
        Assert.True(preset.ExogenousDiscount);
        Assert.Throws<NanoQLException>(() => preset.Fixing(Fixing, Context));

        // The constructor without a discounting curve discounts on the forwarding curve.
        SwapIndex endogenous = Index(new Euribor6M(Curve), exogenous: false);
        Assert.False(endogenous.ExogenousDiscount);
        Assert.True(endogenous.ForecastFixing(Fixing, Context) > 0.0);
        Assert.False(new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), Curve).ExogenousDiscount);
    }

    [Fact]
    public void TheSwapIndexesRefusals()
    {
        // the undefined paths' refusals, .NET's text for a null argument
        Assert.Equal(
            new ArgumentNullException("iborIndex").Message,
            AssertRefusal<ArgumentNullException>("iborIndex", () => Index(null!, exogenous: false)).Message);
        Assert.Equal(
            new ArgumentNullException("overnightIndex").Message,
            AssertRefusal<ArgumentNullException>(
                "overnightIndex",
                () => new OvernightIndexedSwapIndex("EstrSwap", new Period(1, TimeUnit.Years), 2, null, null!)).Message);

        // the constructors' own arguments
        AssertRefusal<ArgumentOutOfRangeException>("settlementDays", () => new SwapIndex(
            "EurSwap",
            new Period(5, TimeUnit.Years),
            -1,
            null,
            new TARGET(),
            new Period(1, TimeUnit.Years),
            BusinessDayConvention.ModifiedFollowing,
            Thirty360,
            new Euribor6M(Curve)));
        Assert.StartsWith(
            "unknown business-day convention",
            AssertRefusal<ArgumentOutOfRangeException>("fixedLegConvention", () => new SwapIndex(
                "EurSwap",
                new Period(5, TimeUnit.Years),
                2,
                null,
                new TARGET(),
                new Period(1, TimeUnit.Years),
                (BusinessDayConvention)42,
                Thirty360,
                new Euribor6M(Curve))).Message,
            StringComparison.Ordinal);
        AssertRefusal<ArgumentOutOfRangeException>("averagingMethod", () => new OvernightIndexedSwapIndex(
            "EstrSwap", new Period(1, TimeUnit.Years), 2, null, new Estr(Curve), false, (RateAveragingType)7));

        // the swap's fixing date
        SwapIndex index = Index(new Euribor6M(Curve), exogenous: false);
        AssertRefusal<ArgumentNullException>("context", () => index.UnderlyingSwap(Fixing, null!));
        AssertRefusal<ArgumentException>("fixingDate", () => index.UnderlyingSwap(default, Context));
        Assert.StartsWith(
            "January 18th, 2025 is not a valid fixing date",
            AssertRefusal<ArgumentException>(
                "fixingDate", () => index.UnderlyingSwap(new Date(18, Month.January, 2025), Context)).Message,
            StringComparison.Ordinal);
        AssertRefusal<ArgumentException>("valueDate", () => index.MaturityDate(default));

        // a forecast with no curve to discount on, and an expired swap's, whose fair rate isn't available
        Assert.Equal(
            "discounting term structure handle is empty",
            Assert.Throws<NanoQLException>(
                () => Index(new Euribor6M(), exogenous: false).ForecastFixing(Fixing, Context)).Message);
        Assert.Equal(
            "result not available",
            Assert.Throws<NanoQLException>(() => Index(new Euribor6M(Curve), exogenous: false)
                .Clone(new Period(1, TimeUnit.Years))
                .ForecastFixing(new Date(16, Month.January, 2023), Context)).Message);

        // a preset's tenor that QuantLib can't compare with a year
        Assert.Throws<NanoQLException>(() => new EuriborSwapIsdaFixB(new Period(365, TimeUnit.Days)));

        // the swap helper on a swap index: the context first, then the index; a fixed-leg tenor with no frequency is
        // refused by the constructor it delegates to, as raised
        AssertRefusal<ArgumentNullException>("context", () => new SwapRateHelper(0.03, (SwapIndex)null!, null!));
        AssertRefusal<ArgumentNullException>("swapIndex", () => new SwapRateHelper(0.03, (SwapIndex)null!, Context));
        var thirteenMonths = new SwapIndex(
            "EurSwap",
            new Period(5, TimeUnit.Years),
            2,
            null,
            new TARGET(),
            new Period(13, TimeUnit.Months),
            BusinessDayConvention.ModifiedFollowing,
            Thirty360,
            new Euribor6M(Curve));
        AssertRefusal<ArgumentOutOfRangeException>(
            "fixedFrequency", () => new SwapRateHelper(0.03, thirteenMonths, Context));
    }

    private static SwapIndex Index(IborIndex iborIndex, bool exogenous) =>
        exogenous
            ? new SwapIndex(
                "EurSwap",
                new Period(5, TimeUnit.Years),
                2,
                null,
                new TARGET(),
                new Period(1, TimeUnit.Years),
                BusinessDayConvention.ModifiedFollowing,
                Thirty360,
                iborIndex,
                null)
            : new SwapIndex(
                "EurSwap",
                new Period(5, TimeUnit.Years),
                2,
                null,
                new TARGET(),
                new Period(1, TimeUnit.Years),
                BusinessDayConvention.ModifiedFollowing,
                Thirty360,
                iborIndex);

    private static T AssertRefusal<T>(string paramName, Func<object> build)
        where T : ArgumentException
    {
        T e = Assert.Throws<T>(build);
        Assert.Equal(paramName, e.ParamName);
        return e;
    }
}
