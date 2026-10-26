// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;
using static NanoQL.Tests.Divergences.TwoAssetMarket;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Gaussian copula spread engine where a volatility accrues no variance to expiry: at a zero volatility, and at an
/// expiry on the volatility's own date. QuantLib reads the smile at expiry through its density calculator, which
/// refuses a smile with no variance ("non-positive at-the-money volatility", <c>gaussiancopulaspreadengine.cpp</c>,
/// <c>smilesectionrndcalculator.cpp</c>), where Kirk's and the other spread engines price both; nanoql puts that asset
/// at its forward (the S-exo plan's ER5 (xviii) at its boundary, and R8). The checks: Kirk's engine, exact at a zero
/// second volatility, and Black's put on the second asset at a zero first volatility, each within the engine's
/// quadrature (QuantLib's own copula cases hold 1e-3), and the engine's own value at a volatility of 1e-6, which
/// QuantLib's prices too, within 1e-5; the deterministic value at both zero and at an expiry on the volatilities' date;
/// Kirk's value at an expiry before it. A surface whose strikes stop below the forward prices QuantLib's bits: the
/// test for no variance is the density calculator's own, at the forward on the smile section, which reads every strike
/// with extrapolation.
/// </summary>
public class GaussianCopulaZeroVarianceTests
{
    private static readonly YieldTermStructure Shared = Flat(0.05);
    private static readonly double Df = Shared.Discount(Today + 365);

    [Fact]
    public void AZeroSecondVolatilityMeetsKirk()
    {
        var p1 = Process(100.0, 0.02, Shared, 0.3);
        var p2 = Process(95.0, 0.03, Shared, 0.0);
        BasketOption call = Spread(OptionType.Call, 5.0);
        double kirk = KirkEngine.Calculate(call, Context, p1, p2, 0.5).NPV!.Value;
        double zero = Copula(call, p1, p2);
        Assert.Equal(kirk, zero, 1e-3 * kirk);

        // the engine's own value at a volatility it prices, as QuantLib's does from 1e-6
        Assert.Equal(Copula(call, p1, Process(95.0, 0.03, Shared, 1e-6)), zero, 1e-5 * zero);
    }

    [Fact]
    public void AZeroFirstVolatilityIsBlacksPutOnTheSecond()
    {
        var p1 = Process(100.0, 0.02, Shared, 0.0);
        var p2 = Process(95.0, 0.03, Shared, 0.3);
        BasketOption call = Spread(OptionType.Call, 5.0);
        double f1 = 100.0 * Flat(0.02).Discount(Today + 365) / Df;
        double f2 = 95.0 * Flat(0.03).Discount(Today + 365) / Df;
        double expected = BlackFormula(OptionType.Put, f1 - 5.0, f2, 0.3, Df);
        double zero = Copula(call, p1, p2);
        Assert.Equal(expected, zero, 1e-3 * expected);
        Assert.Equal(Copula(call, Process(100.0, 0.02, Shared, 1e-6), p2), zero, 1e-5 * zero);
    }

    [Fact]
    public void BothZeroAreTheDeterministicValue()
    {
        var p1 = Process(100.0, 0.02, Shared, 0.0);
        var p2 = Process(95.0, 0.03, Shared, 0.0);
        BasketOption call = Spread(OptionType.Call, 5.0);
        double f1 = 100.0 * Flat(0.02).Discount(Today + 365) / Df;
        double f2 = 95.0 * Flat(0.03).Discount(Today + 365) / Df;
        Assert.Equal(Df * Math.Max(f1 - f2 - 5.0, 0.0), Copula(call, p1, p2), 1e-12);
    }

    [Fact]
    public void AnExpiryOnTheVolatilitiesDateIsAtTheForwards()
    {
        // both volatilities dated two days on, the option expiring then: no variance accrued, the deterministic value
        BlackVolTermStructure later = new BlackConstantVol(Today + 2, new NullCalendar(), 0.3, A365);
        var p1 = new Processes.BlackScholesMertonProcess(100.0, Flat(0.0), Shared, later);
        var p2 = new Processes.BlackScholesMertonProcess(100.0, Flat(0.0), Shared, later);
        var option = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, -0.01)), new EuropeanExercise(Today + 2));
        double d = Shared.Discount(Today + 2);
        double expected = d * Math.Max((100.0 / d) - (100.0 / d) + 0.01, 0.0);
        Assert.Equal(expected, Copula(option, p1, p2), 1e-12);
        Assert.Equal(KirkEngine.Calculate(option, Context, p1, p2, 0.5).NPV!.Value, Copula(option, p1, p2), 1e-12);
    }

    [Fact]
    public void AnExpiryBeforeTheVolatilitiesDateIsAtTheForwards()
    {
        // both volatilities dated two days on, the option expiring the day before: Kirk's value at no variance
        BlackVolTermStructure later = new BlackConstantVol(Today + 2, new NullCalendar(), 0.3, A365);
        var p1 = new Processes.BlackScholesMertonProcess(100.0, Flat(0.02), Shared, later);
        var p2 = new Processes.BlackScholesMertonProcess(95.0, Flat(0.03), Shared, later);
        var option = new BasketOption(
            new SpreadBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 5.0)), new EuropeanExercise(Today + 1));
        Assert.Equal(KirkEngine.Calculate(option, Context, p1, p2, 0.5).NPV!.Value, Copula(option, p1, p2), 1e-12);
    }

    [Fact]
    public void ASurfaceWhoseStrikesStopBelowTheForwardIsQuantLibs()
    {
        // flat surfaces on strikes 80 to 100 under asset 1's forward of about 103.05: the smile section reads them
        // with extrapolation, as QuantLib's, so only a smile with no deviation at the forward goes to the forward
        BlackVolTermStructure Surface(double vol) =>
            new BlackVarianceSurface(
                Today, new NullCalendar(), [Today + 180, Today + 730], [80.0, 90.0, 100.0], new Numerics.Matrix(3, 2, vol), A365);
        var p1 = new Processes.BlackScholesMertonProcess(100.0, Flat(0.02), Shared, Surface(0.3));
        var p2 = new Processes.BlackScholesMertonProcess(95.0, Flat(0.03), Shared, Surface(0.25));
        Assert.Equal(11.161506642992917, Copula(Spread(OptionType.Call, 5.0), p1, p2));
    }

    private static double Copula(
        BasketOption option, Processes.GeneralizedBlackScholesProcess p1, Processes.GeneralizedBlackScholesProcess p2) =>
        GaussianCopulaSpreadEngine.Calculate(option, Context, p1, p2, 0.5).NPV!.Value;
}
