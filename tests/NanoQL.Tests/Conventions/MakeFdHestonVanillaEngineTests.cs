// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="MakeFdHestonVanillaEngine"/> is an immutable builder with QuantLib's defaults: its engine is
/// <see cref="FdHestonVanillaEngine"/>'s with the settings given, QuantLib's 100 × 100 × 50 grid and Hundsdorfer's
/// scheme by default, bit for bit; each method returns a copy, the builder it was called on unchanged; and the grid
/// sizes and damping steps are refused when negative, naming their parameter.
/// </summary>
public class MakeFdHestonVanillaEngineTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    private static readonly HestonModel Model = new(new HestonProcess(
        new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365), 100.0, 0.04, 1.5, 0.04, 0.4, -0.6));

    private static readonly VanillaOption Option =
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(Today + 365));

    [Fact]
    public void TheDefaultsAreQuantLibs()
    {
        OneAssetOptionResults built = new MakeFdHestonVanillaEngine(Model).ToEngine()(Option, Context);
        OneAssetOptionResults direct = FdHestonVanillaEngine.Calculate(Option, Context, Model);

        Assert.Equal(direct.NPV, built.NPV);
        Assert.Equal(direct.Delta, built.Delta);
        Assert.Equal(direct.Gamma, built.Gamma);
        Assert.Equal(direct.Theta, built.Theta);
    }

    [Fact]
    public void TheSettingsReachTheEngineAndEachCallReturnsACopy()
    {
        var helper = new FdmQuantoHelper(
            new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.03, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.15, A365), -0.4, 1.0);
        LocalVolTermStructure leverage = new LocalConstantVol(Today, 1.1, A365);
        var builder = new MakeFdHestonVanillaEngine(Model);
        MakeFdHestonVanillaEngine set = builder
            .WithTGrid(30)
            .WithXGrid(40)
            .WithVGrid(15)
            .WithDampingSteps(2)
            .WithFdmSchemeDesc(FdmSchemeDesc.Douglas())
            .WithLeverageFunction(leverage)
            .WithQuantoHelper(helper)
            .WithCashDividends([Today + 180], [2.0]);

        OneAssetOptionResults built = set.ToEngine()(Option, Context);
        OneAssetOptionResults direct = FdHestonVanillaEngine.Calculate(
            Option,
            Context,
            Model,
            NanoQL.Coupons.DividendFunctions.DividendVector([Today + 180], [2.0]),
            helper,
            30,
            40,
            15,
            2,
            FdmSchemeDesc.Douglas(),
            leverage);
        Assert.Equal(direct.NPV, built.NPV);
        Assert.Equal(direct.Delta, built.Delta);

        // the builder the calls started from still has QuantLib's defaults
        Assert.Equal(FdHestonVanillaEngine.Calculate(Option, Context, Model).NPV, builder.ToEngine()(Option, Context).NPV);
    }

    [Fact]
    public void NegativeGridsAreRefused()
    {
        var builder = new MakeFdHestonVanillaEngine(Model);
        Assert.Equal("tGrid", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithTGrid(-1)).ParamName);
        Assert.Equal("xGrid", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithXGrid(-1)).ParamName);
        Assert.Equal("vGrid", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithVGrid(-1)).ParamName);
        Assert.Equal("dampingSteps", Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithDampingSteps(-1)).ParamName);
        Assert.Equal("hestonModel", Assert.Throws<ArgumentNullException>(() => new MakeFdHestonVanillaEngine(null!)).ParamName);
    }
}
