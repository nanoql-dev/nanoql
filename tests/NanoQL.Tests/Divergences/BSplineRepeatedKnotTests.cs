// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Cubic B-splines on a repeated knot (the S-sr plan's SSR6 (vi)). QuantLib's recursion divides by a knot span that is
/// zero on a repeated knot (<c>bspline.cpp:56-57</c>), giving NaN, so a cubic B-spline fit on the knots −30, −20, 0, 5,
/// 10, 10, 15, 20, 25 and 30 throws "N_th cubic B-spline must be nonzero at t=0" on the wheel, where the knots 10 and
/// 10.001 fit, the parameters 0.9, 0.8, 0.7, 0.6 and 0.5 discounting 0.9194765141427983 at t = 7. nanoql reads a term
/// over a zero span as 0, the Cox–de Boor convention. QuantLib has no value on a repeated knot, so its check is its own
/// values on knots a gap apart, which nanoql gives bit for bit, converging to the convention's as the gap closes.
/// </summary>
public class BSplineRepeatedKnotTests
{
    [Fact]
    public void ARepeatedKnotIsTheLimitOfKnotsAGapApart()
    {
        double[] Knots(double gap) => [-30.0, -20.0, 0.0, 5.0, 10.0, 10.0 + gap, 15.0, 20.0, 25.0, 30.0];
        var repeated = new BSpline(3, 5, Knots(0.0));
        double[] points = [-25.0, -10.0, 2.0, 7.0, 9.5, 12.0, 17.0, 22.0, 28.0];

        double previous = double.MaxValue;
        foreach (double gap in new[] { 1e-2, 1e-4, 1e-6 })
        {
            var apart = new BSpline(3, 5, Knots(gap));
            double worst = 0.0;
            for (int i = 0; i <= 5; ++i)
            {
                foreach (double x in points)
                {
                    double value = repeated.Evaluate(i, x);
                    Assert.False(double.IsNaN(value));
                    worst = Math.Max(worst, Math.Abs(value - apart.Evaluate(i, x)));
                }
            }

            Assert.True(worst < previous && worst <= gap, $"gap {gap}: {worst}");
            previous = worst;
        }

        // the fit QuantLib refuses builds, and QuantLib's fit a gap apart on the wheel
        var today = new Date(15, Month.January, 2025);
        var dc = new Actual365Fixed();
        double[] parameters = [0.9, 0.8, 0.7, 0.6, 0.5];
        Date maxDate = today + new Period(25, TimeUnit.Years);
        var onRepeated = new FittedBondDiscountCurve(today, new CubicBSplinesFitting(Knots(0.0)), parameters, maxDate, dc);
        var gapApart = new FittedBondDiscountCurve(today, new CubicBSplinesFitting(Knots(0.001)), parameters, maxDate, dc);
        Assert.True(Math.Abs((gapApart.Discount(7.0) / 0.9194765141427983) - 1.0) <= 1e-14, $"{gapApart.Discount(7.0)}");
        Assert.True(Math.Abs(onRepeated.Discount(7.0) - gapApart.Discount(7.0)) <= 1e-2, $"{onRepeated.Discount(7.0)}");
    }
}
