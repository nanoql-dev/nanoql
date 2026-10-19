// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An interpolated smile section given more strikes than standard deviations. QuantLib sizes its volatilities by the
/// standard deviations and interpolates over the strikes' count of them
/// (<c>interpolatedsmilesection.hpp:117, 124-125</c>), reading past the volatilities: over strikes 1%, 2%, 3% and 4%
/// with standard deviations 0.4 and 0.3, the wheel's linear section gives 0.14999999999999994 at 2.5% and 0.0 at 3.5%,
/// memory past the vector. nanoql refuses more strikes than standard deviations with
/// <see cref="ArgumentException"/>, and keeps QuantLib's behaviour for fewer, ignoring the extra standard deviations.
/// </summary>
public class InterpolatedSmileSectionCountTests
{
    [Fact]
    public void MoreStrikesThanStandardDeviationsAreRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new LinearInterpolatedSmileSection(1.0, [0.01, 0.02, 0.03, 0.04], [0.4, 0.3], 0.02));
        Assert.Equal("strikes", e.ParamName);
        Assert.StartsWith("4 strikes given for 2 standard deviations", e.Message, StringComparison.Ordinal);

        // Fewer strikes: the wheel's 0.35000000000000003 at 1.5% and 0.24999999999999994 at 2.5%.
        var fewer = new LinearInterpolatedSmileSection(1.0, [0.01, 0.02], [0.4, 0.3, 0.2, 0.1], 0.02);
        Assert.Equal(0.35000000000000003, fewer.Volatility(0.015));
        Assert.Equal(0.24999999999999994, fewer.Volatility(0.025));
    }
}
