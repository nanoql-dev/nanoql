// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Models.MarkovFunctionalModelSettingsAdjustments;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Members of the Gaussian1d models and the Kahale section that QuantLib's own accessors give: GSR's calibrated
/// arguments, its reversions then its volatilities (<c>gsr.cpp</c>'s <c>arguments_[0]</c> and <c>[1]</c>); the
/// Markov-functional model's, its volatilities alone (<c>arguments_[0]</c>), and its settings; the head of its model
/// outputs' text, QuantLib's <c>operator&lt;&lt;</c> (<c>markovfunctional.cpp:834-886</c>), the settings in the
/// stream's default format; the settings' copy with another custom smile factory (<c>withCustomSmileFactory</c>); and
/// the Kahale section's exercise date, day counter and reference date, its source's
/// (<c>kahalesmilesection.hpp:143-146</c>).
/// </summary>
public class Gaussian1dMemberTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void GsrsArgumentsAreItsReversionsThenItsVolatilities()
    {
        var target = new TARGET();
        var gsr = new Gsr(Curve, [target.Advance(Today, 1, TimeUnit.Years)], [0.008, 0.009], [0.02, 0.025]);

        Assert.Equal(2, gsr.Arguments.Length);
        Assert.Equal(new[] { 0.02, 0.025 }, gsr.Arguments[0].Params().ToArray());
        Assert.Equal(new[] { 0.008, 0.009 }, gsr.Arguments[1].Params().ToArray());
    }

    [Fact]
    public void TheMarkovFunctionalModelsArgumentsSettingsAndOutputsText()
    {
        var settings = new MarkovFunctionalModelSettings(
            32, 7.0, 16, 1e-5, 1e-7, 0.0, 2.0, KahaleSmile | SmileExponentialExtrapolation | ExtrapolatePayoffFlat,
            [0.1, 0.25, 0.50, 0.75, 1.0, 1.25, 1.50, 2.0, 5.0]);
        MarkovFunctional model = Model(settings);

        Assert.Single(model.Arguments);
        Assert.Equal(new[] { 1.0 }, model.Arguments[0].Params().ToArray());
        Assert.Equal(32, model.ModelSettings.YGridPoints);
        Assert.Equal(16, model.ModelSettings.GaussHermitePoints);
        Assert.Equal(settings.Adjustments, model.ModelSettings.Adjustments);
        Assert.StartsWith(
            "Markov functional model trace output \n"
                + "Model settings\n"
                + "Grid points y        : 32\n"
                + "Std devs y           : 7\n"
                + "Lower rate bound     : 0\n"
                + "Upper rate bound     : 2\n"
                + "Gauss Hermite points : 16\n"
                + "Digital gap          : 1e-05\n"
                + "Adjustments          : FlatPayoffExt Kahale SmileExp \n"
                + "Smile moneyness checkpoints: 0.1;0.25;0.5;0.75;1;1.25;1.5;2;5\n"
                + "\nMessages:\n",
            model.ModelOutputs(Context).ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheSettingsCopyTakesAnotherCustomSmileFactory()
    {
        var settings = new MarkovFunctionalModelSettings(32, 7.0, 16, 1e-5, 1e-7, 0.0, 2.0, CustomSmile, default);
        var factory = new NoSmile();

        MarkovFunctionalModelSettings copy = settings.WithCustomSmileFactory(factory);

        Assert.Same(factory, copy.CustomSmileFactory);
        Assert.Null(settings.CustomSmileFactory);
        Assert.Equal(settings.YGridPoints, copy.YGridPoints);
        Assert.Equal(settings.Adjustments, copy.Adjustments);
    }

    [Fact]
    public void TheKahaleSectionsDatesAndDayCounterAreItsSources()
    {
        var source = new FlatSmileSection(new Date(15, Month.January, 2027), 0.2, new Actual360(), Today, 0.03);

        var kahale = new KahaleSmileSection(source, 0.03);

        Assert.Equal(source.ExerciseDate, kahale.ExerciseDate);
        Assert.Equal(source.DayCounter, kahale.DayCounter);
        Assert.Equal(source.ReferenceDate(), kahale.ReferenceDate());
    }

    private static MarkovFunctional Model(MarkovFunctionalModelSettings settings)
    {
        var target = new TARGET();
        SwaptionVolatilityStructure vol = new ConstantSwaptionVolatility(Today, target, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed());
        Date[] expiries = [.. Enumerable.Range(1, 3).Select(i => target.Advance(Today, i, TimeUnit.Years))];
        Period[] tenors = [.. Enumerable.Repeat(new Period(10, TimeUnit.Years), 3)];
        return new MarkovFunctional(
            Curve, 0.01, [], [1.0], vol, expiries, tenors, new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years)), Context, settings);
    }

    // A factory the settings hold and never call here.
    private sealed class NoSmile : MarkovFunctionalCustomSmileFactory
    {
        public override MarkovFunctionalCustomSmileSection SmileSection(SmileSection source, double atm) =>
            throw new InvalidOperationException("not called");
    }
}
