// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The piecewise forward-spreaded curve before its first spread date (the S-sr plan's SSR6 (vii)). QuantLib's zero rate
/// at a time adds the spread's integral over the time, which it takes from the interpolation's first time
/// (<c>piecewiseforwardspreadedtermstructure.hpp:152-166</c>), leaving out the flat spread its own <c>calcSpread</c>
/// applies before that time, and reading the interpolation extrapolated backwards before it. On the wheel, spreads of 2%
/// and 3% 100 and 150 days after 15 January 2025 over a flat 2% Actual/365 (Fixed) curve, QuantLib's test set-up
/// (<c>termstructures.cpp:325-370</c>), give the forward 4.4% at 120 days, but every discount past 100 days is
/// e^(0.02·100/365) − 1 = 0.5494% too high, 0.99224937 at 120 days against 0.98682726; and QuantLib's own backward-flat
/// case (<c>termstructures.cpp:508</c>), on a flat 2% Actual/360 base, gives the zero rate 2.3019% at 110 days, its
/// spread's average 0.30%, where the forwards' average is 2.10%. nanoql integrates the spread from the reference date,
/// flat before the first spread's time. QuantLib's check: its own curve with the first spread prepended at the reference
/// date, whose integral starts at 0, gives nanoql's curve bit for bit on the case's set-up; the zero rate is the
/// forwards' average.
/// </summary>
public class ForwardSpreadFirstDateTests
{
    private static readonly Date Today = new(15, Month.January, 2025);

    [Fact]
    public void TheSpreadIsIntegratedFromTheReferenceDate()
    {
        var dc = new Actual365Fixed();
        YieldTermStructure baseCurve = new FlatForward(Today, 0.02, dc);
        Date d1 = Today + 100, d2 = Today + 150;
        var curve = new InterpolatedPiecewiseForwardSpreadedTermStructure<Linear>(baseCurve, [0.02, 0.03], [d1, d2])
            .EnableExtrapolation();

        // QuantLib's curve with the first spread prepended at the reference date, which its integral starts at
        var prepended =
            new InterpolatedPiecewiseForwardSpreadedTermStructure<Linear>(baseCurve, [0.02, 0.02, 0.03], [Today, d1, d2])
                .EnableExtrapolation();

        for (int n = 1; n <= 400; ++n)
        {
            Date d = Today + n;
            Assert.Equal(prepended.Discount(d), curve.Discount(d));
        }

        // QuantLib's prepended curve on the wheel, and its 1.44 curve
        Assert.True(Math.Abs((curve.Discount(Today + 120) / 0.9868272561420659) - 1.0) <= 1e-15, $"{curve.Discount(Today + 120)}");
        Assert.True(curve.Discount(Today + 120) / 0.9922493703194949 < 0.99455, $"{curve.Discount(Today + 120)}");

        double t = dc.YearFraction(Today, Today + 120);
        double forward = curve.ForwardRate(t, t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;
        Assert.True(Math.Abs(forward - 0.0439999999992532) <= 1e-12, $"{forward}");
    }

    // QuantLib's own testBackwardFlatInterpolationZeroRate, moved here at line 508, on a flat base of a fixed date: its
    // expected zero rate is the base's plus the spread's backward-flat primitive from the first spread's time.
    [Fact]
    public void QuantLibsBackwardFlatZeroRateLeavesOutTheFlatSpread()
    {
        var calendar = new TARGET();
        var dc = new Actual360();
        Date referenceDate = calendar.Advance(Today, 2, TimeUnit.Days);
        YieldTermStructure baseCurve = new FlatForward(referenceDate, 0.02, dc);
        double[] spreads = [0.02, 0.03, 0.04];
        Date[] spreadDates =
        [
            calendar.Advance(Today, 100, TimeUnit.Days),
            calendar.Advance(Today, 200, TimeUnit.Days),
            calendar.Advance(Today, 300, TimeUnit.Days),
        ];
        var curve = new InterpolatedPiecewiseForwardSpreadedTermStructure<BackwardFlat>(baseCurve, spreads, spreadDates);
        double t = dc.YearFraction(Today, calendar.Advance(Today, 110, TimeUnit.Days));
        double zero = curve.ZeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true).Rate;

        // the forwards' average: the base's 2% and the spread's integral from the reference date
        double t0 = dc.YearFraction(referenceDate, spreadDates[0]);
        double expected = 0.02 + (((0.02 * t0) + (0.03 * (t - t0))) / t);
        Assert.True(Math.Abs(zero - expected) <= 1e-12, $"{zero} against {expected}");

        // QuantLib's prepended curve on the wheel, and its 1.44 value, which its case asserts
        Assert.True(Math.Abs(zero - 0.041006289308175986) <= 1e-14, $"{zero}");
        Assert.True(Math.Abs(zero - 0.023018867924528015) > 0.0179, $"{zero}");
    }
}
