// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="TrapezoidIntegral{TIntegrationPolicy}"/> with <see cref="MidPoint"/>. QuantLib starts the recursion from
/// the trapezoid's estimate, (f(a) + f(b))·(b − a)/2, for either rule (<c>trapezoidintegral.hpp:58-82</c>), while the
/// midpoint rule's refinement, (I + dx·Σ(f(x + dx/6) + f(x + 5dx/6)))/3 (<c>:101-117</c>), triples a midpoint sum only
/// from a midpoint estimate: the start's error then falls by a third a tripling, first order, and <c>exp</c> on [0, 1]
/// at 1e-6 takes QuantLib 531,442 evaluations and reads 1.7182822244001084, 3.9e-7 from e − 1. nanoql starts from the
/// one-interval midpoint estimate, (b − a)·f((a + b)/2).
/// </summary>
public class TrapezoidMidPointTests
{
    [Fact]
    public void TheMidPointRuleStartsFromTheMidPoint()
    {
        IntegrationResult r = new TrapezoidIntegral<MidPoint>(1e-6, 100).IntegrateWithStatistics(Math.Exp, 0.0, 1.0);

        // QuantLib: 531,442 evaluations, 1.7182822244001084
        Assert.Equal(2187, r.NumberOfEvaluations);

        // QuantLib's check: its own Simpson rule (1.7182818290280153 in 65) and Gauss-Lobatto rule
        // (1.7182818284591825 in 20) give the same integral to the tolerance
        Assert.Equal(1.7182818290280153, r.Value, 1e-6);
        Assert.Equal(1.7182818284591825, r.Value, 1e-6);
        Assert.Equal(Math.E - 1.0, r.Value, 1e-6);
    }
}
