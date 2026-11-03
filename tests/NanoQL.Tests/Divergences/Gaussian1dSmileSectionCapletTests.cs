// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Gaussian1d smile section on an Ibor index. Its caplet: QuantLib's starts on the fixing date
/// (<c>gaussian1dsmilesection.cpp:61-62</c>, <c>:88-91</c>), so it fixes two business days before the date of the
/// forward it is quoted against; nanoql's starts on the fixing's value date, so it fixes on the section's date. On a
/// GSR model (a = 0.01, σ = 0.01) on a flat 2.7% and Euribor 6M fixing in a year, QuantLib's formula with its caplet
/// (struck at the forward, which it would read) gives an at-the-money volatility of 0.3707922589726553, its caplet
/// fixing on 13 January 2026; the caplet fixing on the section's date, 15 January, 0.37188265489834393, both on the
/// wheel. And its constructor: QuantLib 1.44 refuses every Ibor section, with or without a forwarding curve, "cannot
/// calculate ATM without a BlackCapFloorEngine or BachelierCapFloorEngine" [the harness], its <c>MakeCapFloor</c>
/// without a strike asking for an engine that gives its curve; the annuity reads only the caplet's dates, so nanoql's
/// builds it: its level on the index's curve or, without one, the model's, its prices on an index with a curve.
/// </summary>
public class Gaussian1dSmileSectionCapletTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.027, new Actual365Fixed());
    private static readonly Date Fixing = new TARGET().Advance(Today, 1, TimeUnit.Years);

    [Fact]
    public void TheAtmVolatilityIsTheCapletFixingOnTheSectionsDate()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        var index = new Euribor6M(Curve);
        var section = new Gaussian1dSmileSection(Fixing, index, model, new Actual365Fixed(), Context);
        double atm = section.AtmLevel()!.Value;

        CapFloor caplet = new MakeCapFloor(CapFloorType.Cap, index.Tenor, index)
            .WithStrike(atm)
            .WithEffectiveDate(index.ValueDate(Fixing), false)
            .ToCapFloor(Context);
        double annuity = index.DayCounter.YearFraction(caplet.StartDate(), caplet.MaturityDate()) * model.Zerobond(caplet.MaturityDate());
        double price = Gaussian1dCapFloorEngine.Calculate(caplet, Context, model).NPV!.Value / annuity;
        double expected = BlackFormulaImpliedStdDev(OptionType.Call, atm, atm, price) / Math.Sqrt(section.ExerciseTime);

        Assert.Equal(Fixing, ((FloatingRateCoupon)caplet.FloatingLeg[0]).FixingDate());
        Assert.Equal(expected, section.Volatility(atm), 1e-12);
        Assert.Equal(0.37188265489834393, section.Volatility(atm), 1e-10);
    }

    [Fact]
    public void AnIborSectionIsBuilt()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.01, 0.01);
        var forecast = new Euribor6M(new FlatForward(Today, 0.031, new Actual365Fixed()));
        var onItsCurve = new Gaussian1dSmileSection(Fixing, forecast, model, new Actual365Fixed(), Context);
        var withoutOne = new Gaussian1dSmileSection(Fixing, new Euribor6M(), model, new Actual365Fixed(), Context);

        Assert.Equal(model.ForwardRate(Fixing, Context, null, 0.0, forecast), onItsCurve.AtmLevel());
        Assert.True(onItsCurve.Volatility(0.03) > 0.0);

        // without one, the level on the model's curve, and the caplet's price refused as QuantLib's caplet refuses it:
        // its arguments read the coupon's forecast (capfloor.cpp:245-246)
        Assert.Equal(model.ForwardRate(Fixing, Context, null, 0.0, new Euribor6M()), withoutOne.AtmLevel());
        Assert.Equal(
            "null term structure set to this instance of Euribor6M Actual/360",
            Assert.Throws<NanoQLException>(() => withoutOne.OptionPrice(0.03)).Message);
    }
}
