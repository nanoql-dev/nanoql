// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The abcd specifier's in-between short rates at an offset (fixed). QuantLib builds the variance of the short rate in
/// slot <c>i + j·period + offset</c> on reset index <c>i + j·period</c>, an earlier rate's
/// (<c>volatilityinterpolationspecifierabcd.cpp:169-170</c>): in periods of 2 at offset 1 over twelve semi-annual short
/// rates, slots 1 to 4 reset at indexes 0 to 3, each slot's variance ending a step early (the C++ harness's
/// <c>VolatilityInterpolationSpecifierabcd</c>). nanoql resets each slot at its own index. Checks: each in-between
/// slot's variance is QuantLib's own <see cref="PiecewiseConstantAbcdVariance"/> on the two long rates' mean abcd at
/// that index, bit for bit; at offset 0, QuantLib's tested case, the vectors' bits.
/// </summary>
public class VolatilityInterpolationSpecifierOffsetTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    public void EachInBetweenSlotResetsAtItsOwnIndex(int period, int offset)
    {
        double[] small = [.. Enumerable.Range(1, 13).Select(i => 0.5 * i)];
        int count = (small.Length - 1 - offset) / period;
        double[] longTimes = [.. Enumerable.Range(0, count + 1).Select(j => small[offset + (j * period)])];
        PiecewiseConstantAbcdVariance[] originals =
        [
            .. Enumerable.Range(0, count).Select(i => new PiecewiseConstantAbcdVariance(
                -0.05 + (0.002 * i), 0.16 + (0.005 * i), 0.5 + (0.01 * i), 0.15 + (0.003 * i), i, longTimes)),
        ];
        var spec = new VolatilityInterpolationSpecifierabcd(period, offset, originals, small);
        for (int j = 0; j < count - 1; ++j)
        {
            AbcdCoefficients lo = originals[j].GetABCD(), hi = originals[j + 1].GetABCD();
            for (int i = 0; i < period; ++i)
            {
                int slot = i + (j * period) + offset;
                var expected = new PiecewiseConstantAbcdVariance(
                    0.5 * (lo.A + hi.A), 0.5 * (lo.B + hi.B), 0.5 * (lo.C + hi.C), 0.5 * (lo.D + hi.D), slot, small);
                Assert.Equal(expected.Variances, spec.InterpolatedVariances[slot].Variances);

                // its last nonzero variance at its own index, QuantLib's a step earlier
                Assert.NotEqual(0.0, spec.InterpolatedVariances[slot].Variances[slot]);
                Assert.Equal(0.0, spec.InterpolatedVariances[slot].Variances[slot + 1]);
            }
        }
    }
}
