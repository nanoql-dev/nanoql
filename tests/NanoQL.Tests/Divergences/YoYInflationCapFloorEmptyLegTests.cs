// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The last year-on-year coupon of a cap on an empty leg. QuantLib's <c>lastYoYInflationCoupon()</c> reads
/// <c>yoyLeg_.back()</c> (<c>inflationcapfloor.cpp:109-115</c>), which is undefined for an empty leg; the wheel doesn't
/// bind it. nanoql refuses with <see cref="NanoQLException"/> and the text QuantLib's <c>CashFlows</c> gives an empty
/// leg, "empty leg", as <see cref="CapFloor.LastFloatingRateCoupon"/> does. The rest of the cap is QuantLib's defined
/// behaviour, as the wheel gives it: it is expired and prices at 0.
/// </summary>
public class YoYInflationCapFloorEmptyLegTests
{
    [Fact]
    public void TheLastCouponOfAnEmptyLegIsRefused()
    {
        EvaluationContext context = InflationFixture.Context();
        var cap = new YoYInflationCap([], [0.03]);

        Assert.Equal("empty leg", Assert.Throws<NanoQLException>(() => cap.LastYoYInflationCoupon()).Message);

        // QuantLib's defined answers on the same cap: the wheel's values.
        Assert.True(cap.IsExpired(context));
        var index = new YoYInflationIndex(
            new UKRPI(), InflationFixture.YoYCurve([new(1, Month.April, 2024), new(1, Month.April, 2034)], [0.03, 0.03]));
        ConstantYoYOptionletVolatility vol = ConstantYoYOptionletVolatility.Create(
            0.15,
            0,
            new UnitedKingdom(),
            BusinessDayConvention.ModifiedFollowing,
            new Actual365Fixed(),
            new Period(2, TimeUnit.Months),
            Frequency.Monthly,
            false,
            context);
        Assert.Equal(0.0, YoYInflationBlackCapFloorEngine.Calculate(cap, context, index, vol, InflationFixture.Nominal()).NPV);
    }
}
