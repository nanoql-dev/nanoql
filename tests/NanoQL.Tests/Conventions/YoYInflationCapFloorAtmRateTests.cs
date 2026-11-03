// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The year-on-year cap's at-the-money rate, which no vector reads: QuantLib's <c>CashFlows::atmRate(yoyLeg_,
/// discountCurve, false, discountCurve.referenceDate())</c> (<c>inflationcapfloor.cpp:214-217</c>). On
/// <see cref="InflationFixture"/>'s market with a year-on-year curve from 2% in April 2024 to 4% in April 2034, a
/// three-year annual leg of 1,000,000 from 15 June 2024, lagged two months and observed flat, forecasts the rates
/// 0.021998904709748083, 0.02399780941949617 and 0.02599671412924425, and the wheel's <c>CashFlows.atmRate</c> on the
/// same leg and the nominal curve gives 0.023944593435950047.
/// </summary>
public class YoYInflationCapFloorAtmRateTests
{
    [Fact]
    public void TheAtmRateIsTheLegsOnTheDiscountCurve()
    {
        EvaluationContext context = InflationFixture.Context();
        var index = new YoYInflationIndex(
            new UKRPI(),
            InflationFixture.YoYCurve([new(1, Month.April, 2024), new(1, Month.April, 2034)], [0.02, 0.04]));
        ImmutableArray<CashFlow> leg = new YoYInflationLeg(
                new Schedule(
                    InflationFixture.Today,
                    new Date(15, Month.June, 2027),
                    new Period(1, TimeUnit.Years),
                    new UnitedKingdom(),
                    BusinessDayConvention.Unadjusted,
                    BusinessDayConvention.Unadjusted,
                    DateGenerationRule.Forward,
                    false,
                    context),
                new UnitedKingdom(),
                index,
                new Period(2, TimeUnit.Months),
                CPIInterpolationType.Flat)
            .WithNotionals(1e6)
            .WithPaymentDayCounter(new Actual365Fixed())
            .ToLeg();
        var cap = new YoYInflationCap(leg, [0.03]);

        Assert.Equal(3, leg.Length);
        Assert.False(cap.IsExpired(context));
        double rate = cap.AtmRate(InflationFixture.Nominal(), context);
        Assert.True(Tolerance.Within(0.023944593435950047, rate), $"{rate}");
    }
}
