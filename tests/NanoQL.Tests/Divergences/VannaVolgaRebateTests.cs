// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Vanna-Volga engine with a rebate (fixed). QuantLib prices the out option with its rebate and the in option by
/// parity, <c>vanilla − outPrice</c>, capping <c>outPrice</c> at the vanilla
/// (<c>vannavolgabarrierengine.cpp:174-193</c>, <c>:326-339</c>), and gives an out option beyond the barrier 0
/// (<c>:141-168</c>): so a knock-in loses its own rebate and subtracts the knock-out's, a capped knock-out loses its
/// rebate, and a knock-out at the barrier loses its rebate too. On a flat smile, where the method's adjustment vanishes
/// and the engine should give the analytic engine's price, a 0.05 rebate leaves an up-and-in at 1.40, strike 1.25, at
/// 0.06807979413354334 against 0.11711444573142975 (QuantLib-Python 1.44). nanoql prices each option's rebate-free part
/// and bounds it as QuantLib does, then adds its own rebate: an out option's smile-adjusted rebate at the hit
/// (QuantLib's own price where no bound binds), an in option's rebate at expiry by the analytic engine; with the spot
/// at or beyond the barrier an out option is its rebate, due now, and an in option QuantLib's vanilla. A zero rebate
/// keeps QuantLib's bits. QuantLib's checks: its own engine's rebate-free parts and uncapped prices, and its analytic
/// engine.
/// </summary>
public class VannaVolgaRebateTests
{
    private static readonly Date Today = new(15, June, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter Dc = new Actual365Fixed();
    private static readonly (double Put, double Atm, double Call) Flat = (0.12, 0.12, 0.12);
    private static readonly (double Put, double Atm, double Call) Skew = (0.13, 0.12, 0.115);

    [Fact]
    public void OnAFlatSmileAKnockInWithARebateIsTheAnalyticEnginesPrice()
    {
        // QuantLib: 0.06807979413354334 and 0, its engine's gap at a zero rebate 9.1e-8 and 9.1e-8
        Assert.Equal(
            Analytic(BarrierType.UpIn, 1.40, 1.25, 0.05), VannaVolga(BarrierType.UpIn, 1.40, 1.25, 0.05, Flat), 1e-7);
        Assert.Equal(
            Analytic(BarrierType.DownIn, 1.20, 1.25, 0.05),
            VannaVolga(BarrierType.DownIn, 1.20, 1.25, 0.05, Flat),
            1e-7);
        Assert.Equal(
            Analytic(BarrierType.UpIn, 1.40, 1.35, 0.05), VannaVolga(BarrierType.UpIn, 1.40, 1.35, 0.05, Flat), 1e-15);
        Assert.Equal(0.11711444573142975, Analytic(BarrierType.UpIn, 1.40, 1.25, 0.05));
    }

    [Fact]
    public void OnAFlatSmileAKnockOutQuantLibCapsIsTheAnalyticEnginesPrice()
    {
        // QuantLib: 0.05143341298826267, the vanilla, its cap binding the rebate-carrying price; and
        // 0.10415067189095054
        Assert.Equal(0.07075243126100261, VannaVolga(BarrierType.DownOut, 1.20, 1.35, 0.05, Flat), 1e-15);
        Assert.Equal(0.11385777657720902, VannaVolga(BarrierType.DownOut, 1.20, 1.25, 0.05, Flat), 1e-15);
        Assert.Equal(0.07075243126100261, Analytic(BarrierType.DownOut, 1.20, 1.35, 0.05));
    }

    [Fact]
    public void OnASkewedSmileAKnockInIsQuantLibsRebateFreePriceAndItsRebateAtExpiry()
    {
        // QuantLib's rebate-free knock-ins on the skew, and its analytic engine's value of the rebate at expiry
        (
            double Barrier,
            double Strike,
            double RebateFree)[] cases = [(1.40, 1.25, 0.09947006283561127), (1.40, 1.35, 0.05064727804549089)];
        foreach (var c in cases)
        {
            double atExpiry = Analytic(
                BarrierType.UpIn, c.Barrier, c.Strike, 0.05) - Analytic(BarrierType.UpIn, c.Barrier, c.Strike, 0.0);
            Assert.Equal(c.RebateFree, VannaVolga(BarrierType.UpIn, c.Barrier, c.Strike, 0.0, Skew), 1e-15);
            Assert.Equal(c.RebateFree + atExpiry, VannaVolga(BarrierType.UpIn, c.Barrier, c.Strike, 0.05, Skew), 1e-15);
        }
    }

    [Fact]
    public void AtOrBeyondTheBarrierAKnockOutIsItsRebateAndAKnockInTheVanilla()
    {
        // an up barrier at 1.25 below the spot of 1.30: QuantLib 0 for the knock-out, and its vanilla for the knock-in
        Assert.Equal(0.05, VannaVolga(BarrierType.UpOut, 1.25, 1.20, 0.05, Flat));
        Assert.Equal(0.13938089728229447, VannaVolga(BarrierType.UpIn, 1.25, 1.20, 0.05, Flat), 1e-15);
    }

    [Fact]
    public void AnUncappedKnockOutWithARebateIsQuantLibsOwnPrice()
    {
        // QuantLib's own price with the rebate: no bound binds its rebate-free price
        Assert.Equal(0.03678328311867751, VannaVolga(BarrierType.UpOut, 1.40, 1.25, 0.05, Skew), 1e-15);
        Assert.Equal(0.03607087775740719, VannaVolga(BarrierType.UpOut, 1.40, 1.25, 0.05, Flat), 1e-15);
    }

    [Fact]
    public void AZeroRebateKeepsQuantLibsValues()
    {
        // QuantLib's engine at a zero rebate, on the skew
        (BarrierType Type, double Barrier, double Strike, double QuantLib)[] cases =
        [
            (BarrierType.UpOut, 1.40, 1.25, 0.007989598182293868),
            (BarrierType.UpIn, 1.40, 1.35, 0.05064727804549089),
            (BarrierType.DownOut, 1.20, 1.25, 0.08935995783506588),
            (BarrierType.DownOut, 1.20, 1.35, 0.0447030841634671),
            (BarrierType.DownIn, 1.20, 1.25, 0.018099703182839266),
            (BarrierType.DownIn, 1.20, 1.35, 0.006273363468901762),
        ];
        foreach (var c in cases)
        {
            Assert.Equal(c.QuantLib, VannaVolga(c.Type, c.Barrier, c.Strike, 0.0, Skew), 1e-15);
        }
    }

    // A year from 15 June 2026, spot 1.30, spot-delta quotes with a delta-neutral ATM one; domestic 3% and foreign 1%
    // Actual/360 rates on Actual/365 Fixed curves.
    private static double VannaVolga(
        BarrierType type, double barrier, double strike, double rebate, (double Put, double Atm, double Call) vols) =>
        VannaVolgaBarrierEngine.Calculate(
            new BarrierOption(
                type,
                barrier,
                rebate,
                new PlainVanillaPayoff(OptionType.Call, strike),
                new EuropeanExercise(Today + 365)),
            Context,
            new DeltaVolQuote(vols.Atm, DeltaVolQuoteDeltaType.Spot, 1.0, DeltaVolQuoteAtmType.AtmDeltaNeutral),
            new DeltaVolQuote(-0.25, vols.Put, 1.0, DeltaVolQuoteDeltaType.Spot),
            new DeltaVolQuote(0.25, vols.Call, 1.0, DeltaVolQuoteDeltaType.Spot),
            1.30,
            Domestic(),
            Foreign()).NPV!.Value;

    // The analytic engine at the ATM volatility, 12%.
    private static double Analytic(BarrierType type, double barrier, double strike, double rebate) =>
        AnalyticBarrierEngine.Calculate(
            new BarrierOption(
                type,
                barrier,
                rebate,
                new PlainVanillaPayoff(OptionType.Call, strike),
                new EuropeanExercise(Today + 365)),
            Context,
            new BlackScholesMertonProcess(
                1.30, Foreign(), Domestic(), new BlackConstantVol(Today, new NullCalendar(), 0.12, Dc))).NPV!.Value;

    private static FlatForward Domestic() => new(Today, 0.03 * 365.0 / 360.0, Dc);

    private static FlatForward Foreign() => new(Today, 0.01 * 365.0 / 360.0, Dc);
}
