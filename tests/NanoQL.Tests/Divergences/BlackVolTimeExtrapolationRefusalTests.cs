// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="BlackVolTimeExtrapolation"/> on too few times. QuantLib's surface overload reads <c>times[N-2]</c>
/// unchecked under the linear strategy (<c>blackvoltimeextrapolation.cpp:49-50</c>), past the list's start with one
/// time, where its curve overload refuses "at least two times required for volatility extrapolation" (<c>:86-88</c>);
/// and both overloads' flat strategy read <c>times.back()</c> of an empty list (<c>:39</c>, <c>:44</c>). Both are
/// results C++ leaves undefined. nanoql refuses them with <see cref="ArgumentException"/> naming <c>times</c>: the
/// first with the curve overload's text, the second with "at least one time required for volatility extrapolation".
/// </summary>
public class BlackVolTimeExtrapolationRefusalTests
{
    [Fact]
    public void TheSurfaceOverloadRefusesOneTimeUnderTheLinearStrategy()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => BlackVolTimeExtrapolation.ExtrapolatedVariance(
            BlackVolTimeExtrapolationType.LinearVariance, 2.0, 100.0, [1.0], (t, k) => 0.04 * t));
        Assert.Equal("times", e.ParamName);
        Assert.StartsWith("at least two times required for volatility extrapolation", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFlatStrategyRefusesNoTimes()
    {
        ArgumentException surface = Assert.Throws<ArgumentException>(() => BlackVolTimeExtrapolation.ExtrapolatedVariance(
            BlackVolTimeExtrapolationType.FlatVolatility, 2.0, 100.0, [], (t, k) => 0.04 * t));
        ArgumentException curve = Assert.Throws<ArgumentException>(() => BlackVolTimeExtrapolation.ExtrapolatedVariance(
            BlackVolTimeExtrapolationType.FlatVolatility, 2.0, [], t => 0.04 * t));
        foreach (ArgumentException e in new[] { surface, curve })
        {
            Assert.Equal("times", e.ParamName);
            Assert.StartsWith("at least one time required for volatility extrapolation", e.Message, StringComparison.Ordinal);
        }
    }
}
