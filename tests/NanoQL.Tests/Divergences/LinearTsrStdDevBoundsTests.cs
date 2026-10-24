// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The linear TSR pricer's standard-deviations strategy (the S-cms plan's CMR5 (iii)): QuantLib sets both bounds for
/// either option, so a caplet is integrated from below its strike, takes the lognormal width k·σ without √T where the
/// normal width has it, and subtracts the shift twice (<c>lineartsrpricer.cpp:318-342</c>): on the wheel, on a flat 3%
/// curve and a 20% volatility, a 1-year coupon fixing in 10 years prices 0.03276470349655507, 0.03364469105154387 and
/// 0.03424293117331103 at k = 3, 5 and 10, moving away from the full range's 0.032367510616761526 as k grows. nanoql
/// sets each option's own bound, the width k·σ·√T and the shift once. QuantLib's check: its rate-bound strategy on the
/// corrected bounds, and the full range as k grows.
/// </summary>
public class LinearTsrStdDevBoundsTests
{
    private const double FullRange = 0.032367510616761526;

    [Fact]
    public void TheBoundsAreTheRateBoundsAtThoseStrikes()
    {
        CmsCoupon c = CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years)));
        SwaptionVolatilityStructure vol = CmsInputs.Volatility();
        Date fixing = c.FixingDate();
        double atm = c.SwapIndex.Fixing(fixing, CmsInputs.Context);
        SmileSection section = new AtmSmileSection(vol.SmileSection(fixing, c.SwapIndex.Tenor), atm);
        double sigma = section.Volatility(atm), t = section.ExerciseTime;
        const double k = 3.0;
        double width = k * sigma * Math.Sqrt(t);
        double upper = atm * Math.Exp(width - (0.5 * sigma * sigma * t));
        double lower = atm * Math.Exp(-width - (0.5 * sigma * sigma * t));

        double byStdDevs = Rate(c, new LinearTsrPricerSettings().WithBSStdDevs(k));
        Assert.Equal(Rate(c, new LinearTsrPricerSettings().WithRateBound(lower, upper)), byStdDevs);
        Assert.True(Math.Abs(byStdDevs - FullRange) <= 2e-5, $"{byStdDevs}");

        // QuantLib's: +3.97e-4 from the full range
        Assert.True(Math.Abs(0.03276470349655507 - FullRange) > 3.9e-4);
    }

    [Fact]
    public void OnAShiftedSectionTheShiftIsSubtractedOnce()
    {
        // on a 1% shift the strikes are (F + s)·exp(±k·σ·√T − σ²T/2) − s, where QuantLib's subtracts the shift again;
        // the rate-bound strategy's bounds apply to the strike plus the shift
        CmsCoupon c = CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years)));
        const double shift = 0.01;
        SwaptionVolatilityStructure vol = CmsInputs.Volatility(shift: shift);
        Date fixing = c.FixingDate();
        double atm = c.SwapIndex.Fixing(fixing, CmsInputs.Context);
        SmileSection section = new AtmSmileSection(vol.SmileSection(fixing, c.SwapIndex.Tenor), atm);
        double sigma = section.Volatility(atm), t = section.ExerciseTime;
        const double k = 3.0;
        double width = k * sigma * Math.Sqrt(t);
        double upper = ((atm + shift) * Math.Exp(width - (0.5 * sigma * sigma * t))) - shift;
        double lower = ((atm + shift) * Math.Exp(-width - (0.5 * sigma * sigma * t))) - shift;

        double byStdDevs = Rate(c, new LinearTsrPricerSettings().WithBSStdDevs(k), vol);
        Assert.Equal(Rate(c, new LinearTsrPricerSettings().WithRateBound(lower + shift, upper + shift), vol), byStdDevs);

        // QuantLib's twice-shifted bounds, within its adjusted bounds
        double quantLibs = Rate(c, new LinearTsrPricerSettings().WithRateBound(Math.Max(lower, 0.0001), upper), vol);
        Assert.True(Math.Abs(byStdDevs - quantLibs) > 1e-6, $"{byStdDevs} {quantLibs}");
    }

    [Fact]
    public void TheyConvergeToTheFullRange()
    {
        CmsCoupon c = CmsInputs.Coupon(CmsInputs.SwapIndex(new Period(10, TimeUnit.Years)));
        double[] gaps = [.. new[] { 3.0, 5.0, 10.0 }.Select(k => Math.Abs(Rate(c, new LinearTsrPricerSettings().WithBSStdDevs(k)) - FullRange))];
        Assert.True(gaps[0] > gaps[1] && gaps[1] > gaps[2] && gaps[2] <= 1e-9, string.Join(", ", gaps));
    }

    private static double Rate(CmsCoupon c, LinearTsrPricerSettings settings, SwaptionVolatilityStructure? vol = null) =>
        c.SetPricer(new LinearTsrPricer(vol ?? CmsInputs.Volatility(), 0.01, null, settings)).Rate(CmsInputs.Context);
}
