// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of the caps' and floors' refusals, which the vectors' <c>{"throws": true}</c> can't tell apart
/// (playbook §9.4), with their messages and parameter names: a refused argument throws <see cref="ArgumentException"/>
/// naming it (QuantLib's checks of the instruments' rates and strikes, the surface's inputs, the engines' structures and
/// displacements, and the optionlet index past the leg), an enum value outside its type or a member a constructor never
/// handles <see cref="ArgumentOutOfRangeException"/>, a null argument <see cref="ArgumentNullException"/> (the builder's
/// index among them), and a check on stored values <see cref="NanoQLException"/> (an expired instrument's implied
/// volatility, an empty leg's last coupon, and the builder's at-the-money strike without a discount curve), each with
/// QuantLib's text in C#'s number formatting.
/// </summary>
public class CapFloorErrorTests
{
    private static readonly Date Today = new(16, Month.June, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.04, new Actual365Fixed());

    [Fact]
    public void TheCapFloorRefusals()
    {
        ImmutableArray<CashFlow> leg = Leg();
        var cap = new Cap(leg, [0.03]);

        // The constructors
        AssertOutOfRange("type", "unknown CapFloor::Type (7)", () => new CapFloor((CapFloorType)7, leg, [0.03], [0.02]));
        AssertArgument("exerciseRates", "no cap rates given", () => new Cap(leg, []));
        AssertArgument("exerciseRates", "no floor rates given", () => new Floor(leg, []));
        AssertArgument("floorRates", "no floor rates given", () => new Collar(leg, [0.05], []));
        AssertArgument("capRates", "no cap rates given", () => new Collar(leg, [], [0.01]));
        AssertArgument("capRates", "no cap rates given", () => new CapFloor(CapFloorType.Cap, leg, [], [0.01]));
        AssertArgument("strikes", "no strikes given", () => new CapFloor(CapFloorType.Collar, leg, []));
        AssertOutOfRange("type", "only Cap/Floor types allowed in this constructor", () => new CapFloor(CapFloorType.Collar, leg, [0.03]));
        AssertOutOfRange("type", "only Cap/Floor types allowed in this constructor", () => new CapFloor((CapFloorType)7, leg, [0.03]));
        AssertNull("exerciseRates", () => new Cap(leg, null!));
        AssertNull("floorRates", () => new CapFloor(CapFloorType.Cap, leg, [0.03], null!));
        AssertNull("strikes", () => new CapFloor(CapFloorType.Cap, leg, null!));

        // The inspectors
        Assert.Equal("i", Assert.Throws<ArgumentOutOfRangeException>(() => cap.Optionlet(-1)).ParamName);
        AssertArgument("i", "3rd optionlet does not exist, only 2", () => cap.Optionlet(2));
        AssertFailure("empty leg", () => new Cap([], [0.03]).LastFloatingRateCoupon());
        AssertNull("context", () => cap.IsExpired(null!));
        AssertNull("discountCurve", () => cap.AtmRate(null!, Context));

        // The implied volatility
        double price = BlackCapFloorEngine.Calculate(cap, Context, Curve, 0.2).NPV!.Value;
        AssertOutOfRange(
            "type", "unknown VolatilityType (Unknown volatility type (7))", () => cap.ImpliedVolatility(price, Curve, 0.1, Context, type: (VolatilityType)7));
        Assert.Equal("maxEvaluations", Assert.Throws<ArgumentOutOfRangeException>(() => cap.ImpliedVolatility(price, Curve, 0.1, Context, maxEvaluations: -1)).ParamName);
        AssertFailure("instrument expired", () => new Cap([], [0.03]).ImpliedVolatility(price, Curve, 0.1, Context));
        AssertArgument("minVol", "invalid range: xMin_ (0.5) >= xMax_ (0.3)", () => cap.ImpliedVolatility(price, Curve, 0.4, Context, minVol: 0.5, maxVol: 0.3));
        AssertArgument("guess", "guess (5) > xMax_ (4)", () => cap.ImpliedVolatility(price, Curve, 5.0, Context));
        Assert.StartsWith(
            "root not bracketed: f[1E-07,4] -> [",
            Assert.Throws<NanoQLException>(() => cap.ImpliedVolatility(1.0, Curve, 0.1, Context)).Message,
            StringComparison.Ordinal);
        AssertNull("discountCurve", () => cap.ImpliedVolatility(price, null!, 0.1, Context));

        // The engines
        var normal = new ConstantOptionletVolatility(Today, new TARGET(), BusinessDayConvention.Following, 0.01, new Actual365Fixed(), VolatilityType.Normal);
        var shifted = new ConstantOptionletVolatility(Today, new TARGET(), BusinessDayConvention.Following, 0.2, new Actual365Fixed(), VolatilityType.ShiftedLognormal, 0.01);
        AssertArgument(
            "vol",
            "BlackCapFloorEngine should only be used for vol surfaces stripped with shifted log normal model. Options were stripped with model Normal",
            () => BlackCapFloorEngine.Calculate(cap, Context, Curve, normal));
        AssertArgument(
            "displacement",
            "Displacement used for stripping and provided for pricing differ. Model displacement was : 0.01",
            () => BlackCapFloorEngine.Calculate(cap, Context, Curve, shifted, 0.0));
        AssertArgument(
            "vol",
            "BachelierCapFloorEngine should only be used for vol surfaces stripped with normal model. Options were stripped with model ShiftedLognormal",
            () => BachelierCapFloorEngine.Calculate(cap, Context, Curve, shifted));
        AssertNull("capFloor", () => BlackCapFloorEngine.Calculate(null!, Context, Curve, 0.2));
        AssertNull("context", () => BachelierCapFloorEngine.Calculate(cap, null!, Curve, 0.01));
        AssertNull("discountCurve", () => BlackCapFloorEngine.Calculate(cap, Context, null!, normal));
        AssertNull("vol", () => BachelierCapFloorEngine.Calculate(cap, Context, Curve, (OptionletVolatilityStructure)null!));

        // The builder
        AssertNull("iborIndex", () => new MakeCapFloor(CapFloorType.Cap, new Period(2, TimeUnit.Years), null!));
        var builder = new MakeCapFloor(CapFloorType.Cap, new Period(2, TimeUnit.Years), new Euribor6M(Curve));
        AssertFailure("cannot calculate ATM without a BlackCapFloorEngine or BachelierCapFloorEngine", () => builder.ToCapFloor(Context));
        AssertOutOfRange(
            "type",
            "only Cap/Floor types allowed in this constructor",
            () => new MakeCapFloor(CapFloorType.Collar, new Period(2, TimeUnit.Years), new Euribor6M(Curve)).WithDiscountingTermStructure(Curve).ToCapFloor(Context));
        AssertNull("context", () => builder.WithStrike(0.03).ToCapFloor(null!));
        AssertArgument("effectiveDate", "default(Date)", () => builder.WithEffectiveDate(default(Date), true));
        Assert.Equal("bdc", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithConvention((BusinessDayConvention)99)).ParamName);

