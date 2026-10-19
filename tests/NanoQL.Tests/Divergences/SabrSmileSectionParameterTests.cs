// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A SABR smile section given fewer than four parameters. QuantLib reads α, β, ν and ρ from the vector unchecked
/// (<c>sabrsmilesection.cpp:52-55</c>): with three, the wheel's ρ is −2.654651770048e-310, memory past the vector; with
/// none, the process ends with an access violation. nanoql refuses fewer than four with
/// <see cref="ArgumentException"/>, in the text of QuantLib's no-arbitrage SABR section's check
/// (<c>noarbsabrsmilesection.cpp:46-48</c>).
/// </summary>
public class SabrSmileSectionParameterTests
{
    [Fact]
    public void FewerThanFourParametersAreRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new SabrSmileSection(1.0, 0.035, [0.3, 0.6, 0.02]));
        Assert.Equal("sabrParameters", e.ParamName);
        Assert.StartsWith("sabr expects 4 parameters (alpha,beta,nu,rho) but (3) given", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => new SabrSmileSection(1.0, 0.035, []));
        Assert.Equal("sabrParameters", e.ParamName);
        Assert.StartsWith("sabr expects 4 parameters (alpha,beta,nu,rho) but (0) given", e.Message, StringComparison.Ordinal);

        // A fifth parameter is ignored, as QuantLib ignores it.
        var five = new SabrSmileSection(1.0, 0.035, [0.3, 0.6, 0.02, 0.01, 7.0]);
        Assert.Equal(new SabrSmileSection(1.0, 0.035, [0.3, 0.6, 0.02, 0.01]).Volatility(0.04), five.Volatility(0.04));
    }
}
