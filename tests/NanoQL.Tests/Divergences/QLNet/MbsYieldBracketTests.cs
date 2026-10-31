// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Extensions.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="MBSFixedRateBond.MonthlyYield"/> solves in QLNet's bracket of [0, 1]% a month where it holds the root,
/// and otherwise by Brent's step search from QLNet's guess, above −100% a month. QLNet's solve refuses a yield outside
/// its bracket (<c>MBSFixedRateBond.cs:97</c>): a 13% pass-through priced at par throws "root not bracketed: f[0,1]".
/// The checks: the PV difference at the root, and the closed form at par, the pass-through rate over 12.
/// </summary>
public class MbsYieldBracketTests
{
    [Fact]
    public void AYieldAboveQLNetsBracketIsSolved()
    {
        MBSFixedRateBond pool = MbsPools.Pool(120, 120, 0.135, 0.13, new ConstantCPR(0.05));
        EvaluationContext context = MbsPools.At(MbsPools.Start);

        // QLNet: throws ArgumentException "root not bracketed: f[0,1] -> [266384278.0028025,14138600.47417128]".
        double y = pool.MonthlyYield(context);
        Assert.True(Math.Abs(y - (0.13 / 12)) <= 1e-12, $"monthly yield {y:R}");
        double pv = MBSFixedRateBondFunctions.PVDifference(400000000, pool.ExpectedCashflows, y * 100, MbsPools.Start,
            context);
        Assert.True(Math.Abs(pv) <= 1e-2, $"PV difference {pv:R}");
    }

    [Fact]
    public void AYieldBelowQLNetsBracketIsSolved()
    {
        // A negative pass-through rate priced at par: the yield is below 0, outside QLNet's bracket.
        MBSFixedRateBond pool = MbsPools.Pool(60, 60, 0.02, -0.01, new ConstantCPR(0.05));
        EvaluationContext context = MbsPools.At(MbsPools.Start);
        ImmutableArray<CashFlow> flows = pool.ExpectedCashflows;

        Assert.True(MBSFixedRateBondFunctions.PVDifference(400000000, flows, 0.0, MbsPools.Start, context) < 0);
        double y = pool.MonthlyYield(context);
        Assert.True(Math.Abs(y - (-0.01 / 12)) <= 1e-12, $"monthly yield {y:R}");
    }
}
