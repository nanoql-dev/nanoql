// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A cycle's member (<see cref="MultiCurveMember"/>) is the one reference nanoql writes after it is built: a
/// multicurve's <see cref="MultiCurve.Build"/> links it once, to a curve with its reference date and day counter (the
/// playbook's handle row). Before that it answers its reference date and day counter, both given when it is created,
/// and the times they measure, and refuses every query that needs its curve; after it, it answers through its curve with
/// its own range and extrapolation flag, and every copy of it shares the link. A <see cref="MultiCurve"/> is
/// an immutable builder whose second <c>Build</c> refuses, and a <c>Build</c> that fails links no member. A guess
/// function that prices a helper on a bootstrapped member added after its own curve, directly or through an
/// extrapolating copy, is refused with nanoql's text, since that member's curve isn't set up yet; QuantLib's guess
/// function takes the times and data, and prices nothing.
/// </summary>
public class MultiCurveMemberTests
{
    private static readonly Date Today = new(15, Month.January, 2024);
    private static readonly Date Spot = new(17, Month.January, 2024);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter Act365 = new Actual365Fixed();

    [Fact]
    public void AMemberRefusesEveryQueryBeforeBuildAndIsLinkedOnce()
    {
        var member = new MultiCurveMember(Spot, Act365);

        // it answers its reference date (and its day counter, AMembersDayCounterIsTheOneItWasCreatedWith)
        Assert.Equal(Spot, member.ReferenceDate);
        Action[] queries =
        [
            () => _ = member.Discount(1.0),
            () => _ = member.Discount(Spot + 30),
            () => _ = member.ZeroRate(1.0, Compounding.Continuous),
            () => _ = member.ForwardRate(1.0, 2.0, Compounding.Continuous),
            () => _ = member.MaxDate,
            () => _ = member.MaxTime,
            () => _ = member.Calendar,
            () => _ = member.SettlementDays,
            () => _ = member.JumpDates(),
            () => _ = member.JumpTimes(),
        ];
        foreach (Action query in queries)
        {
            Assert.Equal("multicurve member not built", Assert.Throws<NanoQLException>(query).Message);
        }

        MultiCurve multiCurve = new MultiCurve(1e-10).AddBootstrappedCurve<Discount, LogLinear>(
            member, Spot, Helpers(), Act365, jumps: [0.995], jumpDates: [new Date(15, Month.July, 2024)]);
        YieldTermStructure curve = multiCurve.Build(Context)[0];

        // linked to its curve, which answers for it, its jumps among the rest
        Assert.Equal(curve.MaxDate, member.MaxDate);
        Assert.Equal([new Date(15, Month.July, 2024)], member.JumpDates());
        Assert.Equal(curve.JumpTimes(), member.JumpTimes());
        Assert.Equal(curve.Discount(3.0), member.Discount(3.0));
        Assert.Equal(curve.DayCounter, member.DayCounter);
        Assert.Null(member.Calendar);
        Assert.Null(member.SettlementDays);

        // and once: neither another multicurve nor the same one links it again
        Assert.Equal(
            "internal handle must be empty; was the curve added already?",
            Assert.Throws<NanoQLException>(() => multiCurve.Build(Context)).Message);
        Assert.Equal(
            "internalHandle",
            Assert.Throws<ArgumentException>(
                () => new MultiCurve(1e-10).AddNonBootstrappedCurve(member, new FlatForward(Spot, 0.03, Act365)))
                .ParamName);
        Assert.Equal(curve.Discount(3.0), member.Discount(3.0));
    }

    [Fact]
    public void AMembersDayCounterIsTheOneItWasCreatedWith()
    {
        var member = new MultiCurveMember(Spot, Act365);

        // before Build: the day counter it was created with, and the times it measures with it
        Assert.Equal(Act365, member.DayCounter);
        Assert.Equal(Act365.YearFraction(Spot, Spot + 30), member.TimeFromReference(Spot + 30));

        // so a curve that follows it reads it now, as the spreaded curve does
        var spreaded = new ZeroSpreadedTermStructure(member, 0.01);
        Assert.Equal(Act365, spreaded.DayCounter);

        // after Build: the same, which Build checked its curve has
        YieldTermStructure curve = new MultiCurve(1e-10).AddBootstrappedCurve<Discount, LogLinear>(
            member, Spot, Helpers(), Act365).Build(Context)[0];
        Assert.Equal(Act365, member.DayCounter);
        Assert.Equal(curve.DayCounter, member.DayCounter);
        Assert.Equal(curve.TimeFromReference(Spot + 30), member.TimeFromReference(Spot + 30));
    }

    [Fact]
    public void AMembersCalendarAndSettlementDaysAreItsCurves()
    {
        MultiCurveMember member = MultiCurveMember.Create(2, new TARGET(), Act365, Context);
        Assert.Equal(Spot, member.ReferenceDate);

        _ = new MultiCurve(1e-10).AddBootstrappedCurve<Discount, LogLinear>(member, 2, new TARGET(), Helpers(), Act365)
            .Build(Context);

        Assert.Equal(new TARGET(), member.Calendar);
        Assert.Equal(2, member.SettlementDays);
    }

    [Fact]
    public void ACopyTakenBeforeBuildPricesAfterItWithItsOwnFlag()
    {
        var member = new MultiCurveMember(Spot, Act365);
        MultiCurveMember copy = member.EnableExtrapolation();
        var index = new Euribor6M(copy);

        ImmutableArray<YieldTermStructure> curves = new MultiCurve(1e-10)
            .AddBootstrappedCurve<Discount, LogLinear>(member, Spot, Helpers(), Act365)
            .Build(Context);

        // past the curve's maximum date, the copy extrapolates and the member it was copied from refuses
        Date past = curves[0].MaxDate + 1000;
        Assert.Equal(curves[0].Discount(past, true), copy.Discount(past));
        Assert.Contains(
            "is past max curve time",
            Assert.Throws<NanoQLException>(() => member.Discount(past)).Message,
            StringComparison.Ordinal);

        // an index built on the copy before Build forecasts on the curve
        Date fixing = new TARGET().Advance(Today, 1, TimeUnit.Years);
        Assert.Equal(new Euribor6M(curves[0]).Fixing(fixing, Context), index.Fixing(fixing, Context));
    }

