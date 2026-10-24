// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types of Boost.Math's errors (playbook §2): a domain error on the caller's argument throws
/// <see cref="ArgumentOutOfRangeException"/>; a domain error on a stored parameter and an overflow throw
/// <see cref="NanoQLException"/>; each keeps Boost's message. A vector's <c>{"throws": true}</c> accepts either type,
/// so these facts assert them.
/// </summary>
public class BoostErrorTests
{
    [Fact]
    public void AProbabilityOutsideTheUnitIntervalIsTheCallersArgument()
    {
        var quantile = new MaddockInverseCumulativeNormal();

        foreach (double p in new[] { -0.1, 1.5 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => quantile.Evaluate(p));
            Assert.StartsWith(
                "Error in function boost::math::quantile(const normal_distribution<double>&, double): Probability "
                + $"argument is {p.ToString(System.Globalization.CultureInfo.InvariantCulture)}, but must be >= 0 and <= 1 !",
                e.Message,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ANaNToTheCdfIsTheCallersArgument()
    {
        var cdf = new MaddockCumulativeNormal();

        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => cdf.Evaluate(double.NaN));
        Assert.StartsWith(
            "Error in function boost::math::cdf(const normal_distribution<double>&, double): Random variate x is NaN, "
            + "but must be finite!",
            e.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnInfiniteMeanIsAStoredParameter()
    {
        // QuantLib's constructors check only sigma; Boost's normal_distribution refuses the mean when evaluated.
        var cdf = new MaddockCumulativeNormal(double.PositiveInfinity, 1.0);
        var quantile = new MaddockInverseCumulativeNormal(double.NegativeInfinity, 1.0);

        NanoQLException e = Assert.Throws<NanoQLException>(() => cdf.Evaluate(0.0));
        Assert.Equal(
            "Error in function boost::math::normal_distribution<double>::normal_distribution: Location parameter is "
            + "Infinity, but must be finite!",
            e.Message);
        e = Assert.Throws<NanoQLException>(() => quantile.Evaluate(0.5));
        Assert.Equal(
            "Error in function boost::math::normal_distribution<double>::normal_distribution: Location parameter is "
            + "-Infinity, but must be finite!",
            e.Message);
    }

    [Fact]
    public void TheQuantileOfZeroOrOneIsAnOverflow()
    {
        var quantile = new MaddockInverseCumulativeNormal(3.0, 2.0);

        foreach (double p in new[] { 0.0, 1.0 })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(() => quantile.Evaluate(p));
            Assert.Equal("Error in function boost::math::erfc_inv<double>(double, double): Overflow Error", e.Message);
        }
    }

    [Theory]
    [InlineData(-100.0, 100.0, 0.3, "NaN")] // X(f0) NaN: the first distribution's non-centrality
    [InlineData(100.0, -10.0, 0.5, "-10")] // X(K) = K at β 0.5: the second's, refused before the first's variate
    public void ANonCentralityOutsideBoostsRangeIsAStoredParameter(double f0, double strike, double beta, string ncp)
    {
        // a negative forward or strike; QuantLib's messages, Boost's NaN formatted -nan(ind) there
        NanoQLException e = Assert.Throws<NanoQLException>(() => CevCall(f0, strike, beta));
        Assert.Equal(
            "Error in function boost::math::non_central_chi_squared_distribution<double>::"
            + $"non_central_chi_squared_distribution(double,double): Non centrality parameter is {ncp}, but must be > 0, and "
            + "a countable value such that x+1 != x",
            e.Message);
    }

    [Fact]
    public void ANaNVariateIsTheCallersArgument()
    {
        // β above 1: the density's distribution at a NaN forward reads Boost's cdf at a NaN variate
        var calculator = new CEVRNDCalculator(100.0, 0.2 * Math.Pow(100.0, -0.5), 1.5);
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Cdf(double.NaN, 1.0));
        Assert.StartsWith(
            "Error in function boost::math::non_central_chi_squared_distribution<double>::cdf(double): Random variate x "
            + "is NaN, but must be finite and >= 0!",
            e.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheCevDensitysQuantileOfOneIsAnOverflow()
    {
        // β above 1: the inverse at probability 0 takes Boost's non-central chi-squared quantile at 1 − 0; at
        // probability 1 the quantile at 0, whose X⁻¹ is +∞, as QuantLib's
        var calculator = new CEVRNDCalculator(100.0, 0.2 * Math.Pow(100.0, -0.5), 1.5);
        NanoQLException e = Assert.Throws<NanoQLException>(() => calculator.InvCdf(0.0, 1.0));
        Assert.Equal(
            "Error in function quantile(non_central_chi_squared_distribution<double>, double): Overflow Error", e.Message);
        Assert.Equal(double.PositiveInfinity, calculator.InvCdf(1.0, 1.0));
    }

    private static double CevCall(double f0, double strike, double beta)
    {
        var today = new Date(15, Month.October, 2026);
        return AnalyticCEVEngine.Calculate(
            new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(today + 365)),
            new EvaluationContext { EvaluationDate = today },
            f0,
            0.2 * Math.Pow(100.0, 1 - beta),
            beta,
            new FlatForward(today, 0.03, new Actual365Fixed())).NPV!.Value;
    }
}
