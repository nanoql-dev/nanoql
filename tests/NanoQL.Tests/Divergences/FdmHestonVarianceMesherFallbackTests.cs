// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Heston variance mesher's fallbacks, QuantLib's master since 1.44 (#2908's mesher part and #2940, taken under R8;
/// the S-hes plan's HR7 (iii), HR8 (ii), (iii)). Where the variance falls fast (v₀ 0.25, θ 0.0025, κ 10, σ 0.1), 1.44's
/// time-averaged quantiles don't reach v₀ and every finite-difference Heston engine throws "interpolation range is
/// [4.01809, 5.16225] x [0, 0.0483231]: extrapolation at (4.60517, 0.25) not allowed" [executed on the wheel:
/// <c>scratchpad\c1\oracle_c1.py</c>]; master falls back to the default mesh, evenly spaced over θ and v₀ ± 4σ√(θ/2κ),
/// whose locations are those 1.44 builds on its own default branch, and the engine prices within its grid's accuracy of
/// QuantLib's analytic engine. A distribution too narrow for the grid's minimum steps falls back to the same default
/// mesh before any quantile is computed, the mesh 1.44 reaches only after its quantiles fail; at σ 1e-4 master's later
/// checks would reach it too, and at 5.6e-4 and 6.8e-4 (v₀ = θ 0.04, κ 1) that test alone does, the later checks passing
/// the quantile grid.
/// </summary>
public class FdmHestonVarianceMesherFallbackTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AFastFallingVarianceIsPricedOnTheDefaultMesh()
    {
        HestonProcess process = Process(0.25, 10.0, 0.0025, 0.1);
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(Today + 365));

        // QuantLib's analytic engine, 192-point Gauss-Laguerre; QuantLib 1.44's FD engine throws
        const double Analytic = 5.014128713204369;
        double fd = FdHestonVanillaEngine.Calculate(put, Context, new HestonModel(process)).NPV!.Value;
        Assert.True(Math.Abs(fd - Analytic) < 2e-3 * Analytic, $"FD {fd} against the analytic engine's {Analytic}");
    }

    [Fact]
    public void TheFallbacksLocationsAreTheDefaultMesh()
    {
        HestonProcess process = Process(0.25, 10.0, 0.0025, 0.1);
        var mesher = new FdmHestonVarianceMesher(9, process, 1.0);

        // 1.44's default branch: theta and v0 +- 4*sigma*sqrt(theta/(2*kappa)), evenly spaced, v0 snapped onto the grid
        double vol = 0.1 * Math.Sqrt(0.0025 / (2 * 10.0));
        double upperBound = Math.Max(0.25 + (4 * vol), 0.0025 + (4 * vol));
        double lowerBound = Math.Max(0.0, Math.Min(0.25 - (4 * vol), 0.0025 - (4 * vol)));
        double[] expected = new double[9];
        for (int i = 0; i < 9; ++i)
        {
            expected[i] = lowerBound + (i * (upperBound - lowerBound) / 8.0);
        }

        for (int i = 1; i < 9; ++i)
        {
            if (expected[i - 1] <= 0.25 && expected[i] >= 0.25)
            {
                if (Math.Abs(expected[i - 1] - 0.25) < Math.Abs(expected[i] - 0.25))
                {
                    expected[i - 1] = 0.25;
                }
                else
                {
                    expected[i] = 0.25;
                }
            }
        }

        Assert.Equal(expected, mesher.Locations());
    }

    [Fact]
    public void ADistributionTooNarrowForTheGridFallsBackBeforeItsQuantiles()
    {
        // sigma 1e-4 at v0 = theta 0.04, kappa 1: QuantLib 1.44's mesher, which reaches the default mesh after its
        // quantiles fail [executed on the wheel]; master's normal approximation finds the distribution too narrow for
        // the grid's minimum steps at every averaging time and goes there first, its later checks reaching the same mesh
        // here
        var mesher = new FdmHestonVarianceMesher(9, Process(0.04, 1.0, 0.04, 1e-4), 1.0);
        double[] quantLib =
        [
            0.03994343145750508, 0.039957573593128806, 0.039971715728752535, 0.03998585786437627, 0.04,
            0.04001414213562373, 0.040028284271247466, 0.040042426406871195, 0.040056568542494925,
        ];
        Assert.Equal(quantLib, mesher.Locations());
    }

    [Theory]
    [InlineData(5.6e-4, new[]
    {
        0.039683216162028426, 0.03976241212152132, 0.03984160808101421, 0.03992080404050711, 0.04,
        0.04007919595949289, 0.04015839191898579, 0.040237587878478684, 0.040316783837971576,
    })]
    [InlineData(6.8e-4, new[]
    {
        0.03961533391103452, 0.03971150043327589, 0.03980766695551726, 0.03990383347775863, 0.04,
        0.04009616652224137, 0.04019233304448274, 0.04028849956672411, 0.04038466608896548,
    })]
    public void ADistributionTooNarrowForTheGridStaysOnTheDefaultMesh(double sigma, double[] quantLib)
    {
        // QuantLib 1.44's mesher, the default mesh [executed on the wheel]; master's quantile grid passes its two later
        // checks here, so its normal approximation's test alone keeps the default mesh
        var mesher = new FdmHestonVarianceMesher(9, Process(0.04, 1.0, 0.04, sigma), 1.0);
        Assert.Equal(quantLib, mesher.Locations());
    }

    // r 5%, q 2%, rho -0.5, spot 100
    private static HestonProcess Process(double v0, double kappa, double theta, double sigma) =>
        new(new FlatForward(Today, 0.05, A365), new FlatForward(Today, 0.02, A365), 100.0, v0, kappa, theta, sigma, -0.5);
}
