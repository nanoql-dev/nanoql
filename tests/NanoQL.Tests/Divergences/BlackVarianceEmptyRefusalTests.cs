// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="BlackVarianceCurve"/> and <see cref="BlackVarianceSurface"/> with no dates. QuantLib's constructors read
/// <c>dates.back()</c> of the empty list in their initializers, before any check (<c>blackvariancecurve.cpp:35</c>,
/// <c>blackvariancesurface.cpp:36</c>), a result C++ leaves undefined; on the wheel each crashes the process. nanoql
/// refuses with <see cref="ArgumentException"/>, "no dates given", naming <c>dates</c>.
/// </summary>
public class BlackVarianceEmptyRefusalTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void ACurveWithNoDatesIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new BlackVarianceCurve(Today, [], [], new Actual365Fixed()));
        Assert.Equal("dates", e.ParamName);
        Assert.StartsWith("no dates given", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASurfaceWithNoDatesIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new BlackVarianceSurface(Today, null, [], [90.0, 110.0], new Matrix(2, 0), new Actual365Fixed()));
        Assert.Equal("dates", e.ParamName);
        Assert.StartsWith("no dates given", e.Message, StringComparison.Ordinal);
    }
}