        // The surface
        Period[] tenors = [new(1, TimeUnit.Years), new(2, TimeUnit.Years)];
        double[] strikes = [0.02, 0.03];
        var vols = new Matrix(2, 2, 0.2);
        CapFloorTermVolSurface Surface(Period[] t, double[] k, Matrix m) =>
            new(Today, new TARGET(), BusinessDayConvention.Following, t, k, m);
        AssertArgument("optionTenors", "empty option tenor vector", () => Surface([], strikes, vols));
        AssertArgument("volatilities", "mismatch between number of option tenors (2) and number of volatility rows (1)", () => Surface(tenors, strikes, new Matrix(1, 2, 0.2)));
        AssertArgument("optionTenors", "negative first option tenor: 0D", () => Surface([new(0, TimeUnit.Days), new(1, TimeUnit.Years)], strikes, vols));
        AssertArgument("optionTenors", "non increasing option tenor: 1st is 1Y, 2nd is 1Y", () => Surface([new(1, TimeUnit.Years), new(1, TimeUnit.Years)], strikes, vols));
        AssertArgument("volatilities", "mismatch between strikes(3) and vol columns (2)", () => Surface(tenors, [0.02, 0.03, 0.04], vols));
        AssertArgument("strikes", "non increasing strikes: 1st is 3.000000 %, 2nd is 2.000000 %", () => Surface(tenors, [0.03, 0.02], vols));
        AssertFailure("undecidable comparison between 1M and 30D", () => Surface([new(1, TimeUnit.Months), new(30, TimeUnit.Days)], strikes, vols));
        AssertArgument("x", "not enough x points to interpolate: at least 2 required, 1 provided", () => Surface(tenors, [0.02], new Matrix(2, 1, 0.2)));
        AssertArgument("y", "not enough y points to interpolate: at least 2 required, 1 provided", () => Surface([new(1, TimeUnit.Years)], strikes, new Matrix(1, 2, 0.2)));
        AssertFailure("no calendar implementation provided", () => new CapFloorTermVolSurface(Today, null, BusinessDayConvention.Following, tenors, strikes, vols));
        AssertNull("volatilities", () => Surface(tenors, strikes, null!));
        AssertNull("calendar", () => CapFloorTermVolSurface.Create(2, null!, BusinessDayConvention.Following, tenors, strikes, vols, Context));
        Assert.Equal(
            "settlementDays",
            Assert.Throws<ArgumentOutOfRangeException>(() => CapFloorTermVolSurface.Create(-1, new TARGET(), BusinessDayConvention.Following, tenors, strikes, vols, Context)).ParamName);
        CapFloorTermVolSurface surface = Surface(tenors, strikes, vols);
        AssertFailure("strike (0.05) is outside the curve domain [0.02,0.03]", () => surface.Volatility(1.0, 0.05));
        AssertFailure("negative time (-0.5) given", () => surface.Volatility(-0.5, 0.02, true));
        AssertFailure("time (3) is past max curve time (2)", () => surface.Volatility(3.0, 0.02));
        AssertArgument("end", "default(Date)", () => surface.Volatility(default(Date), 0.02));
    }

    // A 1Y semiannual Euribor 6M leg from the spot date: two coupons.
    private static ImmutableArray<CashFlow> Leg()
    {
        var index = new Euribor6M(Curve);
        var schedule = new Schedule(
            new Date(18, Month.June, 2025), new Date(18, Month.June, 2026), new Period(6, TimeUnit.Months), new TARGET(),
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, Context);
        return new IborLeg(schedule, index).WithNotionals([1.0]).ToLeg(Context);
    }

    private static void AssertArgument(string paramName, string message, Action action)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void AssertOutOfRange(string paramName, string message, Action action)
    {
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }

    private static void AssertNull(string paramName, Action action) =>
        Assert.Equal(paramName, Assert.Throws<ArgumentNullException>(action).ParamName);

    private static void AssertFailure(string message, Action action) =>
        Assert.Equal(message, Assert.Throws<NanoQLException>(action).Message);
}
