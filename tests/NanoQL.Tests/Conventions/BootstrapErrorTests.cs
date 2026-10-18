// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the bootstrap, the piecewise and interpolated curves and the jumps, whose type, message and
/// <see cref="ArgumentException.ParamName"/> a vector's <c>{"throws": true}</c> can't assert (playbook §9.4). The
/// bootstrap's checks of the curve's helpers and its solver's failures are <see cref="NanoQLException"/>s with
/// QuantLib's texts; a failure inside the solver is wrapped in QuantLib's "iteration: failed at" text, which carries
/// the callee's QuantLib text without the parameter suffix .NET appends to an <see cref="ArgumentException"/>'s
/// message.
/// </summary>
public class BootstrapErrorTests
{
    private static readonly Date Today = new(16, Month.June, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter Act365 = new Actual365Fixed();

    [Fact]
    public void TheBootstrapsRefusals()
    {
        Assert.Equal(
            "no bootstrap helpers given",
            Assert.Throws<NanoQLException>(() => Curve([])).Message);
        Assert.Equal(
            "all instruments expired",
            Assert.Throws<NanoQLException>(
                () => new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                    new Date(16, Month.June, 2026), [Deposit(0.03)], Act365, Context)).Message);
        Assert.Equal(
            "not enough alive instruments: 1 provided, 2 required",
            Assert.Throws<NanoQLException>(
                () => new PiecewiseYieldCurve<Discount, ThreePoints, IterativeBootstrap>(
                    Today, [Deposit(0.03)], Act365, Context)).Message);
        Assert.Equal(
            "more than one instrument with pillar December 18th, 2025",
            Assert.Throws<NanoQLException>(() => Curve([Deposit(0.03), Deposit(0.031)])).Message);
        Assert.Equal(
            "3rd instrument (pillar: June 18th, 2027) has latestRelevantDate (June 18th, 2027) before or equal to "
            + "previous instrument's latestRelevantDate (June 18th, 2030)",
            Assert.Throws<NanoQLException>(
                () => Curve([Deposit(0.03), Swap(2, 0.031), Swap(5, 0.032, new Date(18, Month.March, 2027))])).Message);
        Assert.StartsWith(
            "1st iteration: failed at 1st alive instrument, pillar December 18th, 2025, maturity December 18th, 2025, "
            + "reference date June 16th, 2025: maximum number of function evaluations (1) exceeded",
            Assert.Throws<NanoQLException>(
                () => Curve([Deposit(0.03)], new IterativeBootstrap(maxEvaluations: 1))).Message,
            StringComparison.Ordinal);

        // the bootstrap's own arguments
        Assert.Equal(
            "Expected that maxFactor would be at least 1.0 but got 0.5 (Parameter 'maxFactor')",
            Assert.Throws<ArgumentException>(() => new IterativeBootstrap(maxFactor: 0.5)).Message);
        Assert.Equal(
            "minFactor",
            Assert.Throws<ArgumentException>(() => new IterativeBootstrap(minFactor: 0.5)).ParamName);
        Assert.Equal(
            "maxAttempts",
            Assert.Throws<ArgumentOutOfRangeException>(() => new IterativeBootstrap(maxAttempts: -1)).ParamName);
        Assert.Equal(
            "dontThrowSteps",
            Assert.Throws<ArgumentOutOfRangeException>(() => new IterativeBootstrap(dontThrowSteps: -1)).ParamName);
        Assert.Equal(
            "maxEvaluations",
            Assert.Throws<ArgumentOutOfRangeException>(() => new IterativeBootstrap(maxEvaluations: -1)).ParamName);

        // the curves' arguments
        Assert.Equal(
            "context",
            Assert.Throws<ArgumentNullException>(
                () => new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(Today, [Deposit(0.03)], Act365, null!))
                .ParamName);
        Assert.Equal(
            "dayCounter",
            Assert.Throws<ArgumentNullException>(
                () => new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(Today, [Deposit(0.03)], null!, Context))
                .ParamName);
        Assert.Equal(
            "calendar",
            Assert.Throws<ArgumentNullException>(
                () => PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>.Create(
                    2, null!, [Deposit(0.03)], Act365, Context)).ParamName);
        Assert.Equal(
            "settlementDays",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>.Create(
                    -1, new TARGET(), [Deposit(0.03)], Act365, Context)).ParamName);

