// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Exponential splines with a fixed κ (the S-sr plan's SSR6 (v)). QuantLib's discount function sums one term fewer than
/// its size (<c>nonlinearfittingmethods.cpp:84</c>, <c>:91</c>), right when the last parameter is κ and wrong when κ is
/// fixed: the last coefficient is dead. On the wheel, constrained at zero with nine coefficients and κ = 0.5, the
/// coefficients 0.3, −0.2, 0.15, 0.1, −0.05, 0.08, 0.02 and −0.01 discount 0.4584577385303755 at t = 1, as they do with
/// the last one 0.49, where the method with κ free gives 0.46441195516211947 and 0.1667011235749239. nanoql sums every
/// coefficient when κ is fixed. QuantLib's check: its free-κ method's discount function at the same coefficients and κ.
/// </summary>
public class ExponentialSplinesFixedKappaTests
{
    [Fact]
    public void AFixedKappaDiscountsAsTheFreeMethodAtThatKappa()
    {
        var today = new Date(15, Month.January, 2025);
        var dc = new Actual365Fixed();
        Date maxDate = today + new Period(30, TimeUnit.Years);
        var fixedKappa = new ExponentialSplinesFitting(true, 9, 0.5);
        var freeKappa = new ExponentialSplinesFitting(true);
        Assert.Equal(8, fixedKappa.Size);

        double[] coefficients = [0.3, -0.2, 0.15, 0.1, -0.05, 0.08, 0.02, -0.01];
        var fixedCurve = new FittedBondDiscountCurve(today, fixedKappa, coefficients, maxDate, dc);
        var freeCurve = new FittedBondDiscountCurve(today, freeKappa, [.. coefficients, 0.5], maxDate, dc);
        foreach (double t in new[] { 0.25, 1.0, 5.0, 12.0, 29.0 })
        {
            Assert.Equal(freeCurve.Discount(t), fixedCurve.Discount(t));
        }

        // QuantLib's free-κ discount on the wheel, and its fixed-κ one, which leaves out the last coefficient
        Assert.True(Math.Abs((fixedCurve.Discount(1.0) / 0.46441195516211947) - 1.0) <= 1e-14, $"{fixedCurve.Discount(1.0)}");
        Assert.True(Math.Abs(fixedCurve.Discount(1.0) - 0.4584577385303755) > 5e-3, $"{fixedCurve.Discount(1.0)}");

        // the last coefficient isn't dead
        double[] moved = [.. coefficients[..^1], 0.49];
        var movedCurve = new FittedBondDiscountCurve(today, fixedKappa, moved, maxDate, dc);
        Assert.True(Math.Abs((movedCurve.Discount(1.0) / 0.1667011235749239) - 1.0) <= 1e-14, $"{movedCurve.Discount(1.0)}");
    }
}
