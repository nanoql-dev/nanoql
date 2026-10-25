// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fixed local volatility surface's constant extrapolation in strike at a node time (the S-van plan's VR4 (xviii)).
/// Between nodes QuantLib holds a strike outside a time's strikes at the nearest one, but at a node, and for every time
/// before the first, which it clamps onto the first node, it evaluates the strike interpolation beyond its range
/// (<c>fixedlocalvolsurface.cpp:135-137</c>, <c>:143-161</c>): on three strikes 80, 100 and 120 and three times from 15
/// October 2026, linear in both, at the second node and strike 60 it gives 0.25, where a nanosecond either side it gives
/// 0.22999999995989012 and 0.23000000001994536; before the first node 0.24, where a nanosecond after it 0.2200000000401099
/// [executed: the wheel, <c>scratchpad\c5\probe_fixed_node.py</c>]. nanoql clamps at a node too; QuantLib's check, its own
/// off-node value as the time tends to the node, gives nanoql's values. Inside a time's strikes QuantLib's bits stand.
/// </summary>
public class FixedLocalVolNodeExtrapolationTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();

    [Fact]
    public void AtANodeAStrikeOutsideIsHeldAtTheNearest()
    {
        FixedLocalVolSurface surface = Surface();
        double t1 = Dc.YearFraction(Today, Today + 182);

        // QuantLib's values a nanosecond before and after the node
        AssertClose(0.22999999995989012, surface.LocalVol(t1, 60.0, true), 1e-9);
        AssertClose(0.23000000001994536, surface.LocalVol(t1, 60.0, true), 1e-9);
        AssertClose(0.18999999995989014, surface.LocalVol(t1, 140.0, true), 1e-9);
        AssertClose(0.19000000001994538, surface.LocalVol(t1, 140.0, true), 1e-9);

        // inside the strikes, QuantLib's bits
        Assert.Equal(0.22000000000000003, surface.LocalVol(t1, 90.0, true));
    }

    [Fact]
    public void BeforeTheFirstNodeAStrikeOutsideIsHeldAtTheNearest()
    {
        FixedLocalVolSurface surface = Surface();

        foreach (double t in new[] { 0.0, 0.1, Dc.YearFraction(Today, Today + 91) })
        {
            // QuantLib's value a nanosecond after the first node
            AssertClose(0.2200000000401099, surface.LocalVol(t, 60.0, true), 1e-9);
            AssertClose(0.1800000000401099, surface.LocalVol(t, 140.0, true), 1e-9);
            Assert.Equal(0.21000000000000002, surface.LocalVol(t, 90.0, true));
        }
    }

    private static FixedLocalVolSurface Surface()
    {
        double[] strikes = [80.0, 100.0, 120.0];
        var m = new Matrix(3, 3);
        for (int i = 0; i < 3; ++i)
        {
            for (int j = 0; j < 3; ++j)
            {
                m[i, j] = 0.2 + (0.001 * (100.0 - strikes[i])) + (0.01 * j);
            }
        }

        return new FixedLocalVolSurface(Today, [Today + 91, Today + 182, Today + 365], strikes, m, Dc);
    }

    private static void AssertClose(double expected, double actual, double relative) =>
        Assert.True(
            Math.Abs(actual - expected) <= relative * Math.Abs(expected),
            $"{actual:R} differs from {expected:R} by more than {relative} relative");
}
