// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The G2 process's conditional correlation: QuantLib's step correlation is ρ times the ratio of the factors' kernels
/// (<c>g2process.cpp:100-111</c>, <c>:223-234</c>), which at a = b and ρ = 1 rounds just above 1, so the standard
/// deviation's √(1 − ρ²) is NaN: on the wheel, the half-year step's [1][1] at a = b = 0.1, σ = η = 0.01, ρ = 1 is NaN,
/// its [1][0] 0.006897919322666819. At a zero step the ratio is 0/0, NaN, and so are the standard deviation's second
/// row and the covariance's terms that read it. nanoql's correlation is ρ·f((a+b)Δt)/√(f(2aΔt)f(2bΔt)), f(x) = (1 −
/// e^(−x))/x, held in [−1, 1] and ρ itself at a = b, its limit at Δt = 0. QuantLib's check: the bound, and the step's
/// limit.
/// </summary>
public class G2ProcessCorrelationBoundTests
{
    [Fact]
    public void ThePerfectlyCorrelatedStepIsBounded()
    {
        var p = new G2Process(0.1, 0.01, 0.1, 0.01, 1.0);
        Matrix m = p.StdDeviation(0.0, new Vector([0.0, 0.0]), 0.5);
        Assert.Equal(0.0, m[1, 1]);
        Assert.True(Math.Abs((m[1, 0] / 0.006897919322666819) - 1.0) <= 1e-15, $"{m[1, 0]}");
        Assert.Equal(m[0, 0], m[1, 0]);

        var q = new G2Process(0.2, 0.01, 0.2, 0.012, -1.0);
        Matrix n = q.StdDeviation(1.0, new Vector([0.01, 0.0]), 0.25);
        Assert.Equal(0.0, n[1, 1]);
        Assert.True(n[1, 0] < 0.0);
    }

    [Fact]
    public void TheZeroStepIsItsLimit()
    {
        var p = new G2Process(0.1, 0.01, 0.2, 0.013, -0.5);
        Matrix sd = p.StdDeviation(2.0, new Vector([0.0, 0.0]), 0.0);
        Matrix cov = p.Covariance(2.0, new Vector([0.0, 0.0]), 0.0);
        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                Assert.Equal(0.0, Math.Abs(sd[i, j]));
                Assert.Equal(0.0, Math.Abs(cov[i, j]));
            }
        }
    }
}
