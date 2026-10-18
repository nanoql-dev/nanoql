// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The overnight coupons' undefined paths, each refused before the undefined step (the playbook's undefined-path rule),
/// and the lookback of <see cref="int.MaxValue"/> days, which <see cref="int"/>? treats as a value where QuantLib's
/// <c>Null&lt;Natural&gt;()</c> means none.
/// </summary>
public class OvernightIndexedCouponRefusalTests
{
    private static readonly Date Today = new(15, January, 2025);

    private static Sofr Sofr() => new(new FlatForward(Today, 0.04, new Actual360()));

    // O1: telescopic value dates whose rate computation is reversed. QuantLib's telescopic coupon reads
    // valueDates_.back() of an empty list (overnightindexedcoupon.cpp:115-127) before its own check, "degenerate
    // schedule" (:134), and crashes; with every daily value date the check refuses the list first.
    [Fact]
    public void AnEmptyValueDateListIsADegenerateSchedule()
    {
        var start = new Date(17, February, 2025);
        var end = new Date(17, March, 2025);
        NanoQLException e = Assert.Throws<NanoQLException>(() => new OvernightIndexedCoupon(
            end, 1e6, start, end, Sofr(), telescopicValueDates: true,
            rateComputationStartDate: new Date(20, February, 2025), rateComputationEndDate: new Date(10, February, 2025)));
        Assert.Equal("degenerate schedule", e.Message);

        // OvernightLeg's negative last recent period starts each computation after its end (:648-651).
        var schedule = new Schedule([start, end], new TARGET());
        e = Assert.Throws<NanoQLException>(() => new OvernightLeg(schedule, Sofr())
            .WithNotionals(1e6)
            .WithTelescopicValueDates(true)
            .WithLastRecentPeriod(new Period(-5, TimeUnit.Days))
            .ToLeg());
        Assert.Equal("degenerate schedule", e.Message);
    }

    // O2: QuantLib dereferences a null underlying in the base initializer (overnightindexedcoupon.cpp:273), undefined
    // under std::shared_ptr and a Boost assertion by default.
    [Fact]
    public void ACappedCouponRefusesANullUnderlying()
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() =>
            new CappedFlooredOvernightIndexedCoupon(null!, 0.05, 0.01));
        Assert.Equal("underlying", e.ParamName);
    }

    // O3: QuantLib's conversion counts schedule_.size()-1 periods, a Size that wraps below zero
    // (overnightindexedcoupon.cpp:593), and reads schedule_.date(0) past the end, or prints the wrapped count in its
    // payment-date check (:596-601). nanoql's conversion refuses the schedule where the count wraps, after the
    // notional and pricer checks QuantLib makes before it.
    [Fact]
    public void AnOvernightLegRefusesAnEmptySchedule()
    {
        var leg = new OvernightLeg(new Schedule([], new TARGET()), Sofr());
        NanoQLException e = Assert.Throws<NanoQLException>(() => leg.WithNotionals(1e6).ToLeg());
        Assert.Equal("empty schedule", e.Message);
        e = Assert.Throws<NanoQLException>(() => leg.WithNotionals(1e6).WithPaymentDates([Today]).ToLeg());
        Assert.Equal("empty schedule", e.Message);

        // A one-date schedule stays QuantLib's: an empty leg (D12).
        Assert.Empty(new OvernightLeg(new Schedule([Today], new TARGET()), Sofr()).WithNotionals(1e6).ToLeg());
    }

    // O4: QuantLib's ClosestRounding reads pow10_lut[precision & 0x1F] (rounding.cpp:29-38), its documented range
    // being [0, 16] (rounding.hpp:31-34): a coupon rounded to 17 places pays NaN, and -30 rounds as 2
    // (overnightindexedcoupon.cpp:212-213). The coupon and OvernightLeg refuse the precision on entry, with the text
    // QuantLib's swap gives (overnightindexedswap.cpp:178-180).
    [Fact]
    public void ARoundingPrecisionOutsideItsRangeIsRefused()
    {
        var start = new Date(17, February, 2025);
        var end = new Date(17, March, 2025);
        foreach (int precision in new[] { 17, 31, -1, -30 })
        {
            ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new OvernightIndexedCoupon(end, 1e6, start, end, Sofr(), roundingPrecision: precision));
            Assert.Equal("roundingPrecision", e.ParamName);
            Assert.StartsWith($"rounding precision ({precision}) must be between 0 and 16", e.Message, StringComparison.Ordinal);

            e = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new OvernightLeg(new Schedule([start, end], new TARGET()), Sofr()).WithRoundingPrecision(precision));
            Assert.Equal("roundingPrecision", e.ParamName);
        }

        // The range's ends are kept.
        Assert.True(new OvernightIndexedCoupon(end, 1e6, start, end, Sofr(), roundingPrecision: 0).Amount(
            new EvaluationContext { EvaluationDate = Today }) == 0.0);
        _ = new OvernightIndexedCoupon(end, 1e6, start, end, Sofr(), roundingPrecision: 16);
    }

    // F1, O5: QuantLib's lookback and lockout days are Natural, which can't hold a negative; from C++ a negative wraps
    // and is negated back where QuantLib advances by it (overnightindexedcoupon.cpp:46, 123-124), and 2^31 negates
    // INT_MIN.
    [Fact]
    public void NegativeLookbackAndLockoutDaysAreRefused()
    {
        var start = new Date(17, February, 2025);
        var end = new Date(17, March, 2025);
        var schedule = new Schedule([start, end], new TARGET());

        Assert.Equal("lookbackDays", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OvernightIndexedCoupon(end, 1e6, start, end, Sofr(), lookbackDays: -1)).ParamName);
        Assert.Equal("lockoutDays", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OvernightIndexedCoupon(end, 1e6, start, end, Sofr(), lockoutDays: -1)).ParamName);
        Assert.Equal("lookbackDays", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OvernightLeg(schedule, Sofr()).WithLookbackDays(int.MinValue)).ParamName);
        Assert.Equal("lockoutDays", Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OvernightLeg(schedule, Sofr()).WithLockoutDays(-1)).ParamName);

        // Zero stays QuantLib's.
        Assert.Equal(0, new OvernightIndexedCoupon(end, 1e6, start, end, Sofr(), lookbackDays: 0).FixingDays);
    }

    // SD27, D22: QuantLib's Null<Natural>() is INT_MAX, so a lookback of 2147483647 days means none and the coupon
    // takes the index's fixing days (overnightindexedcoupon.hpp:71, floatingratecoupon.cpp:49). Here it is a value:
    // the coupon advances its value dates back by it, which Date's range refuses.
    [Fact]
    public void ALookbackOfIntMaxValueIsAValue()
    {
        var start = new Date(17, February, 2025);
        var end = new Date(17, March, 2025);
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OvernightIndexedCoupon(end, 1e6, start, end, Sofr(), lookbackDays: int.MaxValue));
        Assert.Contains("outside allowed range", e.Message, StringComparison.Ordinal);

        // QuantLib's null: no lookback, the index's fixing days.
        Assert.Equal(0, new OvernightIndexedCoupon(end, 1e6, start, end, Sofr(), lookbackDays: null).FixingDays);
    }
}
