// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The year-on-year cap and floor's refusals, with QuantLib's texts (<c>inflationcapfloor.cpp</c>): a cap without cap
/// rates or a floor without floor rates is an <see cref="ArgumentException"/> naming the rates, as are no strikes; a
/// collar given to the strikes constructor an <see cref="ArgumentOutOfRangeException"/> naming the type; an optionlet
/// past the leg an <see cref="ArgumentException"/> naming the index, "3rd optionlet does not exist, only 2"; a null
/// cash flow in the leg, or a <c>default</c> array, an <see cref="ArgumentException"/> naming the leg, "null cash flow
/// in leg", from every constructor; and a leg with a cash flow that isn't a year-on-year coupon, which the engines
/// refuse, and the implied volatility, which QuantLib doesn't implement, each a <see cref="NanoQLException"/>.
/// </summary>
public class YoYInflationCapFloorRefusalTests
{
    private static readonly Date Today = new(15, Month.June, 2024);

    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void MissingRatesAreRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new YoYInflationCap(Leg(), []));
        Assert.Equal("exerciseRates", e.ParamName);
        Assert.StartsWith("no cap rates given", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentException>(() => new YoYInflationFloor(Leg(), []));
        Assert.Equal("exerciseRates", e.ParamName);
        Assert.StartsWith("no floor rates given", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentException>(() => new YoYInflationCollar(Leg(), [0.03], []));
        Assert.Equal("floorRates", e.ParamName);
        Assert.StartsWith("no floor rates given", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACollarIsRefusedByTheStrikesConstructor()
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new YoYInflationCapFloor(YoYInflationCapFloorType.Collar, Leg(), [0.03]));
        Assert.Equal("type", e.ParamName);
        Assert.StartsWith("only Cap/Floor types allowed in this constructor", e.Message, StringComparison.Ordinal);
        ArgumentException noStrikes = Assert.Throws<ArgumentException>(
            () => new YoYInflationCapFloor(YoYInflationCapFloorType.Cap, Leg(), []));
        Assert.Equal("strikes", noStrikes.ParamName);
        Assert.StartsWith("no strikes given", noStrikes.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullCashFlowIsRefused()
    {
        ImmutableArray<CashFlow> withNull = Leg().Add(null!);
        Action[] builds =
        [
            () => _ = new YoYInflationCap(withNull, [0.03]),
            () => _ = new YoYInflationFloor(withNull, [0.03]),
            () => _ = new YoYInflationCollar(withNull, [0.04], [0.02]),
            () => _ = new YoYInflationCapFloor(YoYInflationCapFloorType.Cap, withNull, [0.03], []),
            () => _ = new YoYInflationCapFloor(YoYInflationCapFloorType.Floor, withNull, [0.03]),
        ];
        foreach (Action build in builds)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(build);
            Assert.Equal("yoyLeg", e.ParamName);
            Assert.StartsWith("null cash flow in leg", e.Message, StringComparison.Ordinal);
        }

        Assert.Equal("yoyLeg", Assert.Throws<ArgumentException>(() => new YoYInflationCap(default, [0.03])).ParamName);
    }

    [Fact]
    public void AnOptionletPastTheLegIsRefused()
    {
        var cap = new YoYInflationCap(Leg(), [0.03]);
        Assert.Equal(2, cap.YoYLeg.Length);
        ArgumentException e = Assert.Throws<ArgumentException>(() => cap.Optionlet(2));
        Assert.Equal("i", e.ParamName);
        Assert.StartsWith("3rd optionlet does not exist, only 2", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherCashFlowAndTheImpliedVolatilityAreRefused()
    {
        ImmutableArray<CashFlow> leg = [.. Leg(), new SimpleCashFlow(1e6, new Date(15, Month.June, 2026))];
        var cap = new YoYInflationCap(leg, [0.03]);
        var index = new YoYInflationIndex(new UKRPI());
        ConstantYoYOptionletVolatility vol = ConstantYoYOptionletVolatility.Create(
            0.15,
            0,
            new UnitedKingdom(),
            BusinessDayConvention.ModifiedFollowing,
            new Actual365Fixed(),
            new Period(2, TimeUnit.Months),
            Frequency.Monthly,
            false,
            Context);
        var nominal = new FlatForward(Today, 0.04, new Actual365Fixed());

        Assert.Equal(
            "non-YoYInflationCoupon given",
            Assert.Throws<NanoQLException>(() => YoYInflationBlackCapFloorEngine.Calculate(cap, Context, index, vol, nominal)).Message);
        Assert.Equal(
            "not implemented yet",
            Assert.Throws<NanoQLException>(() => new YoYInflationCap(Leg(), [0.03]).ImpliedVolatility(100.0, null, 0.1, Context)).Message);
    }

    private static ImmutableArray<CashFlow> Leg() =>
        new YoYInflationLeg(
            new Schedule(
                Today,
                new Date(15, Month.June, 2026),
                new Period(1, TimeUnit.Years),
                new UnitedKingdom(),
                BusinessDayConvention.Unadjusted,
                BusinessDayConvention.Unadjusted,
                DateGenerationRule.Forward,
                false,
                Context),
            new UnitedKingdom(),
            new YoYInflationIndex(new UKRPI()),
            new Period(2, TimeUnit.Months),
            CPIInterpolationType.Flat)
        .WithNotionals(1e6)
        .WithPaymentDayCounter(new Actual365Fixed())
        .ToLeg();
}
