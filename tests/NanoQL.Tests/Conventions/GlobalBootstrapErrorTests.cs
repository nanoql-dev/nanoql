// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the global bootstrap, the multicurve and its members, whose type, message and
/// <see cref="ArgumentException.ParamName"/> a vector's <c>{"throws": true}</c> can't assert (playbook §9.4). A
/// bootstrap's and a multicurve's checks of what they were given when a curve is built are
/// <see cref="NanoQLException"/>s with QuantLib's texts, the members' with nanoql's; a refusal of a member's own
/// argument names it. An additional helper of another kind of term structure fails .NET's cast, a type argument's
/// failure, as a wrong result type does.
/// </summary>
public class GlobalBootstrapErrorTests
{
    private static readonly Date Today = new(15, Month.January, 2024);
    private static readonly Date Spot = new(17, Month.January, 2024);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter Act365 = new Actual365Fixed();

    [Fact]
    public void TheGlobalBootstrapsRefusals()
    {
        // its own arguments
        Assert.Equal(
            "additionalHelpers",
            Assert.Throws<ArgumentException>(() => new GlobalBootstrap(additionalHelpers: [Deposit(), null!])).ParamName);
        Assert.Equal(
            "additionalDates",
            Assert.Throws<ArgumentException>(() => new GlobalBootstrap(additionalDates: [Spot, default])).ParamName);

        // the curve's, when it is built
        Assert.Equal(
            "GlobalBootstrap: number of instrument weights (3) must match number of instruments (4)",
            Assert.Throws<NanoQLException>(() => Curve(new GlobalBootstrap(instrumentWeights: [1.0, 2.0, 3.0]))).Message);
        Assert.Equal(
            "GlobalBootstrap: not enough curve points (1) for interpolation requiring at least 2",
            Assert.Throws<NanoQLException>(
                () => new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(Spot, [], Act365, Context)).Message);
        Assert.Equal(
            "initial guess has 2 values but the curve has 4 pillars",
            Assert.Throws<NanoQLException>(
                () => Curve(new GlobalBootstrap(initialGuessFn: (_, _) => new Vector(2, 0.9)))).Message);
        Assert.Equal(
            "global bootstrap failed to minimize to required accuracy: MaxIterations",
            Assert.Throws<NanoQLException>(
                () => Curve(new GlobalBootstrap(endCriteria: new EndCriteria(3, 2, 1e-15, 1e-15, 1e-15)))).Message);
        Assert.Equal(
            "less functions (4) than available variables (6)",
            Assert.Throws<NanoQLException>(
                () => Curve(new GlobalBootstrap(additionalDates: [Spot + 45, Spot + 100]))).Message);
        Assert.Throws<InvalidCastException>(() => Curve(new GlobalBootstrap(additionalHelpers: [new OtherHelper()])));
    }

    [Fact]
    public void TheMultiCurvesRefusals()
    {
        var member = new MultiCurveMember(Spot, Act365);
        var flat = new FlatForward(Spot, 0.03, Act365);
        var multiCurve = new MultiCurve(1e-10);

        // the members' own arguments
        Assert.Equal(
            "internalHandle",
            Assert.Throws<ArgumentNullException>(() => multiCurve.AddNonBootstrappedCurve(null!, flat)).ParamName);
        ArgumentNullException nullCurve = Assert.Throws<ArgumentNullException>(
            () => multiCurve.AddNonBootstrappedCurve(member, null!));
        Assert.Equal("curve", nullCurve.ParamName);
        Assert.Equal("curve must not be null (Parameter 'curve')", nullCurve.Message);
        Assert.Equal(
            "instruments",
            Assert.Throws<ArgumentNullException>(
                () => multiCurve.AddBootstrappedCurve<Discount, LogLinear>(member, Spot, null!, Act365)).ParamName);
        Assert.Equal(
            "instruments",
            Assert.Throws<ArgumentException>(
                () => multiCurve.AddBootstrappedCurve<Discount, LogLinear>(member, Spot, [Deposit(), null!], Act365))
                .ParamName);
        Assert.Equal(
            "dayCounter",
            Assert.Throws<ArgumentNullException>(
                () => multiCurve.AddBootstrappedCurve<Discount, LogLinear>(member, Spot, [Deposit()], null!)).ParamName);
        Assert.Equal(
            "settlementDays",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => multiCurve.AddBootstrappedCurve<Discount, LogLinear>(member, -1, new TARGET(), [Deposit()], Act365))
                .ParamName);
        Assert.Equal(
            "calendar",
            Assert.Throws<ArgumentNullException>(
                () => multiCurve.AddBootstrappedCurve<Discount, LogLinear>(member, 2, null!, [Deposit()], Act365))
                .ParamName);

        // a member added already, through a copy of it
        MultiCurve one = multiCurve.AddNonBootstrappedCurve(member, flat);
        ArgumentException twice = Assert.Throws<ArgumentException>(
            () => one.AddNonBootstrappedCurve(member.EnableExtrapolation(), flat));
        Assert.Equal("internalHandle", twice.ParamName);
        Assert.Equal(
            "internal handle must be empty; was the curve added already? (Parameter 'internalHandle')", twice.Message);

        // Build's
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => one.Build(null!)).ParamName);
        _ = one.Build(Context);
        Assert.Equal(
            "internal handle must be empty; was the curve added already?",
            Assert.Throws<NanoQLException>(() => one.Build(Context)).Message);
    }

    [Fact]
    public void TheMembersRefusals()
    {
        Assert.Equal(
            "dayCounter",
            Assert.Throws<ArgumentNullException>(() => new MultiCurveMember(Spot, null!)).ParamName);
        Assert.Equal(
            "referenceDate",
            Assert.Throws<ArgumentException>(() => new MultiCurveMember(default, Act365)).ParamName);
        Assert.Equal(
            "context",
            Assert.Throws<ArgumentNullException>(() => MultiCurveMember.Create(2, new TARGET(), Act365, null!))
                .ParamName);
        Assert.Equal(
            "settlementDays",
            Assert.Throws<ArgumentOutOfRangeException>(() => MultiCurveMember.Create(-1, new TARGET(), Act365, Context))
                .ParamName);
        Assert.Equal(
            "calendar",
            Assert.Throws<ArgumentNullException>(() => MultiCurveMember.Create(2, null!, Act365, Context)).ParamName);
        Assert.Equal(
            "dayCounter",
            Assert.Throws<ArgumentNullException>(() => MultiCurveMember.Create(2, new TARGET(), null!, Context))
                .ParamName);
        Assert.IsType<NanoQLException>(
            Assert.ThrowsAny<Exception>(() => new MultiCurveMember(Spot, Act365).Discount(1.0)));
    }

    private static PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> Curve(GlobalBootstrap bootstrap) =>
        new(Spot, [Deposit(), Swap(2, 0.031), Swap(5, 0.032), Swap(10, 0.033)], Act365, Context, bootstrap: bootstrap);

    private static DepositRateHelper Deposit() =>
        new(0.030, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
            new Actual360(), Context);

    private static SwapRateHelper Swap(int years, double rate) =>
        new(
            rate,
            new Period(years, TimeUnit.Years),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M(),
            Context);

    // A helper of another kind of term structure than a yield curve's.
    private sealed class OtherHelper() : BootstrapHelper<VolatilityTermStructure>(0.0)
    {
        public override double ImpliedQuote(EvaluationContext context) => 0.0;
    }
}
