// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The isotropic random walk's direction from three dimensions (the S-xrt plan's XRR5 (xxxiii) and XRR11, fixed under
/// R8). QuantLib's hyperspherical angles are each uniform in [0, π], where a uniform direction needs densities
/// ∝ sin^(d−k−1)φ, so its first coordinate's second moment is ½ in any dimension, against 1/d. nanoql's direction is
/// Muller's, d normals scaled to unit length (IMSL's RNSPH above four dimensions). On a unit radius at a fixed seed its
/// moments are E[xᵢ²] = 1/d and E[xᵢxⱼ] = 0 within three standard errors, d 3 to 6, and its first coordinate's law,
/// the density ∝ (1 − x²)^((d−3)/2) on [−1, 1], passes a binned χ² test at the 0.1% level; in one and two dimensions
/// the walk is QuantLib's, bit for bit (the harness's vectors).
/// </summary>
public class IsotropicRandomWalkTests
{
    private const int Draws = 20000;

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void TheMomentsAreTheUniformDirections(int d)
    {
        double[][] x = Directions(d);
        double ex4 = 3.0 / (d * (d + 2.0));
        double seSquare = Math.Sqrt((ex4 - (1.0 / (d * d))) / Draws);
        double seCross = Math.Sqrt(1.0 / (d * (d + 2.0)) / Draws);
        for (int i = 0; i < d; i++)
        {
            double m2 = x.Average(s => s[i] * s[i]);
            Assert.True(Math.Abs(m2 - (1.0 / d)) < 3.0 * seSquare, $"d {d}: E[x{i}²] {m2} for {1.0 / d}");
            for (int j = i + 1; j < d; j++)
            {
                double m11 = x.Average(s => s[i] * s[j]);
                Assert.True(Math.Abs(m11) < 3.0 * seCross, $"d {d}: E[x{i}x{j}] {m11}");
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void TheFirstCoordinatesLawPassesAChiSquaredTest(int d)
    {
        double[][] x = Directions(d);
        const int bins = 20;
        var counts = new int[bins];
        foreach (double[] s in x)
        {
            counts[Math.Min(bins - 1, (int)((s[0] + 1.0) / 2.0 * bins))]++;
        }

        // each bin's probability: the density ∝ (1 − x²)^((d−3)/2), so (x + 1)/2 is Beta((d − 1)/2, (d − 1)/2)
        double a = (d - 1) / 2.0;
        double Cdf(double t) => t <= 0.0 ? 0.0 : t >= 1.0 ? 1.0 : BetaFunctions.IncompleteBetaFunction(a, a, t);
        double chi2 = 0.0;
        for (int b = 0; b < bins; b++)
        {
            double p = Cdf((double)(b + 1) / bins) - Cdf((double)b / bins);
            double expected = p * Draws;
            chi2 += (counts[b] - expected) * (counts[b] - expected) / expected;
        }

        // the 99.9% quantile of χ² with 19 degrees of freedom
        Assert.True(chi2 < 43.82, $"d {d}: χ² {chi2}");
    }

    // Unit-radius steps at seed 42.
    private static double[][] Directions(int d)
    {
        var walk = new IsotropicRandomWalk<UnitRadius>(new MersenneTwisterUniformRng(42), new UnitRadius(), d, null, 42);
        var x = new double[Draws][];
        for (int k = 0; k < Draws; k++)
        {
            x[k] = new double[d];
            walk.NextReal(x[k]);
        }

        return x;
    }

    private sealed class UnitRadius : IRandomWalkDistribution
    {
        public double Next(MersenneTwisterUniformRng engine) => 1.0;

        public IRandomWalkDistribution Clone() => this;
    }
}
