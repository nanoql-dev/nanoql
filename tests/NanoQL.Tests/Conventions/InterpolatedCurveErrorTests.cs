// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the interpolated zero, forward and simple-zero curves, in QuantLib's order, whose type, message and
/// <see cref="ArgumentException.ParamName"/> a vector's <c>{"throws": true}</c> can't assert (playbook §9.4): a refused
/// argument throws <see cref="ArgumentException"/> naming the curve's own parameter, the zero curve's compounding and
/// frequency included; a callee's refusal of a value the curve computed (a compound factor, the interpolation's
/// values) reaches the caller as raised; and a compounding QuantLib can't convert directly throws
/// <see cref="NanoQLException"/>. A named curve on an interpolator QuantLib can't default-construct refuses its zero
/// value with <see cref="InvalidOperationException"/>.
/// </summary>
public class InterpolatedCurveErrorTests
{
    private static readonly Date Today = new(15, Month.January, 2024);
    private static readonly DayCounter Act365 = new Actual365Fixed();
    private static readonly Date[] Three = [Today, Today + 182, Today + 365];

    [Fact]
    public void TheZeroCurvesRefusals()
    {
        double[] rates = [0.03, 0.031, 0.032];
        AssertNull("dates", () => new ZeroCurve(null!, rates, Act365));
        AssertNull("yields", () => new ZeroCurve(Three, null!, Act365));
        AssertNull("dayCounter", () => new ZeroCurve(Three, rates, null!));
        AssertArgument("dates", "not enough input dates given", () => new ZeroCurve([], [], Act365));
        AssertArgument("dates", "not enough input dates given", () => new ZeroCurve([Today], [0.03], Act365));
        AssertArgument("yields", "dates/data count mismatch", () => new ZeroCurve(Three, rates[..2], Act365));
        AssertArgument(
            "dates",
            "dates not sorted: July 15th, 2024 passed after January 14th, 2025",
            () => new ZeroCurve([Today, Today + 365, Today + 182], rates, Act365));
        AssertArgument(
            "jumpDates",
            "mismatch between number of jumps (2) and jump dates (1)",
            () => new ZeroCurve(Three, rates, Act365, null, [0.999, 0.998], [Today + 90]));

        // the compounding, checked on entry, and the frequency, checked only where QuantLib reads it
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ZeroCurve(Three, rates, Act365, compounding: (Compounding)9));
        Assert.Equal("compounding", e.ParamName);
        Assert.StartsWith("unknown compounding convention", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ZeroCurve(Three, rates, Act365, compounding: Compounding.Compounded, frequency: (Frequency)7));
        Assert.Equal("frequency", e.ParamName);
        Assert.Equal(
            rates, new ZeroCurve(Three, rates, Act365, compounding: Compounding.Continuous, frequency: (Frequency)7).Data());
        AssertArgument(
            "frequency",
            "frequency not allowed for this interest rate",
            () => new ZeroCurve(Three, rates, Act365, compounding: Compounding.Compounded, frequency: Frequency.NoFrequency));
        AssertArgument(
            "frequency",
            "frequency not allowed for this interest rate",
            () => new ZeroCurve(Three, rates, Act365, compounding: Compounding.SimpleThenCompounded, frequency: Frequency.Once));
        Assert.Equal(
            "SimpleThenCompounded is not supported for direct calculations: use the CashFlow methods",
            Assert.Throws<NanoQLException>(
                () => new ZeroCurve(Three, rates, Act365, compounding: Compounding.SimpleThenCompounded)).Message);
        AssertArgument(
            "compound",
            "positive compound factor required",
            () => new ZeroCurve(Three, [0.03, -2.5, 0.032], Act365, compounding: Compounding.Simple));
        AssertArgument(
            "y",
            "invalid value (-0.01) at index 1",
            () => new InterpolatedZeroCurve<LogLinear>(Three, [0.03, -0.01, 0.032], Act365));

        // QuantLib's order: the count before the conversion, the dates' order before the frequency
        AssertArgument(
            "yields",
            "dates/data count mismatch",
            () => new ZeroCurve(Three, rates[..2], Act365, compounding: Compounding.Compounded, frequency: Frequency.NoFrequency));
        AssertArgument(
            "dates",
            "dates not sorted",
            () => new ZeroCurve(
                [Today, Today + 365, Today + 182], rates, Act365, compounding: Compounding.Compounded, frequency: Frequency.Once));
    }

    [Fact]
    public void TheForwardAndSimpleZeroCurvesRefusals()
    {
        double[] rates = [0.03, 0.031, 0.032];
        AssertNull("forwards", () => new ForwardCurve(Three, null!, Act365));
        AssertArgument("dates", "not enough input dates given", () => new LinearForwardCurve([Today], [0.03], Act365));
        Assert.Equal([0.03], new ForwardCurve([Today], [0.03], Act365).Forwards());
        AssertArgument("forwards", "dates/data count mismatch", () => new ForwardCurve(Three, rates[..2], Act365));
        AssertArgument(
            "dates",
            "two passed dates (January 30th, 2025 and January 31st, 2025) correspond to the same time",
            () => new CubicForwardCurve(
                [new Date(30, Month.January, 2024), new Date(30, Month.January, 2025), new Date(31, Month.January, 2025)],
                rates,
                new Thirty360(Thirty360Convention.BondBasis)));

        AssertNull("yields", () => new SimpleZeroCurve(Three, null!, Act365));
        AssertArgument("dates", "not enough input dates given", () => new SimpleZeroCurve([], [], Act365));
        AssertArgument("yields", "dates/data count mismatch", () => new SimpleZeroCurve(Three, rates[..2], Act365));
        AssertArgument(
            "jumpDates",
            "mismatch between number of jumps (1) and jump dates (2)",
            () => new SimpleZeroCurve(Three, rates, Act365, null, [0.999], [Today + 30, Today + 60]));
    }

    [Fact]
    public void ANamedCurveRefusesAnInterpolatorsZeroValue()
    {
        Assert.Throws<InvalidOperationException>(() => new LogCubicDiscountCurve(Three, [1.0, 0.99, 0.97], Act365));
        Assert.Throws<InvalidOperationException>(
            () => new LogMixedLinearCubicDiscountCurve(Three, [1.0, 0.99, 0.97], Act365));

        var context = new EvaluationContext { EvaluationDate = Today };
        var helpers = new List<RateHelper>
        {
            new DepositRateHelper(
                0.030, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), context),
            new DepositRateHelper(
                0.031, new Period(12, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), context),
        };
        Assert.Throws<InvalidOperationException>(() => new PiecewiseLogCubicDiscount(Today, helpers, Act365, context));
        Assert.Throws<InvalidOperationException>(
            () => PiecewiseLogMixedLinearCubicDiscount.Create(2, new TARGET(), helpers, Act365, context));
    }

    private static void AssertNull(string paramName, Action action) =>
        Assert.Equal(paramName, Assert.Throws<ArgumentNullException>(action).ParamName);

    private static void AssertArgument(string paramName, string message, Action action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
