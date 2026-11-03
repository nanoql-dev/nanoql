// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The caplet variance curve's smile sections: QuantLib's is a <c>FlatSmileSection(t, atmVol, dayCounter())</c>
/// (<c>capletvariancecurve.hpp:86-91</c>), shifted lognormal with shift 0 whatever the curve's type and displacement,
/// so a normal curve's section prices its normal volatility as a lognormal one. nanoql's section carries the curve's
/// type and displacement; the check is the curve's own <see cref="CapletVarianceCurve.VolatilityType"/> and
/// <see cref="CapletVarianceCurve.Displacement"/>.
/// </summary>
public class CapletVarianceCurveSmileTests
{
    [Fact]
    public void TheSectionsKeepTheCurvesTypeAndDisplacement()
    {
        var today = new Date(15, Month.January, 2025);
        Date[] dates = [new(15, Month.July, 2025), new(15, Month.January, 2026), new(15, Month.January, 2027)];
        var normal = new CapletVarianceCurve(today, dates, [0.0085, 0.0090, 0.0095], new Actual360(), VolatilityType.Normal);
        var displaced = new CapletVarianceCurve(
            today, dates, [0.12, 0.115, 0.11], new Actual365Fixed(), VolatilityType.ShiftedLognormal, 0.02);

        SmileSection n = normal.SmileSection(dates[1]);
        SmileSection s = displaced.SmileSection(dates[1]);

        Assert.Equal(normal.VolatilityType, n.VolatilityType);
        Assert.Equal(normal.Displacement, n.Shift);
        Assert.Equal(displaced.VolatilityType, s.VolatilityType);
        Assert.Equal(displaced.Displacement, s.Shift);
        Assert.Equal(normal.Volatility(dates[1], 0.03), n.Volatility(0.03));
    }
}