    [Fact]
    public void AMemberWhoseCurveDiffersIsRefusedAtBuild()
    {
        var member = new MultiCurveMember(Spot, Act365);

        Assert.Equal(
            "multicurve member's reference date (January 16th, 2024) differs from its handle's (January 17th, 2024)",
            Assert.Throws<NanoQLException>(
                () => new MultiCurve(1e-10)
                    .AddNonBootstrappedCurve(member, new FlatForward(new Date(16, Month.January, 2024), 0.03, Act365))
                    .Build(Context)).Message);
        Assert.Equal(
            "multicurve member's day counter (Actual/360) differs from its handle's (Actual/365 (Fixed))",
            Assert.Throws<NanoQLException>(
                () => new MultiCurve(1e-10)
                    .AddBootstrappedCurve<Discount, LogLinear>(member, Spot, Helpers(), new Actual360())
                    .Build(Context)).Message);

        // neither linked it
        Assert.Throws<NanoQLException>(() => member.MaxDate);
    }

    [Fact]
    public void ABuildThatFailsLinksNoMember()
    {
        var flat = new MultiCurveMember(Spot, Act365);
        var member = new MultiCurveMember(Spot, Act365);
        MultiCurve multiCurve = new MultiCurve(endCriteria: new EndCriteria(3, 2, 1e-15, 1e-15, 1e-15))
            .AddNonBootstrappedCurve(flat, new FlatForward(Spot, 0.03, Act365))
            .AddBootstrappedCurve<Discount, LogLinear>(member, Spot, Helpers(), Act365);

        Assert.Equal(
            "global bootstrap failed to minimize to required accuracy (during multi curve bootstrap): MaxIterations",
            Assert.Throws<NanoQLException>(() => multiCurve.Build(Context)).Message);
        Assert.Throws<NanoQLException>(() => flat.MaxDate);
        Assert.Throws<NanoQLException>(() => member.MaxDate);

        // so another multicurve can link them
        ImmutableArray<YieldTermStructure> curves = new MultiCurve()
            .AddNonBootstrappedCurve(flat, new FlatForward(Spot, 0.03, Act365))
            .AddBootstrappedCurve<Discount, LogLinear>(member, Spot, Helpers(), Act365)
            .Build(Context);
        Assert.Equal(curves[1].MaxDate, member.MaxDate);
    }

    [Fact]
    public void AMultiCurveIsAnImmutableBuilder()
    {
        var member = new MultiCurveMember(Spot, Act365);
        var empty = new MultiCurve(1e-10);
        MultiCurve one = empty.AddNonBootstrappedCurve(member, new FlatForward(Spot, 0.03, Act365));

        // adding returned a copy: the empty builder builds nothing and links nothing
        Assert.Empty(empty.Build(Context));
        Assert.Throws<NanoQLException>(() => member.MaxDate);
        Assert.Single(one.Build(Context));
    }

    [Fact]
    public void AGuessFunctionPricingAHelperOnABootstrappedMemberAddedLaterIsRefused()
    {
        // directly, where the member's range check reads the curve's maximum date, and through an extrapolating copy,
        // which skips it and reads the curve's discount
        Func<MultiCurveMember, YieldTermStructure>[] discountings =
            [member => member, member => member.EnableExtrapolation()];
        foreach (Func<MultiCurveMember, YieldTermStructure> discounting in discountings)
        {
            var forecast = new MultiCurveMember(Spot, Act365);
            var discount = new MultiCurveMember(Spot, Act365);

            // the forecasting curve's swaps discount on the member added after it, and its guess function prices them
            RateHelper[] forecastHelpers =
            [
                new DepositRateHelper(
                    0.030, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing,
                    false, new Actual360(), Context),
                Swap(2, 0.031, discounting(discount)),
                Swap(5, 0.032, discounting(discount)),
            ];
            Func<IBootstrapState, EvaluationContext, Vector> guess = (state, context) =>
            {
                foreach (IBootstrapHelper helper in state.Instruments)
                {
                    _ = helper.ImpliedQuote(context);
                }

                return new Vector(state.Times.Length - 1, 0.95);
            };
            MultiCurve multiCurve = new MultiCurve(1e-10)
                .AddBootstrappedCurve<Discount, LogLinear>(
                    forecast, Spot, forecastHelpers, Act365, bootstrap: new GlobalBootstrap(initialGuessFn: guess))
                .AddBootstrappedCurve<Discount, LogLinear>(discount, Spot, Helpers(), Act365);

            Assert.Equal(
                "curve not set up yet: its bootstrap has set no nodes",
                Assert.Throws<NanoQLException>(() => multiCurve.Build(Context)).Message);
        }
    }

    // A six-month deposit and three swaps.
    private static RateHelper[] Helpers() =>
    [
        new DepositRateHelper(
            0.030, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
            new Actual360(), Context),
        Swap(2, 0.031),
        Swap(5, 0.032),
        Swap(10, 0.033),
    ];

    private static SwapRateHelper Swap(int years, double rate, YieldTermStructure? discountingCurve = null) =>
        new(
            rate,
            new Period(years, TimeUnit.Years),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M(),
            Context,
            discountingCurve: discountingCurve);
}
