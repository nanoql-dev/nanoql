// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fix QuantLib's master made to Kruger's cubic after the pin, which nanoql takes: #2952 (<c>32de8d923</c>), a zero
/// derivative at a node where the adjacent slopes' product is at most 0, as the harmonic scheme has it. QuantLib 1.44
/// tests <c>S_[i-1]*S_[i] &lt; 0.0</c> (<c>cubicinterpolation.hpp:634</c>), so where a −0 slope meets a +0 one the
/// harmonic mean 2/(1/S₀ + 1/S₁) is 2/(−∞ + ∞), NaN, and the interpolation is NaN on both adjacent intervals.
/// </summary>
public class InterpolationUpstreamFixesTests
{
    [Fact]
    public void TestKrugerZeroSlope()
    {
        // Upstream: QuantLib's master's case for #2952 (interpolations.cpp's testKrugerZeroSlope)
        // a flat segment, and a -0 slope next to a +0 one
        double[] x = [0.0, 1.0, 2.0, 3.0, 4.0];
        double[][] y = [[1.0, 1.0, 1.0, 2.0, 3.0], [0.0, -0.0, 0.0, 1.0, 2.0]];

        foreach (double[] yi in y)
        {
            var f = new KrugerCubic(x, yi);
            for (int k = 0; k <= 40; ++k)
            {
                double xk = k * 0.1;
                Assert.False(double.IsNaN(f.Evaluate(xk)) || double.IsNaN(f.Derivative(xk)), $"NaN detected at x = {xk}");
            }

            Assert.Equal(0.0, f.Derivative(1.0));
        }
    }
}
