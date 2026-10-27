// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Asian engines on structures that measure time differently (the S-bar plan's BR5 (xxvi), batch 4's dates class,
/// fixed under R8). QuantLib's engines read each structure on its own day counter's time as though they shared one
/// clock, so on an Actual/360 risk-free curve (r 5%) beside Actual/365 Fixed dividends (2%) and volatility (25%) they
/// are off their own values on the market restated onto one clock with the discounts and variances by date unchanged:
/// a call at 100 on twelve fixings every 30 days by the FD engine +0.478%, by the arithmetic Monte Carlo +0.469%, Levy's
/// continuous call over a year +0.482%, and the Heston geometric call (v₀ = θ 0.04, κ 2, vol of vol 0.5, ρ −0.5)
/// −0.174% (the S-bar run's <c>c3_mismatch_ql.py</c>). nanoql runs the FD and Monte Carlo engines on
/// <c>FdmBlackScholesOp.Clock</c>, Levy on the volatility's clock, and the Heston engine on <c>FdmHestonOp.Clock</c>,
/// each structure read at the instants its times denote. Check: each engine on the restated market, to rounding for the
/// grids and paths (the same numbers drawn) and within R7 for the integrals.
/// </summary>
public class AsianMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A360 = new Actual360();
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly Date[] Monthly = [.. Enumerable.Range(1, 12).Select(i => Today + (30 * i))];

    private static readonly GeneralizedBlackScholesProcess Mismatched = Market(new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A360), 0.25, A365);

    // onto the risk-free curve's clock, the dividend rate and the volatility scaled
    private static readonly GeneralizedBlackScholesProcess On360 =
        Market(new FlatForward(Today, 0.02 * 360.0 / 365.0, A360), new FlatForward(Today, 0.05, A360), 0.25 * Math.Sqrt(360.0 / 365.0), A360);

    // onto the volatility's clock, the risk-free rate scaled
    private static readonly GeneralizedBlackScholesProcess On365 =
        Market(new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05 * 365.0 / 360.0, A365), 0.25, A365);

    private static readonly DiscreteAveragingAsianOption Arithmetic = new(
        AverageType.Arithmetic, 0.0, 0, Monthly, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Monthly[^1]));

    [Fact]
    public void TheFiniteDifferenceEnginePricesAsOnTheRestatedMarket() =>
        Assert.Equal(
            FdBlackScholesAsianEngine.Calculate(Arithmetic, Context, On360).NPV!.Value,
            FdBlackScholesAsianEngine.Calculate(Arithmetic, Context, Mismatched).NPV!.Value,
            1e-11);

    [Fact]
    public void TheMonteCarloEnginePricesAsOnTheRestatedMarket()
    {
        double Price(GeneralizedBlackScholesProcess p) => MCDiscreteArithmeticAPEngine.Calculate<PseudoRandom, GeneralStatistics>(
            Arithmetic, Context, p, false, false, false, 20000, null, null, 42).NPV!.Value;
        Assert.Equal(Price(On360), Price(Mismatched), 1e-11);
    }

    [Fact]
    public void LevyPricesAsOnTheRestatedMarket()
    {
        var option = new ContinuousAveragingAsianOption(
            AverageType.Arithmetic, Today, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        double restated = ContinuousArithmeticAsianLevyEngine.Calculate(option, Context, On365, 100.0).NPV!.Value;
        Assert.Equal(restated, ContinuousArithmeticAsianLevyEngine.Calculate(option, Context, Mismatched, 100.0).NPV!.Value, 1e-10 * restated);
    }

    [Fact]
    public void TheHestonGeometricEnginePricesAsOnTheRestatedMarket()
    {
        var option = new DiscreteAveragingAsianOption(
            AverageType.Geometric, 1.0, 0, Monthly, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Monthly[^1]));
        HestonProcess Heston(YieldTermStructure dividends) => new(new FlatForward(Today, 0.05, A360), dividends, 100.0, 0.04, 2.0, 0.04, 0.5, -0.5);
        double restated = AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(
            option, Context, Heston(new FlatForward(Today, 0.02 * 360.0 / 365.0, A360))).NPV!.Value;
        double mismatched = AnalyticDiscreteGeometricAveragePriceAsianHestonEngine.Calculate(option, Context, Heston(new FlatForward(Today, 0.02, A365))).NPV!.Value;
        Assert.Equal(restated, mismatched, 1e-10 * restated);
    }

    private static GeneralizedBlackScholesProcess Market(YieldTermStructure dividends, YieldTermStructure riskFree, double vol, DayCounter volDc) =>
        new BlackScholesMertonProcess(100.0, dividends, riskFree, new BlackConstantVol(Today, new NullCalendar(), vol, volDc));
}
