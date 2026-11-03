// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The sphere-cylinder optimizer's clamp (fixed). QuantLib's constructor stores the cylinder's radius s, then clamps
/// its parameter to 0 (<c>spherecylinder.cpp:87</c>), so its intersection test reads the clamped radius and its solves
/// the stored one: at r 1, α 0.5 and z (0.6, 0.1, 0.5), s −0.2 projects to (0.359, −0.141, 0.923) and s 0 to (0.5, 0,
/// 0.866). nanoql stores the clamped radius. QuantLib's one caller passes s ≥ 0. Check: QuantLib's own optimizer at s
/// 0, for every s below 0.
/// </summary>
public class SphereCylinderClampTests
{
    [Theory]
    [InlineData(-0.2)]
    [InlineData(-1e-3)]
    [InlineData(-5.0)]
    public void ANegativeRadiusIsTheZeroRadius(double s)
    {
        var negative = new SphereCylinderOptimizer(1.0, s, 0.5, 0.6, 0.1, 0.5);
        var zero = new SphereCylinderOptimizer(1.0, 0.0, 0.5, 0.6, 0.1, 0.5);
        Assert.Equal(zero.IsIntersectionNonEmpty(), negative.IsIntersectionNonEmpty());
        Assert.True(negative.FindByProjection(out double y1, out double y2, out double y3));
        zero.FindByProjection(out double x1, out double x2, out double x3);
        Assert.Equal((x1, x2, x3), (y1, y2, y3));
        Assert.Equal(0.5, y1, 12);
        Assert.Equal(0.0, y2, 12);
        Assert.Equal(Math.Sqrt(0.75), y3, 12);
        negative.FindClosest(100, 1e-10, out y1, out y2, out y3);
        zero.FindClosest(100, 1e-10, out x1, out x2, out x3);
        Assert.Equal((x1, x2, x3), (y1, y2, y3));
    }
}
