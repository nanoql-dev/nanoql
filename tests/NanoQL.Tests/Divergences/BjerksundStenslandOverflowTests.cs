// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Bjerksund-Stensland's phi family where a power overflows against an underflowing normal tail. <c>phi</c> returns
/// e^λ·[N(d) − (I/S)^κ·N(d − 2 ln(I/S)/√v)] (<c>bjerksundstenslandengine.cpp:37-48</c>); on puts with q ≫ r and a long
/// expiry the symmetric call's (I/S)^κ overflows while the N factor underflows, the product is NaN, and the European
/// floor misses it: QuantLib prices puts at σ 5%, 30 years, r 0.1%, q 30% NaN (QuantLib-Python 1.44). The Greeks'
/// functions carry the same powers beside erfc and Gaussian densities. nanoql takes each such product as one exponent,
/// through log N's tail, where the power isn't finite or the tail is 0. QuantLib's check: its FD engine (2,000 × 2,000)
/// on the value, delta and gamma; the other Greeks against the engine's own NPVs bumped by ±1e-6; QuantLib's bits where
/// the guard doesn't fire.
/// </summary>
public class BjerksundStenslandOverflowTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Theory]
    [InlineData(50.0, 48.782860504849396, -0.001624718871349201, 1.630130027236505e-05)] // QuantLib NaN
    [InlineData(100.0, 97.79121408147223, -0.003256947815862007, 3.267795201858733e-05)] // NaN
    [InlineData(200.0, 196.03446083390384, -0.006528950632269873, 6.550695409600497e-05)] // NaN
    public void TheValueDeltaAndGammaAreTheFdEngines(double strike, double value, double delta, double gamma)
    {
        OneAssetOptionResults results = Price(strike, 0.30, 0.001, 0.05);
        AssertClose(value, results.NPV!.Value, 1e-6);
        AssertClose(delta, results.Delta!.Value, 1e-6);
        AssertClose(gamma, results.Gamma!.Value, 1e-6);
    }

    [Fact]
    public void TheOtherGreeksAreTheBumpedValues()
    {
        // the put at 100: vega, rho and dividend rho against central differences of the engine's own NPVs
        const double h = 1e-6;
        OneAssetOptionResults results = Price(100.0, 0.30, 0.001, 0.05);
        double Npv(double q, double r, double vol) => Price(100.0, q, r, vol).NPV!.Value;
        AssertClose((Npv(0.30, 0.001, 0.05 + h) - Npv(0.30, 0.001, 0.05 - h)) / (2 * h), results.Vega!.Value, 1e-4);
        AssertClose((Npv(0.30, 0.001 + h, 0.05) - Npv(0.30, 0.001 - h, 0.05)) / (2 * h), results.Rho!.Value, 1e-4);
        AssertClose((Npv(0.30 + h, 0.001, 0.05) - Npv(0.30 - h, 0.001, 0.05)) / (2 * h), results.DividendRho!.Value, 1e-4);
    }

    [Fact]
    public void WhereTheGuardDoesntFireTheBitsAreQuantLibs()
    {
        // q 10%, ten years, the put at 100: QuantLib's value and Greeks
        var results = BjerksundStenslandApproximationEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new AmericanExercise(Today, Today + 3650)),
            Context,
            Process(0.10, 0.001, 0.05));
        Assert.Equal(62.217039258049404, results.NPV!.Value);
        Assert.Equal(-0.3678794410552049, results.Delta!.Value);
        Assert.Equal(4.6582602000559974e-11, results.Gamma!.Value);
        Assert.Equal(2.3291301000280018e-07, results.Vega!.Value);
        Assert.Equal(-990.049833635699, results.Rho!.Value);
        Assert.Equal(367.879441055205, results.DividendRho!.Value);
        Assert.Equal(-3.579789427770765, results.Theta!.Value);
    }

    private static OneAssetOptionResults Price(double strike, double q, double r, double vol) =>
        BjerksundStenslandApproximationEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Put, strike), new AmericanExercise(Today, Today + 10950)),
            Context,
            Process(q, r, vol));

    private static GeneralizedBlackScholesProcess Process(double q, double r, double vol) =>
        new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, q, A365), new FlatForward(Today, r, A365), new BlackConstantVol(Today, new NullCalendar(), vol, A365));

    private static void AssertClose(double expected, double calculated, double relative) =>
        Assert.True(
            Math.Abs(calculated - expected) <= relative * Math.Abs(expected), $"{calculated} against {expected}");
}
