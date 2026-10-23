// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The SABR-interpolated section's strikes: QuantLib reads a strike per volatility, past the strikes' end where there
/// are fewer strikes than volatilities (<c>sabrinterpolatedsmilesection.cpp:111-119</c>), undefined behaviour. nanoql
/// refuses fewer strikes than volatilities; more strikes are ignored past the volatilities, as QuantLib's.
/// </summary>
public class SabrInterpolatedSmileSectionStrikesTests
{
    [Fact]
    public void FewerStrikesThanVolatilitiesAreRefused()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2025) };
        var option = new Date(15, Month.January, 2030);
        ArgumentException e = Assert.Throws<ArgumentException>(() => SabrInterpolatedSmileSection.Create(
            option, 0.03, [0.02, 0.03, 0.04], false, 0.0, [0.25, 0.2, 0.18, 0.2], 0.03, 0.8, 0.5, 0.0, context));
        Assert.Equal("strikes", e.ParamName);
        Assert.StartsWith("3 strikes given for 4 volatilities", e.Message, StringComparison.Ordinal);

        // More strikes than volatilities: those past the volatilities are left out.
        SabrInterpolatedSmileSection section = SabrInterpolatedSmileSection.Create(
            option, 0.03, [0.02, 0.025, 0.03, 0.035, 0.04, 0.05], false, 0.0, [0.25, 0.22, 0.2, 0.19, 0.18], 0.03, 0.8, 0.5, 0.0, context);
        Assert.Equal(0.04, section.MaxStrike());
    }
}
