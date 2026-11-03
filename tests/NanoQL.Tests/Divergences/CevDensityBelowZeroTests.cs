// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CEV density below zero (a run find). Where the Sankaran guess's Brent fails, QuantLib's inverse starts a second
/// Brent from the forward (<c>cevrndcalculator.cpp:105-120</c>), which can step below zero, where X(f) = f^(2(1 − β))
/// is NaN and Boost refuses it: QuantLib throws "Non centrality parameter is -nan(ind)" at a quantile that exists.
/// nanoql's density and distribution are 0 below zero, the absorbing boundary, and QuantLib's values at and above it
/// stand, so the second Brent finds the quantile to its accuracy, 1e-8 in the forward. The check: the distribution
/// brackets the probability within that accuracy (QuantLib-Python 1.44; f0 100, α = σ·f0^(1 − β)).
/// </summary>
public class CevDensityBelowZeroTests
{
    [Theory]
    [InlineData(0.3, 0.3, 1.0, 0.001)] // σ 30%, a year: one of the 12 the density's vectors leave out
    [InlineData(0.95, 2.0, 5.0, 0.5)]
    [InlineData(0.95, 1.0, 30.0, 0.9)]
    public void WhereQuantLibStepsBelowZeroTheQuantileIsFound(double beta, double sigma, double t, double q)
    {
        // QuantLib throws "Error in function boost::math::non_central_chi_squared_distribution<double>::
        // non_central_chi_squared_distribution(double,double): Non centrality parameter is -nan(ind), but must be > 0,
        // and a countable value such that x+1 != x"
        var calculator = new CEVRNDCalculator(100.0, sigma * Math.Pow(100.0, 1 - beta), beta);
        double f = calculator.InvCdf(q, t);
        Assert.True(calculator.Cdf(Math.Max(0.0, f - 1e-8), t) <= q && q <= calculator.Cdf(f + 1e-8, t), $"{f}");
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.5)]
    public void BelowZeroTheDensityAndDistributionAre0(double beta)
    {
        // QuantLib refuses both, X(−1) being NaN
        var calculator = new CEVRNDCalculator(100.0, 0.2 * Math.Pow(100.0, 1 - beta), beta);
        Assert.Equal(0.0, calculator.Pdf(-1.0, 1.0));
        Assert.Equal(0.0, calculator.Cdf(-1.0, 1.0));
    }
}
