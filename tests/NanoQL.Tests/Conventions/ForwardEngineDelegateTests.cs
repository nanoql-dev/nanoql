// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The forward and forward-performance engines' inner engine (the S-exo plan's ER2 (iii)): a delegate called once per
/// price, on the forward process (the spot, and the curves and volatility implied from the reset date) and on a vanilla
/// option struck at the moneyness times the spot, of the payoff's type and the option's exercise.
/// </summary>
public class ForwardEngineDelegateTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly GeneralizedBlackScholesProcess Process = new BlackScholesMertonProcess(
        100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), 0.25, A365));

    private static readonly ForwardVanillaOption Option = new(
        0.95, Today + 91, new PlainVanillaPayoff(OptionType.Put, 0.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void TheForwardEngineCallsItsInnerEngineOnceOnTheForwardProcess() =>
        Check((o, c, p, inner) => ForwardVanillaEngine.Calculate(o, c, p, inner));

    [Fact]
    public void TheForwardPerformanceEngineCallsItsInnerEngineOnceOnTheForwardProcess() =>
        Check((o, c, p, inner) => ForwardPerformanceVanillaEngine.Calculate(o, c, p, inner));

    private static void Check(
        Func<ForwardVanillaOption, EvaluationContext, GeneralizedBlackScholesProcess,
            Func<VanillaOption, EvaluationContext, GeneralizedBlackScholesProcess, OneAssetOptionResults>,
            OneAssetOptionResults> engine)
    {
        var calls = new List<(VanillaOption Option, GeneralizedBlackScholesProcess Process)>();
        engine(
            Option,
            Context,
            Process,
            (o, c, p) =>
            {
                calls.Add((o, p));
                return AnalyticEuropeanEngine.Calculate(o, c, p);
            });

        (VanillaOption vanilla, GeneralizedBlackScholesProcess forward) = Assert.Single(calls);
        PlainVanillaPayoff payoff = Assert.IsType<PlainVanillaPayoff>(vanilla.Payoff);
        Assert.Equal(OptionType.Put, payoff.OptionType);
        Assert.Equal(95.0, payoff.Strike);
        Assert.Same(Option.Exercise, vanilla.Exercise);

        Assert.Equal(100.0, forward.X0);
        Assert.Equal(Option.ResetDate, forward.RiskFreeRate.ReferenceDate);
        Assert.Equal(Option.ResetDate, forward.DividendYield.ReferenceDate);
        Assert.Equal(Option.ResetDate, forward.BlackVolatility.ReferenceDate);
        Date expiry = Option.Exercise.LastDate();
        Assert.Equal(
            Process.RiskFreeRate.Discount(expiry) / Process.RiskFreeRate.Discount(Option.ResetDate),
            forward.RiskFreeRate.Discount(expiry),
            1e-15);
    }
}
