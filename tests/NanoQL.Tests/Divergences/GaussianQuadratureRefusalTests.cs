// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="MultiDimGaussianIntegration"/> with no dimension. QuantLib writes <c>spacing[0] = 1</c> on an empty
/// vector (<c>gaussianquadratures.cpp:101-102</c>), an undefined write; nanoql refuses on entry, after QuantLib's own
/// allocations.
/// </summary>
public class GaussianQuadratureRefusalTests
{
    [Fact]
    public void NoDimensionIsRefused()
    {
        var e = Assert.Throws<ArgumentException>(
            () => new MultiDimGaussianIntegration([], n => new GaussLegendreIntegration(n)));
        Assert.Equal("ns", e.ParamName);
        Assert.StartsWith("no dimension given", e.Message, StringComparison.Ordinal);
    }
}
