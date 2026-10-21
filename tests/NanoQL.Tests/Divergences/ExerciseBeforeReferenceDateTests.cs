// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Options expiring on or before a structure's reference date. QuantLib's analytic, binomial and Heston engines read
/// each structure at the exercise date, so a put expiring between the evaluation date and a risk-free curve settling
/// T+2 throws "negative time"; on the curve's own date the Heston engine's integrations read 0/0
/// (<c>analytichestonengine.cpp:644-674</c>) and the dividend engine's theta is 0. QuantLib's finite-difference engine
/// on the market restated from today, every structure accruing nothing before its own date, prices them, as nanoql's
/// does on its clock (the user's rulings). nanoql's other engines read each structure as accruing nothing before its
/// own date, so each prices them as QuantLib's engine prices the restated market: the analytic engines' value and
/// Greeks, and the binomial engine's value, delta, gamma and theta; the Heston engine's model time runs from the
/// earliest curve date (<c>HestonMismatchedTimesTests</c>), and at its time 0, on or before every curve's date, its
/// value is the forward's intrinsic value, as QuantLib's European engine reads it at zero volatility. The rho of an
/// option expiring before the risk-free curve's date is 0, as no risk-free rate reaches it. Where every structure is
/// dated after the exercise, the finite-difference and binomial engines' clocks have no time for it, and they refuse it
/// as QuantLib's do on structures dated together.
/// </summary>
public class ExerciseBeforeReferenceDateTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void TheAnalyticEnginesPriceAPutExpiringInTheGap()
    {
        // QuantLib's European engine on the restated market: value, delta, gamma, vega, dividend rho, theta and theta
        // per day, the dividend engine its first five
        double[] restated = [0.5247660342196738, -0.49903298414677266, 0.30485371213640083, 2.0880391242219227, 0.1367213655196618, -96.26485101091886, -0.26373931783813387];
        OneAssetOptionResults e = AnalyticEuropeanEngine.Calculate(Put(1), Context, Process());
        AssertAll(restated, [e.NPV, e.Delta, e.Gamma, e.Vega, e.DividendRho, e.Theta, e.ThetaPerDay]);
        Assert.Equal(0.0, e.Rho!.Value);

        OneAssetOptionResults d = AnalyticDividendEuropeanEngine.Calculate(Put(1), Context, Process(), ImmutableArray<Dividend>.Empty);
        AssertAll([restated[0], restated[1], restated[2], restated[3], restated[5]], [d.NPV, d.Delta, d.Gamma, d.Vega, d.Theta]);
        Assert.Equal(0.0, d.Rho!.Value);
    }

    [Fact]
    public void AVolatilityDatedAfterTheExerciseAccruesNoVariance()
    {
        // the volatility settling T+2 beside the risk-free curve and q dated today, a call at 100 expiring tomorrow:
        // QuantLib's European engine on the restated market, the variance 0 until 19 October: value, delta, theta, rho
        // and dividend rho; on the market as given it throws "date before reference date"
        BlackConstantVol settling = BlackConstantVol.Create(2, new TARGET(), 0.25, A365, Context);
        Assert.Equal(Today + 4, settling.ReferenceDate);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.02, A365), new FlatForward(Today, 0.05, A365), settling);
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 1));
        OneAssetOptionResults e = AnalyticEuropeanEngine.Calculate(call, Context, process);
        AssertAll(
            [0.008218389981928675, 0.9999452069806444, -2.9994247014446054, 0.2739350748166644, -0.2739575909536012],
            [e.NPV, e.Delta, e.Theta, e.Rho, e.DividendRho]);
    }

    [Fact]
    public void TheBinomialEnginePricesAPutExpiringInTheGap()
    {
        // QuantLib's Cox-Ross-Rubinstein engine (801 steps) on the restated market, its theta at the restated risk-free
        // curve's rate at today, 0, and its value, delta, gamma and theta on and after the curve's date likewise
        AssertAll(
            [0.5249289790893119, -0.4990306390942574, 0.30494887037394336, -96.29458327005177],
            Binomial(1));
        AssertAll(
            [1.055255582813141, -0.49800687829021706, 0.15244809762559597, -48.63604426458512],
            Binomial(4));
    }

    [Fact]
    public void TheHestonEnginePricesTheForwardsIntrinsicValueAtItsTimeZero()
    {
        // both curves settling T+2, a put at 101 expiring on that date, the model's time 0, and the day after today,
        // before it: QuantLib's European engine at zero volatility as given and on the restated market, the intrinsic
        // value on the forward 100, undiscounted, where QuantLib's Heston engine reads 0/0 and throws "negative time"
        var model = new HestonModel(new HestonProcess(
            Settling(), FlatForward.Create(2, new TARGET(), 0.02, A365, Context), 100.0, 0.0625, 1.5, 0.0625, 0.4, -0.6));
        foreach (int days in new[] { 4, 1 })
        {
            var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 101.0), new EuropeanExercise(Today + days));
            Assert.Equal(1.0, AnalyticHestonEngine.Calculate(put, Context, model, 192).NPV!.Value, 1e-15);
        }
    }

    [Fact]
    public void TheFiniteDifferenceEnginePricesItOnItsClock()
    {
        // QuantLib's engine (400 x 400) on the restated market: value, delta, gamma and theta
        OneAssetOptionResults fd = new MakeFdBlackScholesVanillaEngine(Process()).WithTGrid(400).WithXGrid(400).ToEngine()(Put(1), Context);
        AssertAll([0.5247791263836017, -0.4990493797347693, 0.30486302774755236, -174.502390886714], [fd.NPV, fd.Delta, fd.Gamma, fd.Theta]);
    }

    // The risk-free curve 5% settling T+2 (19 October), q 2% and volatility 25% dated today
    private static BlackScholesMertonProcess Process() =>
        new(100.0, new FlatForward(Today, 0.02, A365), Settling(), new BlackConstantVol(Today, null, 0.25, A365));

    private static YieldTermStructure Settling()
    {
        YieldTermStructure settling = FlatForward.Create(2, new TARGET(), 0.05, A365, Context);
        Assert.Equal(Today + 4, settling.ReferenceDate);
        return settling;
    }

    private static VanillaOption Put(int days) =>
        new(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + days));

    private static double?[] Binomial(int days)
    {
        OneAssetOptionResults c = BinomialCRRVanillaEngine.Calculate(Put(days), Context, Process(), 801);
        return [c.NPV, c.Delta, c.Gamma, c.Theta];
    }

    private static void AssertAll(double[] expected, double?[] actual)
    {
        for (int i = 0; i < expected.Length; ++i)
        {
            Assert.Equal(0.0, (actual[i]!.Value / expected[i]) - 1.0, 1e-12);
        }
    }
}