        // the jumps
        Assert.Equal(
            "mismatch between number of jumps (1) and jump dates (2) (Parameter 'jumpDates')",
            Assert.Throws<ArgumentException>(() => Discounts(
                [0.99], [new Date(31, Month.December, 2025), new Date(31, Month.December, 2026)])).Message);
        Assert.Equal(
            "invalid 1st jump value: -0.5",
            Assert.Throws<NanoQLException>(() => Discounts([-0.5], [new Date(31, Month.December, 2025)]).Discount(1.0))
                .Message);

        // the interpolated discount curve's, in QuantLib's order
        Assert.Equal(
            "not enough input dates given (Parameter 'dates')",
            Assert.Throws<ArgumentException>(
                () => new InterpolatedDiscountCurve<LogLinear>([], [], Act365)).Message);
        Assert.Equal(
            "not enough input dates given (Parameter 'dates')",
            Assert.Throws<ArgumentException>(
                () => new InterpolatedDiscountCurve<LogLinear>([Today], [1.0], Act365)).Message);
        Assert.Equal(
            "dates/data count mismatch (Parameter 'dfs')",
            Assert.Throws<ArgumentException>(
                () => new InterpolatedDiscountCurve<LogLinear>([Today, Today + 365], [1.0], Act365)).Message);
        Assert.Equal(
            "the first discount must be == 1.0 to flag the corresponding date as reference date (Parameter 'dfs')",
            Assert.Throws<ArgumentException>(
                () => new InterpolatedDiscountCurve<LogLinear>([Today, Today + 365], [0.99, 0.95], Act365)).Message);
        Assert.Equal(
            "negative discount (Parameter 'dfs')",
            Assert.Throws<ArgumentException>(
                () => new InterpolatedDiscountCurve<LogLinear>([Today, Today + 365], [1.0, -0.95], Act365)).Message);
        Assert.Equal(
            "dates not sorted: June 16th, 2025 passed after June 16th, 2026 (Parameter 'dates')",
            Assert.Throws<ArgumentException>(
                () => new InterpolatedDiscountCurve<LogLinear>([Today, Today + 365, Today], [1.0, 0.95, 0.9], Act365))
                .Message);
    }

    [Fact]
    public void AFailureInsideTheSolverIsWrappedWithItsQuantLibText()
    {
        // the log interpolation refuses the negative trial value with ArgumentException "y"; QuantLib's what() has no
        // parameter suffix
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => Curve(
                [Deposit(0.03), Swap(2, 0.031), Swap(5, 0.032)],
                new IterativeBootstrap(minValue: -0.1)));
        Assert.Equal(
            "1st iteration: failed at 1st alive instrument, pillar December 18th, 2025, maturity December 18th, 2025, "
            + "reference date June 16th, 2025: invalid value (-0.1) at index 1",
            e.Message);
        Assert.IsType<ArgumentException>(e.InnerException);
    }

    private static PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap> Curve(
        List<RateHelper> helpers, IterativeBootstrap bootstrap = default) =>
        new(Today, helpers, Act365, Context, bootstrap: bootstrap);

    private static InterpolatedDiscountCurve<LogLinear> Discounts(double[] jumps, Date[] jumpDates) =>
        new([Today, new Date(15, Month.June, 2030)], [1.0, 0.85], Act365, null, jumps, jumpDates);

    // An interpolation factory of one's own needing three points, which no interpolator ported so far does.
    private readonly struct ThreePoints : IInterpolator
    {
        public static bool Global => false;

        public static int RequiredPoints => 3;

        public Interpolation Interpolate(ReadOnlySpan<double> x, ReadOnlySpan<double> y) => default(Linear).Interpolate(x, y);
    }

    private static DepositRateHelper Deposit(double rate) =>
        new(rate, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
            new Actual360(), Context);

    private static SwapRateHelper Swap(int years, double rate, Date? customPillarDate = null) =>
        new(
            rate,
            new Period(years, TimeUnit.Years),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M(),
            Context,
            pillar: customPillarDate is null ? PillarChoice.LastRelevantDate : PillarChoice.CustomDate,
            customPillarDate: customPillarDate);
}
