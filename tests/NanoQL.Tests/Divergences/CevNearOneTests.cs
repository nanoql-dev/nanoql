// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CEV engine and density near β = 1. At β = 1 <c>CEVCalculator</c>'s δ = (1 − 2β)/(1 − β) divides by zero
/// (<c>analyticcevengine.cpp:36</c>) and Boost refuses the infinite degrees of freedom; near it Boost's non-central
/// chi-squared refuses or runs for seconds, from |β − 1| ≤ 1e-4 at σ 5% and 0.1 years to 1e-6 at σ 80% and 10 years;
/// <c>CEVRNDCalculator</c> refuses β = 1 (<c>cevrndcalculator.cpp:39</c>). nanoql prices Black's formula at Hagan's
/// CEV-equivalent volatility (the SABR volatility at ν = 0) for |β − 1| &lt; 1e-3, exactly Black's at β = 1; its
/// density refuses the band but β = 1, where it is the lognormal density. QuantLib's check: its CEV formula where it
/// still prices inside the band, and at the band's edge (QuantLib-Python 1.44; f0 100, σ 20%, r 3% on Actual/365
/// (Fixed), from 15 October 2026).
/// </summary>
public class CevNearOneTests
{
    private const double F0 = 100.0;

    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Discount = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Theory]
    [InlineData(1 - 9.99e-4, 100.0, 7.730149371974239)]
    [InlineData(1 - 9.99e-4, 120.0, 2.083329302794668)]
    [InlineData(1 - 5e-4, 100.0, 7.7301493624835)]
    [InlineData(1 - 5e-4, 120.0, 2.083582656584919)]
    [InlineData(1 + 5e-4, 100.0, 7.730149362484065)]
    [InlineData(1 + 5e-4, 120.0, 2.0840904450172926)]
    [InlineData(1 + 9.99e-4, 100.0, 7.730149371974521)]
    [InlineData(1 + 9.99e-4, 120.0, 2.0843438640852128)]
    public void InTheBandTheValueIsBlackAtHagansVolatility(double beta, double strike, double quantLib)
    {
        // a year's call; QuantLib's CEV formula still prices here, and Black at Hagan's volatility is within 1e-10 of it
        double alpha = 0.2 * Math.Pow(F0, 1 - beta);
        double value = Price(OptionType.Call, strike, 365, alpha, beta);
        double t = Discount.TimeFromReference(Today + 365);
        double hagan = BlackFormulaFunctions.BlackFormula(
            OptionType.Call, strike, F0, SabrFunctions.UnsafeSabrLogNormalVolatility(strike, F0, t, alpha, beta, 0.0, 0.0)
            * Math.Sqrt(t)) * Discount.Discount(Today + 365);
        Assert.Equal(hagan, value);
        Assert.True(Math.Abs(value - quantLib) <= 1e-10 * quantLib, $"{value} against QuantLib's {quantLib}");
    }

    [Theory]
    [InlineData(80.0)]
    [InlineData(100.0)]
    [InlineData(120.0)]
    public void AtBetaOneTheValueIsBlacks(double strike)
    {
        // QuantLib refuses: "boost::math::non_central_chi_squared_distribution<double>::
        // non_central_chi_squared_distribution(double,double): Degrees of freedom argument is inf, but must be > 0 !"
        double value = Price(OptionType.Call, strike, 365, 0.2, 1.0);
        double t = Discount.TimeFromReference(Today + 365);
        Assert.Equal(
            BlackFormulaFunctions.BlackFormula(OptionType.Call, strike, F0, 0.2 * Math.Sqrt(t)) * Discount.Discount(Today + 365),
            value);
    }

    [Theory]
    [InlineData(1 - 5e-4)]
    [InlineData(1 + 5e-4)]
    [InlineData(1 + 9.99e-4)]
    public void TheDensityRefusesTheBand(double beta)
    {
        // QuantLib's cdf at f 100 and a year reads 0.5398079896661454, 0.5398476849209493 and 0.5398674929021304 here,
        // where its non-central chi-squared still converges; further in it refuses or runs for seconds
        var e = Assert.Throws<NanoQLException>(() => new CEVRNDCalculator(F0, 0.2 * Math.Pow(F0, 1 - beta), beta));
        Assert.Equal("the CEV density near beta = 1 is outside the non-central chi-squared's range", e.Message);
    }

    [Fact]
    public void AtBetaOneTheDensityIsLognormal()
    {
        // QuantLib refuses: "beta can not be one". The driftless lognormal at σ = α: ln f_t ~ N(ln f0 − α²t/2, α²t)
        var calculator = new CEVRNDCalculator(F0, 0.2, 1.0);
        const double t = 1.5;
        double s = 0.2 * Math.Sqrt(t);
        Assert.Equal(0.0, calculator.MassAtZero(t));
        foreach (double f in new[] { 50.0, 90.0, 100.0, 130.0, 250.0 })
        {
            double d = (Math.Log(f / F0) + (0.5 * s * s)) / s;
            Assert.Equal(Math.Exp(-0.5 * d * d) / (f * s * Math.Sqrt(2.0 * Math.PI)), calculator.Pdf(f, t), 1e-15);
            Assert.Equal(new NanoQL.Numerics.MaddockCumulativeNormal().Evaluate(d), calculator.Cdf(f, t), 1e-15);
        }
    }

    [Fact]
    public void AtBetaOneTheDensitysInverseIsTheLognormals()
    {
        var calculator = new CEVRNDCalculator(F0, 0.2, 1.0);
        const double t = 1.5;
        foreach (double p in new[] { 0.001, 0.1, 0.5, 0.9, 0.999 })
        {
            double f = calculator.InvCdf(p, t);
            Assert.Equal(p, calculator.Cdf(f, t), 1e-14);
        }
    }

    private static double Price(OptionType type, double strike, int days, double alpha, double beta) =>
        AnalyticCEVEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(type, strike), new EuropeanExercise(Today + days)),
            Context,
            F0,
            alpha,
            beta,
            Discount).NPV!.Value;
}
