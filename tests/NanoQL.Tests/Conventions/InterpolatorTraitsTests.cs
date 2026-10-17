// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The interpolation factories' static traits, QuantLib's <c>global</c> and <c>requiredPoints</c>, read as a consumer
/// generic over the factory reads them, through <see cref="IInterpolator"/>'s static abstract members.
/// </summary>
public class InterpolatorTraitsTests
{
    [Fact]
    public void FactoriesHaveQuantLibsTraits()
    {
        Assert.Equal((false, 2), Traits<Linear>());
        Assert.Equal((false, 1), Traits<BackwardFlat>());
        Assert.Equal((false, 2), Traits<LogLinear>());
        Assert.Equal((true, 2), Traits<Cubic>());
        Assert.Equal((true, 2), Traits<LogCubic>());
        Assert.Equal((true, 2), Traits<MonotonicLogCubic>());
        Assert.Equal((true, 2), Traits<KrugerLog>());
    }

    private static (bool Global, int RequiredPoints) Traits<TInterpolator>()
        where TInterpolator : struct, IInterpolator =>
        (TInterpolator.Global, TInterpolator.RequiredPoints);
}
