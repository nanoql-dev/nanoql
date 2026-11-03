// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A capped year-on-year coupon read again: QuantLib's <c>CappedFlooredYoYInflationCoupon::rate</c> prices its caplet
/// and floorlet on the pricer's state (<c>capflooredinflationcoupon.cpp:92-105</c>), which its swaplet rate sets only
/// when first computed, the coupon's <c>InflationCoupon::rate</c> being cached (<c>inflationcoupon.cpp:64-75</c>), so
/// with one pricer on a leg a coupon read again after another reads that other coupon's state: on the wheel, on the
/// vectors' <c>one cap</c> leg (six annual coupons capped at 3.5%, the Black pricer), the first coupon's amount read
/// after the leg's rates is 28,665.871612869618 on 1e6, where read first it is 30,024.190259687595, its rate's.
/// QuantLib's capped Ibor and digital coupons read their underlying's pricer the same way after its cached rate, and
/// its stripped coupon initializes the pricer first (<c>strippedcapflooredcoupon.cpp:46-47</c>). nanoql's pricers take
/// the coupon on every call, so a coupon's amount is the same however often and in whatever order its leg is read.
/// QuantLib's check: its amounts read in turn, each coupon's rate and amount before the next coupon's (the vectors).
/// </summary>
public class CappedYoYPricerStateTests
{
    [Fact]
    public void ACouponReadAgainAfterItsLegKeepsItsAmount()
    {
        VectorCase c = VectorFile.Load("cashflows/capflooredinflationcoupon").Cases
            .Single(x => x.Name.StartsWith("leg one cap /", StringComparison.Ordinal));
        (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
        ImmutableArray<CashFlow> leg = InflationCouponPricerFunctions.SetCouponPricer(
            CappedFlooredYoYInflationCouponVectorTests.LegOf(c.Input("leg"), (YoYInflationIndex)index, context),
            CappedFlooredYoYInflationCouponVectorTests.PricerOf(c, context));
        Assert.All(leg, f => Assert.IsType<CappedFlooredYoYInflationCoupon>(f));

        double first = leg[0].Amount(context);
        double[] rates = [.. leg.Select(f => ((Coupon)f).Rate(context))];
        double again = leg[0].Amount(context);

        Assert.Equal(first, again);
        Assert.Equal(rates[0] * 1e6, again, 1e-9);
        Assert.True(Math.Abs(again - 30024.190259687595) <= 1e-9, $"{again}");
        Assert.True(Math.Abs(again - 28665.871612869618) > 1.0, $"{again}");
    }
}
