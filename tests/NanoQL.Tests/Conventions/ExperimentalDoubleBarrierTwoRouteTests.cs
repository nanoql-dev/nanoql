// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// C1's two routes to one value (the S-xeq plan's §2.5): the binomial, Suo-Wang and Monte Carlo double barriers against
/// S-bar's analytic engine, and the quanto double barrier at zero correlation against the double barrier. The
/// perturbative, Vanna-Volga, Heston forward and Asian Heston engines' routes sit with their divergence facts.
/// </summary>
public class ExperimentalDoubleBarrierTwoRouteTests
{
    private static readonly Date Today = new(15, May, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(DoubleBarrierType.KnockOut, OptionType.Call)]
    [InlineData(DoubleBarrierType.KnockIn, OptionType.Call)]
    [InlineData(DoubleBarrierType.KnockOut, OptionType.Put)]
    [InlineData(DoubleBarrierType.KnockIn, OptionType.Put)]
    public void TheClosedFormsAndTheTreeAgree(DoubleBarrierType type, OptionType optionType)
    {
        DoubleBarrierOption option = Option(type, optionType);
        GeneralizedBlackScholesProcess process = Process();
        double analytic = AnalyticDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value;

        // Suo and Wang's series and Ikeda and Kunitomo's are one image series
        Assert.Equal(analytic, SuoWangDoubleBarrierEngine.Calculate(option, Context, process).NPV!.Value, 1e-12);

        // the Derman-Kani tree at 2,000 steps, within QuantLib's own tolerance for it at 300 (0.033) divided by 10
        double tree = BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniDoubleBarrierOption>(
            option, Context, process, 2000).NPV!.Value;
        Assert.Equal(analytic, tree, 0.0033);
    }

    [Fact]
    public void TheMonteCarloEngineAgreesWithTheClosedForm()
    {
        // 20,000 antithetic paths of 1,000 steps at seed 42, monitored at the steps: within three standard errors of the
        // closed form at the barriers moved out by Broadie, Glasserman and Kou's continuity correction, e^(±βσ√Δt), β
        // 0.5826 ("A continuity correction for discrete barrier options", Mathematical Finance 7, 1997)
        DoubleBarrierOption option = Option(DoubleBarrierType.KnockOut, OptionType.Call);
        GeneralizedBlackScholesProcess process = Process();
        double shift = Math.Exp(0.5826 * 0.25 * Math.Sqrt((182 / 365.0) / 1000));
        var continuous = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0 / shift, 130.0 * shift, 0.0, (StrikedTypePayoff)option.Payoff!, option.Exercise);
        double analytic = AnalyticDoubleBarrierEngine.Calculate(continuous, Context, process).NPV!.Value;
        OneAssetOptionResults mc = MCDoubleBarrierEngine.Calculate<PseudoRandom, GeneralStatistics>(
            option, Context, process, 1000, null, false, true, 20000, null, null, 42);
        Assert.True(
            Math.Abs(mc.NPV!.Value - analytic) <= 3 * mc.ErrorEstimate!.Value, $"{mc.NPV} ± {mc.ErrorEstimate} against {analytic}");
    }

    [Fact]
    public void AtZeroCorrelationTheQuantoAdjustmentIsTheRateDifference()
    {
        // a quanto double barrier at zero correlation is the double barrier on the domestic rate's drift, the dividend
        // yield raised by the foreign rate's excess over the domestic
        var payoff = new PlainVanillaPayoff(OptionType.Call, 100.0);
        var exercise = new EuropeanExercise(Today + 182);
        var quanto = new QuantoDoubleBarrierOption(DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, payoff, exercise);
        var plain = new DoubleBarrierOption(DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, payoff, exercise);
        var vol = new BlackConstantVol(Today, new NullCalendar(), 0.25, A365);
        var process = new BlackScholesMertonProcess(100.0, new FlatForward(Today, 0.01, A365), new FlatForward(Today, 0.05, A365), vol);
        double calculated = QuantoEngine.Calculate(
            quanto, Context, process, new FlatForward(Today, 0.03, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.2, A365), 0.0,
            (o, c, p) => AnalyticDoubleBarrierEngine.Calculate(o, c, p)).NPV!.Value;
        var adjusted = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.01 + 0.05 - 0.03, A365), new FlatForward(Today, 0.05, A365), vol);
        Assert.Equal(AnalyticDoubleBarrierEngine.Calculate(plain, Context, adjusted).NPV!.Value, calculated, 1e-12);
    }

    private static DoubleBarrierOption Option(DoubleBarrierType type, OptionType optionType) =>
        new(type, 80.0, 130.0, 0.0, new PlainVanillaPayoff(optionType, 100.0), new EuropeanExercise(Today + 182));

    private static GeneralizedBlackScholesProcess Process() =>
        new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365),
            new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));
}
