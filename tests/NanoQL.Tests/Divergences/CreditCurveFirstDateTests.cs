// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The interpolated credit curves given no dates. QuantLib's constructors pass <c>dates.at(0)</c> to their base before
/// checking the count (<c>interpolatedhazardratecurve.hpp</c>, <c>interpolateddefaultdensitycurve.hpp</c>,
/// <c>interpolatedsurvivalprobabilitycurve.hpp</c>), so with no date they throw <c>std::out_of_range</c>: the wheel's
/// <c>HazardRateCurve</c>, <c>DefaultDensityCurve</c> and <c>SurvivalProbabilityCurve</c> each raise <c>IndexError:
/// invalid vector subscript</c>. nanoql refuses first, with QuantLib's own "not enough input dates given", an
/// <see cref="ArgumentException"/> naming <c>dates</c>.
/// </summary>
public class CreditCurveFirstDateTests
{
    [Fact]
    public void NoDatesAreRefusedBeforeTheFirstIsRead()
    {
        Action[] builds =
        [
            () => _ = new InterpolatedHazardRateCurve<BackwardFlat>([], [], new Actual365Fixed()),
            () => _ = new InterpolatedDefaultDensityCurve<Linear>([], [], new Actual365Fixed()),
            () => _ = new InterpolatedSurvivalProbabilityCurve<LogLinear>([], [], new Actual365Fixed()),
        ];
        foreach (Action build in builds)
        {
            ArgumentException e = Assert.Throws<ArgumentException>(build);
            Assert.Equal("dates", e.ParamName);
            Assert.StartsWith("not enough input dates given", e.Message, StringComparison.Ordinal);
        }
    }
}
