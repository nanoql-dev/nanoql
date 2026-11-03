// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Documented divergence of <see cref="KlugeExtOUProcess.Diffusion"/>: QuantLib writes the gas row at row <c>size()</c>
/// and column <c>factors()</c>, one past each end of its <c>size() × factors()</c> matrix, so its gas row is 0 (and the
/// writes corrupt the heap); here the gas row is the last, ρσ_u in the first column and √(1 − ρ²)σ_u in the last, so
/// the diffusion's square is the covariance of the power and gas Brownian motions.
/// </summary>
public class KlugeExtOUDiffusionTests
{
    [Theory]
    [InlineData(0.7)]
    [InlineData(-0.4)]
    [InlineData(0.0)]
    public void TheDiffusionsSquareIsTheCovariance(double rho)
    {
        const double sigmaX = 1.4, sigmaU = 1.1;
        var power = new ExtOUWithJumpsProcess(new ExtendedOrnsteinUhlenbeckProcess(7.0, sigmaX, 0.0, _ => 0.0), 0.0, 200.0, 4.0, 5.0);
        var gas = new ExtendedOrnsteinUhlenbeckProcess(4.45, sigmaU, 0.0, _ => 0.0);
        var process = new KlugeExtOUProcess(rho, power, gas);

        Matrix d = process.Diffusion(0.5, process.InitialValues());
        Assert.Equal(3, d.Rows);
        Assert.Equal(4, d.Columns);
        double[,] expected = { { sigmaX * sigmaX, 0.0, rho * sigmaX * sigmaU }, { 0.0, 0.0, 0.0 }, { rho * sigmaX * sigmaU, 0.0, sigmaU * sigmaU } };
        for (int i = 0; i < 3; ++i)
        {
            for (int j = 0; j < 3; ++j)
            {
                double sum = 0.0;
                for (int k = 0; k < 4; ++k)
                {
                    sum += d[i, k] * d[j, k];
                }

                Assert.Equal(expected[i, j], sum, 1e-14);
            }
        }
    }
}
