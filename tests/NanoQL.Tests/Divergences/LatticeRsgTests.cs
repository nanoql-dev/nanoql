// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The lattice rules' bound and skip (the S-core plan's CR7 (xi) and (xii)): QuantLib's <c>LatticeRule::getRule</c>
/// checks the number of points against 2.9^20, about 1.8e9, where its message and its tables say 2^20, so 2^22 points
/// are accepted; nanoql checks 2^20. And QuantLib's <c>LatticeRsg::skipTo(n)</c> moves n points on, where its other
/// sequences' <c>skipTo</c> moves to the n-th: after 3 draws, <c>skipTo(10)</c> draws the 13th point (0.0126953125,
/// 0.0146484375 on rule A at 1,024 points in two dimensions); nanoql's draws the 10th, as a fresh sequence skipped to
/// 10 does in QuantLib (0.009765625, 0.857421875) (the plan's <c>misc1.cpp</c>, rerun in this run's
/// <c>probes\misc1.cpp</c>).
/// </summary>
public class LatticeRsgTests
{
    [Fact]
    public void TheRulesHoldUpToTwoToTheTwentyPoints()
    {
        _ = LatticeRule.GetRule(LatticeRuleType.A, 1 << 20);
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => LatticeRule.GetRule(LatticeRuleType.A, 1 << 22)); // QuantLib accepts it
        Assert.Equal("n", e.ParamName);
        Assert.StartsWith("N must be between 2 to 10 and 2 to the 20 for these lattice rules ", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipToMovesToThePoint()
    {
        var z = LatticeRule.GetRule(LatticeRuleType.A, 1024);
        var drawn = new LatticeRsg(2, z, 1024);
        drawn.NextSequence();
        drawn.NextSequence();
        drawn.NextSequence();
        drawn.SkipTo(10);
        Assert.Equal([0.009765625, 0.857421875], drawn.NextSequence().Value.ToArray()); // QuantLib 0.0126953125, 0.0146484375

        var fresh = new LatticeRsg(2, z, 1024);
        fresh.SkipTo(10);
        Assert.Equal([0.009765625, 0.857421875], fresh.NextSequence().Value.ToArray()); // QuantLib's
    }
}
