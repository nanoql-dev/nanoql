// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston analytic engines at the model's time 0, an option expiring on the evaluation date with reference-date
/// events included (the S-hes plan's HR7 (xxxi), under R8). QuantLib's PDF engine window is zero-wide and prices a call
/// at 90 at 0; the time-dependent engine's Gauss-Laguerre integrates sin(φ(x − ln K))/φ, which doesn't decay, and
/// prices it 10.0687; exponential fitting's average variance is 0/0 and it throws "stdDev (-nan(ind)) must be
/// non-negative"; COS's cumulants at t = 0 are rounding noise, so at the forward its bounds test fails and the series
/// prices 1.7e-7 where the intrinsic value is 0. nanoql prices the forward's discounted intrinsic value at the model's
/// time 0 in the PDF, time-dependent, exponential-fitting, COS and expansion engines, before any cumulant or coefficient.
/// The check: QuantLib's COS and expansions off the forward, bit for bit, and its analytic European engine at zero
/// volatility.
/// </summary>
public class HestonModelTimeZeroTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today, IncludeReferenceDateEvents = true };
    private static readonly YieldTermStructure RTS = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly YieldTermStructure QTS = new FlatForward(Today, 0.01, new Actual365Fixed());
    private static readonly HestonModel Model = new(new HestonProcess(RTS, QTS, 100.0, 0.04, 1.5, 0.04, 0.4, -0.6));

    [Fact]
    public void ThePdfTimeDependentAndFittingEnginesPriceTheIntrinsicValue()
    {
        // QuantLib 1.44 [executed on the wheel]: the PDF engine 0 for each; the time-dependent engine 10.068659247616452,
        // 0.08764506434371322 and 10.087645064343704 for the call at 90, the call at 110 and the put at 110; exponential
        // fitting throws "stdDev (-nan(ind)) must be non-negative"
        var ptd = new PiecewiseTimeDependentHestonModel(
            RTS, QTS, 100.0, 0.04, new ConstantParameter(0.04, new PositiveConstraint()), new ConstantParameter(1.5, new PositiveConstraint()),
            new ConstantParameter(0.4, new PositiveConstraint()), new ConstantParameter(-0.6, new BoundaryConstraint(-1.0, 1.0)), new TimeGrid(2.0, 4));
        foreach ((OptionType type, double strike) in new[] { (OptionType.Call, 90.0), (OptionType.Call, 110.0), (OptionType.Put, 110.0), (OptionType.Put, 90.0) })
        {
            VanillaOption option = Expiring(type, strike);
            double intrinsic = IntrinsicAtZeroVolatility(option);
            Assert.Equal(intrinsic, AnalyticPDFHestonEngine.Calculate(option, Context, Model).NPV);
            Assert.Equal(intrinsic, AnalyticPTDHestonEngine.Calculate(option, Context, ptd).NPV);
            Assert.Equal(intrinsic, ExponentialFittingHestonEngine.Calculate(option, Context, Model).NPV);
        }
    }

    [Fact]
    public void OffTheForwardCosAndTheExpansionsAreQuantLibs()
    {
        // QuantLib 1.44's COS engine and its LPP2, LPP3 and Forde expansions price 10, 0, 10 and 0 [executed on the
        // wheel and the harness]
        foreach ((OptionType type, double strike, double quantLib) in new[]
                 {
                     (OptionType.Call, 90.0, 10.0), (OptionType.Call, 110.0, 0.0), (OptionType.Put, 110.0, 10.0), (OptionType.Put, 90.0, 0.0),
                 })
        {
            VanillaOption option = Expiring(type, strike);
            Assert.Equal(quantLib, COSHestonEngine.Calculate(option, Context, Model).NPV);
            foreach (HestonExpansionEngineHestonExpansionFormula formula in Enum.GetValues<HestonExpansionEngineHestonExpansionFormula>())
            {
                Assert.Equal(quantLib, HestonExpansionEngine.Calculate(option, Context, Model, formula).NPV);
            }

            Assert.Equal(IntrinsicAtZeroVolatility(option), quantLib);
        }
    }

    [Fact]
    public void AtTheForwardCosPricesTheIntrinsicZero()
    {
        // QuantLib 1.44's COS engine prices 1.7182536282689398e-07 for the call and 1.718253647292302e-07 for the put
        // at 100 [executed on the wheel]
        Assert.Equal(0.0, COSHestonEngine.Calculate(Expiring(OptionType.Call, 100.0), Context, Model).NPV);
        Assert.Equal(0.0, COSHestonEngine.Calculate(Expiring(OptionType.Put, 100.0), Context, Model).NPV);
    }

    private static VanillaOption Expiring(OptionType type, double strike) =>
        new(new PlainVanillaPayoff(type, strike), new EuropeanExercise(Today));

    // QuantLib's analytic European engine at zero volatility on the model's curves
    private static double IntrinsicAtZeroVolatility(VanillaOption option) =>
        AnalyticEuropeanEngine.Calculate(
            option, Context, new BlackScholesMertonProcess(100.0, QTS, RTS, new BlackConstantVol(Today, new NullCalendar(), 0.0, new Actual365Fixed()))).NPV!.Value;
}
