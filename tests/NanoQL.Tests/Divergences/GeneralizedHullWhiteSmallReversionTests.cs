// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Generalized Hull-White's analytical fitting parameter and its Ornstein-Uhlenbeck process's variance at small and
/// negative reversion, in their cancellation-free forms through expm1 below |a| 1e-2 (the S-xrt plan's XRR5 (xix), fixed
/// under R8). QuantLib cuts both at a &lt; √ε to the zero-reversion forms σt and σ²Δt, which jump there and cancel just
/// above, and which every negative reversion takes. On QuantLib 1.44 (the S-xrt planner's <c>xp_c3.cpp</c>: σ 0.01, a
/// flat 3% curve, φ at 5 years, the variance over a year) the variance at −0.05 is 1e-4, 4.92e-2 low, and φ(5)
/// 0.0312500000015574, 3.63e-4 low; at 1.4e-8 the variance is 1e-4, 1.4e-8 relative off; at 2e-8 4.47e-11. nanoql's
/// values meet 40-digit references (mpmath) to a few ulps at every reversion from −0.1 to 0.1, QuantLib's formula from
/// |a| 1e-2.
/// </summary>
public class GeneralizedHullWhiteSmallReversionTests
{
    [Theory]
    [InlineData(-0.1, 0.000110701379080084917595522323947)]
    [InlineData(-0.05, 0.000105170918075647625108480085031)]
    [InlineData(-0.01, 0.0001010067001337790508218159327)]
    [InlineData(-0.005, 0.000100501670841680575432132559727)]
    [InlineData(-1e-6, 0.000100000100000066666699995488138)]
    [InlineData(0.0, 0.0001)]
    [InlineData(1e-9, 0.0000999999999000000000666666604052)]
    [InlineData(1.4e-8, 0.0000999999986000000130666666120829)]
    [InlineData(2e-8, 0.0000999999980000000266666663581549)]
    [InlineData(1e-6, 0.0000999999000000666666333378718494)]
    [InlineData(0.005, 0.0000995016625083194642506010167667)]
    [InlineData(0.0099, 0.0000990165017843579982532493395394)]
    [InlineData(0.01, 0.0000990066334662234888753882823058)]
    [InlineData(0.05, 0.0000951625819640404265760231368202)]
    [InlineData(0.1, 0.0000906346234610090701786686211376)]
    public void TheProcessVarianceMeetsItsReference(double a, double reference)
    {
        var process = new GeneralizedOrnsteinUhlenbeckProcess(_ => a, _ => 0.01);
        double variance = process.Variance(0.0, 0.0, 1.0);
        Assert.True(Math.Abs(variance - reference) <= 2e-14 * reference, $"{variance} for {reference}");
    }

    [Theory]
    [InlineData(-0.1, 0.0321041964352939447715648857626)]
    [InlineData(-0.05, 0.0316134087464929035973587062399)]
    [InlineData(-0.01, 0.0313143626617997727097157101725)]
    [InlineData(-0.005, 0.0312817106543327166836209561205)]
    [InlineData(-1e-6, 0.0312500062500182292057289511151)]
    [InlineData(0.0, 0.03125)]
    [InlineData(1e-9, 0.0312499999937500000182291662383)]
    [InlineData(1.4e-8, 0.0312499999125000035729165617843)]
    [InlineData(1e-6, 0.0312499937500182291276045167635)]
    [InlineData(0.005, 0.0312192008880973436742730950803)]
    [InlineData(0.01, 0.0311892845172657774894716770496)]
    [InlineData(0.1, 0.0307740906087308773522373212248)]
    public void TheFittingParameterMeetsItsReference(double a, double reference)
    {
        var today = new Date(15, Month.June, 2026);
        var model = new GeneralizedHullWhite(new FlatForward(today, 0.03, new Actual365Fixed()), a, 0.01);

        // the short rate at a state of 0 is φ(t); the curve's instantaneous forward by its finite difference, 1.6e-12
        // off 3%
        double phi = model.HwDynamics().ShortRate(5.0, 0.0);
        Assert.True(Math.Abs(phi - reference) <= 1e-11, $"{phi} for {reference}");
    }

    [Fact]
    public void QuantLibsCutIsFarOffAtANegativeReversion()
    {
        // QuantLib 1.44's variance and φ(5) at −0.05, the cut's σ²Δt and σt forms
        var process = new GeneralizedOrnsteinUhlenbeckProcess(_ => -0.05, _ => 0.01);
        Assert.True(Math.Abs(process.Variance(0.0, 0.0, 1.0) - 1e-4) > 4.9e-2 * 1e-4);

        var model = new GeneralizedHullWhite(new FlatForward(new Date(15, Month.June, 2026), 0.03, new Actual365Fixed()), -0.05, 0.01);
        Assert.True(Math.Abs(model.HwDynamics().ShortRate(5.0, 0.0) - 0.0312500000015574) > 3.6e-4);
    }
}
