// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Models.MarkovFunctionalModelSettingsAdjustments;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The refusals of the Markov-functional model, its state process, the smile sections it calibrates to and the flat
/// extrapolator, by type, message and parameter (playbook §2; the S-g1d plan's G1D17): QuantLib's texts, a check of an
/// argument an <see cref="ArgumentException"/> naming it, a check of a value already stored or computed a
/// <see cref="NanoQLException"/>. The settings' own refusals are <see cref="MarkovFunctionalSettingsTests"/>'.
/// </summary>
public class MarkovFunctionalRefusalTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly TARGET Calendar = new();
    private static readonly SwapIndex SwapIndex = new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years));

    [Fact]
    public void TheModelsConstructorRefusals()
    {
        Date one = Calendar.Advance(Today, 1, TimeUnit.Years), two = Calendar.Advance(Today, 2, TimeUnit.Years);
        SwaptionVolatilityStructure vol = new ConstantSwaptionVolatility(Today, Calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed());
        OptionletVolatilityStructure capletVol = new ConstantOptionletVolatility(Today, Calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed());
        Period[] tenors = [new(10, TimeUnit.Years), new(10, TimeUnit.Years)];
        MarkovFunctional Swaptions(Date[] steps, double[] vols, Date[] expiries, Period[] t, SwaptionVolatilityStructure v) =>
            new(Curve, 0.01, steps, vols, v, expiries, t, SwapIndex, Context);

        Assert.Equal("termStructure", Assert.Throws<ArgumentNullException>(() => new MarkovFunctional(null!, 0.01, [], [0.01], vol, [one], [tenors[0]], SwapIndex, Context)).ParamName);
        ArgumentNullException noVol = Assert.Throws<ArgumentNullException>(() => Swaptions([], [1.0], [one], [tenors[0]], null!));
        Assert.Equal("swaptionVol", noVol.ParamName);
        Assert.StartsWith("swaption volatility structure is empty", noVol.Message, StringComparison.Ordinal);
        ArgumentNullException noCapletVol = Assert.Throws<ArgumentNullException>(() => new MarkovFunctional(Curve, 0.01, [], [1.0], null!, [one], new Euribor6M(Curve), Context));
        Assert.Equal("capletVol", noCapletVol.ParamName);
        Assert.StartsWith("caplet volatility structure is empty", noCapletVol.Message, StringComparison.Ordinal);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => new MarkovFunctional(Curve, 0.01, [], [1.0], vol, [one], [tenors[0]], SwapIndex, null!)).ParamName);

        Check(() => Swaptions([], [1.0], [one, two], [tenors[0]], vol), "swaptionTenors", "number of swaption expiries (2) is differnt from number of swaption tenors (1)");
        Check(() => Swaptions([], [1.0], [], [], vol), "swaptionExpiries", "need at least one swaption expiry to calibrate numeraire");
        Check(() => new MarkovFunctional(Curve, 0.01, [], [1.0], capletVol, [], new Euribor6M(Curve), Context), "capletExpiries", "need at least one caplet expiry to calibrate numeraire");
        Check(() => Swaptions([], [1.0], [one, one], tenors, vol), "swaptionExpiries", $"swaption expiry ({one}) occurs more than once in calibration set");
        Check(() => new MarkovFunctional(Curve, 0.01, [], [1.0], capletVol, [one, one], new Euribor6M(Curve), Context), "capletExpiries", $"caplet expiry ({one}) occurs more than once in calibration set");
        Check(() => Swaptions([Today], [1.0, 1.0], [one], [tenors[0]], vol), "volstepdates", "volsteptimes must be positive (0)");
        Check(() => Swaptions([two, one], [1.0, 1.0, 1.0], [one], [tenors[0]], vol), "volstepdates", "volsteptimes must be strictly increasing (");
        Check(() => Swaptions([one], [1.0], [one], [tenors[0]], vol), "volatilities", "there must be n+1 volatilities (1) for n volatility step times (1)");
    }

    [Fact]
    public void TheModelsSabrSmileRefusals()
    {
        Date one = Calendar.Advance(Today, 1, TimeUnit.Years);
        var settings = new MarkovFunctionalModelSettings().WithAdjustments(SabrSmile);
        SwaptionVolatilityStructure normal = new ConstantSwaptionVolatility(
            Today, Calendar, BusinessDayConvention.ModifiedFollowing, 0.005, new Actual365Fixed(), VolatilityType.Normal);
        Assert.Equal(
            "MarkovFunctional: SABR calibration to normal input volatilities is not supported",
            Assert.Throws<NanoQLException>(() => new MarkovFunctional(Curve, 0.01, [], [1.0], normal, [one], [new Period(10, TimeUnit.Years)], SwapIndex, Context, settings)).Message);

        SwaptionVolatilityStructure vol = new ConstantSwaptionVolatility(Today, Calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed());
        Assert.Equal(
            "for sabr calibration at least 4 points are needed (is 3)",
            Assert.Throws<NanoQLException>(() => new MarkovFunctional(
                Curve, 0.01, [], [1.0], vol, [one], [new Period(10, TimeUnit.Years)], SwapIndex, Context,
                settings.WithSmileMoneynessCheckpoints([0.5, 1.0, 1.5]))).Message);
    }

    [Fact]
    public void TheMembersRefusals()
    {
        Date one = Calendar.Advance(Today, 1, TimeUnit.Years);
        SwaptionVolatilityStructure vol = new ConstantSwaptionVolatility(Today, Calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed());
        var model = new MarkovFunctional(
            Curve, 0.01, [], [1.0], vol, [one], [new Period(10, TimeUnit.Years)], SwapIndex, Context,
            new MarkovFunctionalModelSettings().WithYGridPoints(16).WithGaussHermitePoints(16));
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => model.ModelOutputs(null!)).ParamName);
        Assert.Equal("context", Assert.Throws<ArgumentNullException>(() => model.ForceArbitrageIndices([], null!)).ParamName);
    }

    [Fact]
    public void TheStateProcesssRefusals()
    {
        Check(() => new MfStateProcess(0.01, [1.0, 2.0], [0.01, 0.012]), "vols", "number of volatilities (2) compared to number of times (2 must be bigger by one");
        Check(() => new MfStateProcess(0.01, [2.0, 1.0], [0.01, 0.012, 0.009]), "times", "times must be increasing (2@0 , 1@1)");
        Check(() => new MfStateProcess(0.01, [1.0, 2.0], [0.01, -0.012, 0.009]), "vols", "volatilities must be non negative (-0.012@1)");
        Assert.Equal("times", Assert.Throws<ArgumentNullException>(() => new MfStateProcess(0.01, null!, [0.01])).ParamName);
        Assert.Equal("vols", Assert.Throws<ArgumentNullException>(() => new MfStateProcess(0.01, [], null!)).ParamName);
    }

    [Fact]
    public void TheSmileSectionsRefusals()
    {
        var sabr = new SabrSmileSection(5.0, 0.03, [0.03, 0.6, 0.4, -0.3]);
        Check(() => new SmileSectionUtils(sabr, [-0.1, 1.0, 2.0]), "moneynessGrid", "moneyness grid should only contain non negative values (-0.1)");
        Check(() => new SmileSectionUtils(sabr, [0.0, 0.5, 0.5, 2.0]), "moneynessGrid", "moneyness grid should contain strictly increasing values (0.5,0.5 at indices 1, 2)");
        Check(() => new SmileSectionUtils(new FlatSmileSection(1.0, 0.2, new Actual365Fixed())), "atm", "atm level must be provided by source section or given in the constructor");
        Assert.Equal(
            "Atm point in moneyness grid (1) too close to boundary.",
            Assert.Throws<NanoQLException>(() => new SmileSectionUtils(sabr, [1.0, 1.5])).Message);
        Assert.Equal("section", Assert.Throws<ArgumentNullException>(() => new SmileSectionUtils(null!)).ParamName);

        Check(
            () => new KahaleSmileSection(new FlatSmileSection(1.0, 0.005, new Actual365Fixed(), 0.03, VolatilityType.Normal)),
            "source",
            "KahaleSmileSection only supports shifted lognormal source sections");
        Assert.StartsWith(
            "can not extrapolate to left, right index of af region reached (",
            Assert.Throws<NanoQLException>(() => new KahaleSmileSection(sabr, null, false, false, false, default, 1e-5, 100)).Message,
            StringComparison.Ordinal);
        Assert.StartsWith(
            "can not extrapolate to right, left index of af region reached (",
            Assert.Throws<NanoQLException>(() => new KahaleSmileSection(sabr, null, false, false, false, default, 1e-5, -1, 0)).Message,
            StringComparison.Ordinal);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => new KahaleSmileSection(null!)).ParamName);

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => new AtmSmileSection(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => new AtmAdjustedSmileSection(null!)).ParamName);

        Check(
            () => SabrInterpolatedSmileSection.Create(
                Calendar.Advance(Today, 1, TimeUnit.Years), 0.03, [0.02, 0.03], false, 0.2, [0.2, 0.2, 0.2], 0.03, 0.5, 0.4, 0.0, Context),
            "strikes",
            "2 strikes given for 3 volatilities");
        Assert.Equal(
            "context",
            Assert.Throws<ArgumentNullException>(() => SabrInterpolatedSmileSection.Create(
                Calendar.Advance(Today, 1, TimeUnit.Years), 0.03, [0.02, 0.03, 0.04], false, 0.2, [0.2, 0.2, 0.2], 0.03, 0.5, 0.4, 0.0, null!)).ParamName);
    }

    [Fact]
    public void TheFlatExtrapolatorsRefusal() =>
        Assert.Equal("decoratedInterpolation", Assert.Throws<ArgumentNullException>(() => new FlatExtrapolator(null!)).ParamName);

    private static void Check(Func<object> call, string paramName, string message)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(call);
        Assert.Equal(typeof(ArgumentException), e.GetType());
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message, StringComparison.Ordinal);
    }
}
