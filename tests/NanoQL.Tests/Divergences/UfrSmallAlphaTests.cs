// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The ultimate-forward curve's kernel at small α. QuantLib's B(x) = (1 − e^(−αx))/(αx)
/// (<c>ultimateforwardtermstructure.hpp:169</c>) is unguarded: on the wheel, over a flat 3% Actual/365 (Fixed) curve
/// from 15 January 2025 with an LLFR of 3%, a UFR of 4% and a 20-year first smoothing point, α = 0 refuses the 60-year
/// zero rate with "positive compound factor required", α = 1e-12 gives 0.029999995243582964 against its limit 0.03, and
/// α = 0.1 gives 0.035031562480595566. nanoql's kernel is −expm1(−αx)/(αx), and 1 at 0, as upstream's own
/// <c>oneMinusExpOverX</c> after the pin. QuantLib's check: the limit, and its own values at ordinary α.
/// </summary>
public class UfrSmallAlphaTests
{
    [Fact]
    public void ASmallAlphaGivesTheLimit()
    {
        var today = new Date(15, Month.January, 2025);
        var dc = new Actual365Fixed();
        YieldTermStructure baseCurve = new FlatForward(today, 0.03, dc);
        Date sixty = today + new Period(60, TimeUnit.Years);
        double Zero(double alpha) =>
            new UltimateForwardTermStructure(baseCurve, 0.03, 0.04, new Period(20, TimeUnit.Years), alpha)
                .ZeroRate(sixty, dc, Compounding.Continuous).Rate;

        // at α = 0 the forward past the cut-off is the LLFR, the base's 3%; at α = 1e-12 the kernel is 1 − αΔ/2 to
        // second order, which puts the zero rate 1.3e-13 above that limit
        Assert.True(Math.Abs(Zero(0.0) - 0.03) <= 1e-15, $"{Zero(0.0)}");
        double cutOff = baseCurve.TimeFromReference(today + new Period(20, TimeUnit.Years));
        double t = baseCurve.TimeFromReference(sixty);
        double x = 1e-12 * (t - cutOff);
        double expected = ((cutOff * 0.03) + ((t - cutOff) * (0.04 - (0.01 * (1.0 - (x / 2.0)))))) / t;
        Assert.True(Math.Abs(Zero(1e-12) - expected) <= 1e-15, $"{Zero(1e-12)} against {expected}");
        Assert.True(Math.Abs(Zero(1e-12) - 0.029999995243582964) > 4e-9, $"{Zero(1e-12)}");
        Assert.True(Math.Abs((Zero(0.1) / 0.035031562480595566) - 1.0) <= 1e-12, $"{Zero(0.1)}");
    }
}
