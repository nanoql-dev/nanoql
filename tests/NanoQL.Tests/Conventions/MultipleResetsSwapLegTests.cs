// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The multiple-resets swap's floating leg (the S-cms plan's CMD7): QuantLib's <c>MultipleResetsSwap</c> builds its
/// <c>FixedVsFloatingSwap</c> base on the coupon schedule, every resets-per-coupon-th reset date, then replaces the
/// base's floating leg with a <c>MultipleResetsLeg</c> on the full reset schedule
/// (<c>multipleresetsswap.cpp:38-73</c>); nanoql's passes the leg's builder to the base's constructor, so the swap is
/// built once, its floating leg the <see cref="MultipleResetsLeg"/> and its floating schedule the coupon schedule.
/// </summary>
public class MultipleResetsSwapLegTests
{
    [Fact]
    public void TheFloatingLegIsTheMultipleResetsLeg()
    {
        (MultipleResetsSwap swap, EvaluationContext context) = Swap();
        ImmutableArray<CashFlow> leg = new MultipleResetsLeg(swap.FullResetSchedule, swap.IborIndex, swap.ResetsPerCoupon)
            .WithNotionals(swap.FloatingNominals)
            .WithRateSpreads(swap.Spread)
            .WithAveragingMethod(swap.AveragingMethod)
            .WithPaymentAdjustment(swap.PaymentConvention)
            .WithPaymentLag(swap.PaymentLag)
            .WithPaymentCalendar(swap.PaymentCalendar ?? swap.FullResetSchedule.Calendar)
            .ToLeg();

        Assert.All(swap.FloatingLeg, f => Assert.IsType<MultipleResetsCoupon>(f));
        Assert.Equal(leg.Length, swap.FloatingLeg.Length);
        for (int i = 0; i < leg.Length; i++)
        {
            var expected = (MultipleResetsCoupon)leg[i];
            var actual = (MultipleResetsCoupon)swap.FloatingLeg[i];
            Assert.Equal(expected.Date, actual.Date);
            Assert.Equal(expected.ValueDates, actual.ValueDates);
            Assert.Equal(expected.Amount(context), actual.Amount(context));
        }
    }

    [Fact]
    public void TheFloatingScheduleIsTheCouponSchedule()
    {
        (MultipleResetsSwap swap, _) = Swap();
        ImmutableArray<Date> resets = swap.FullResetSchedule.Dates;
        Date[] couponDates = [.. resets.Where((_, i) => i % swap.ResetsPerCoupon == 0)];

        Assert.Equal(couponDates, swap.FloatingSchedule.Dates);
        Assert.Equal(swap.FloatingLeg.Length, swap.FloatingNominals.Length);
        Assert.Equal(swap.FloatingLeg.Length * swap.ResetsPerCoupon, resets.Length - 1);
    }

    private static (MultipleResetsSwap Swap, EvaluationContext Context) Swap()
    {
        VectorCase c = VectorFile.Load("instruments/multipleresetsswap").Cases
            .Single(x => x.Name.StartsWith("swap 5Y, monthly resets compounded quarterly, payer /", StringComparison.Ordinal));
        EvaluationContext context = c.Context();
        return (MultipleResetsSwapVectorTests.SwapOf(c.Input("swap"), context), context);
    }
}
