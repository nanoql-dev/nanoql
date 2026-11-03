// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The coinitial Z matrix (fixed). QuantLib's <c>SwapForwardMappings::coinitialSwapZedMatrix</c> scales each row from
/// its diagonal rightward (<c>for (Size j=i; j&lt;n; ++j)</c>, <c>swapforwardmappings.cpp:152-153</c>), the coterminal
/// version's loop, though a coinitial swap's row is nonzero left of its diagonal, so those entries stay raw
/// derivatives, blind to the displacement: on six semi-annual rates at 3% + 0.4% i, entry [5][0] is 0.174871 at
/// displacements 0 and 2%, where the elasticity of S₅ + d in f₀ + d is 0.131951 and 0.146315. No QuantLib code calls
/// it. nanoql scales every entry. Checks: each row is QuantLib's own <c>cmSwapZedMatrix(cs, i + 1, d)</c>'s first row,
/// the same calls in the same order, bit for bit; and the bumped elasticity.
/// </summary>
public class CoinitialZedMatrixTests
{
    [Theory]
    [InlineData(0.0, 0.131951)]
    [InlineData(0.02, 0.146315)]
    public void EachRowIsTheConstantMaturityFirstRow(double d, double elasticity)
    {
        const int n = 6;
        double[] times = [.. Enumerable.Range(0, n + 1).Select(i => 0.5 * (i + 1))];
        double[] f = [.. Enumerable.Range(0, n).Select(i => 0.03 + (0.004 * i))];
        var cs = new LMMCurveState(times);
        cs.SetOnForwardRates(f);
        Matrix z = SwapForwardMappings.CoinitialSwapZedMatrix(cs, d);
        for (int i = 0; i < n; ++i)
        {
            Matrix c = SwapForwardMappings.CmSwapZedMatrix(cs, i + 1, d);
            for (int j = 0; j < n; ++j)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(c[0, j]), BitConverter.DoubleToInt64Bits(z[i, j]));
            }
        }

        // the elasticity of S5 + d in f0 + d by a central bump of log(f0 + d)
        const double h = 1e-6;
        double[] up = (double[])f.Clone(), down = (double[])f.Clone();
        up[0] = ((f[0] + d) * Math.Exp(h)) - d;
        down[0] = ((f[0] + d) * Math.Exp(-h)) - d;
        var u = new LMMCurveState(times);
        u.SetOnForwardRates(up);
        var w = new LMMCurveState(times);
        w.SetOnForwardRates(down);
        double bump = (Math.Log(u.CmSwapRate(0, n) + d) - Math.Log(w.CmSwapRate(0, n) + d)) / (2 * h);
        Assert.Equal(bump, z[5, 0], 1e-8);
        Assert.Equal(elasticity, z[5, 0], 1e-6);
        Assert.NotEqual(0.174871, z[5, 0], 1e-2);
    }
}
