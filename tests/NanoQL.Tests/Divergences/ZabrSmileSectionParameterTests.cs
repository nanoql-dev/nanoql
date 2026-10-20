// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A short-maturity ZABR smile section given fewer than five parameters. QuantLib's short-maturity <c>init</c> builds
/// its model from <c>params_[0]</c> to <c>params_[4]</c> unchecked (<c>zabrsmilesection.hpp:139-152</c>), past the end
/// of a shorter vector: the wheel's sections with α, β, ν and ρ alone gave the lognormal volatility
/// 0.21300493901013692 at 4% in some runs and NaN in others, where γ = 1 given gives 0.21305710815847104. Only its
/// local-volatility kernel checks the count, "zabr expects 5 parameters (alpha,beta,nu,rho,gamma) but (4) given"
/// (<c>:158-160</c>). nanoql refuses fewer than five for every kernel, with that text, naming <c>zabrParameters</c>.
/// </summary>
public class ZabrSmileSectionParameterTests
{
    [Fact]
    public void FewerThanFiveParametersAreRefused()
    {
        double[] four = [0.08, 0.7, 0.2, -0.3];
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        Action[] builds =
        [
            () => _ = new ZabrShortMaturityLognormalSmileSection(5.0, 0.03, four),
            () => _ = new ZabrShortMaturityNormalSmileSection(5.0, 0.03, four),
            () => _ = ZabrShortMaturityLognormalSmileSection.Create(new Date(15, Month.January, 2030), 0.03, four, context),
            () => _ = new ZabrSmileSection<ZabrShortMaturityNormal>(5.0, 0.03, []),
        ];
        foreach (Action build in builds)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(build);
            Assert.Equal("zabrParameters", e.ParamName);
        }

        Assert.Equal(0.21305710815847104, new ZabrShortMaturityLognormalSmileSection(5.0, 0.03, [.. four, 1.0]).Volatility(0.04));
    }
}
