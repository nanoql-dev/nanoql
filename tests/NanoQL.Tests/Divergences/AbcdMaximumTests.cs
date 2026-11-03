// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The abcd function's maximum. Its location is QuantLib's master's (#2841, after the pin): QuantLib 1.44 returns the
/// stationary point 1/c − a/b whatever b's sign, a minimum when b &lt; 0. Its value is the function at that location:
/// QuantLib returns d whenever b = 0 or a ≤ 0. QuantLib's values are the wheel's; the check is QuantLib's own
/// <c>operator()</c>.
/// </summary>
public class AbcdMaximumTests
{
    [Fact]
    public void ANegativeBsStationaryPointIsAMinimum()
    {
        // the wheel: maximumLocation() 1.2, where f is 0.04940289404389894; f(0) is 0.30000000000000004
        var f = new AbcdMathFunction(0.1, -0.5, 1.0, 0.2);

        Assert.Equal(0.0, f.MaximumLocation());
        Assert.True(f.Evaluate(0.0) > f.Evaluate(1.2));
        Assert.Equal(f.Evaluate(0.0), f.MaximumValue());
    }

    [Fact]
    public void TheMaximumValueIsTheFunctionAtItsLocation()
    {
        // the wheel: AbcdFunction()'s defaults give maximumValue() 0.17 and f(maximumLocation()) 0.26571704020530196;
        // with b = 0 and a > 0, 0.1 and f(0) 0.30000000000000004
        var defaults = new AbcdMathFunction(-0.06, 0.17, 0.54, 0.17);
        var flat = new AbcdMathFunction(0.2, 0.0, 0.5, 0.1);

        Assert.Equal(0.26571704020530196, defaults.MaximumValue());
        Assert.Equal(defaults.Evaluate(defaults.MaximumLocation()), defaults.MaximumValue());
        Assert.Equal(0.30000000000000004, flat.MaximumValue());
    }
}
