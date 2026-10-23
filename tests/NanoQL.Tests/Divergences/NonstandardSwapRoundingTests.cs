// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A nonstandard swap converted from an overnight-indexed swap with a rounding precision: QuantLib's conversion copies
/// the source's observation conventions (<c>nonstandardswap.cpp:60-66</c>) but not its rounding precision, and builds
/// the overnight leg without one (<c>:188-200</c>), so a rounded source's coupons differ from the converted swap's. On
/// the wheel, from 30 June 2025 on a flat 3.37% Actual/360 curve, a payer on 1,000,000 SOFR from one year out for two
/// years, semiannual, against 3% fixed, is worth 7519.320757824375 with a precision of 4, where the swap QuantLib's
/// conversion builds (its legs as <c>:188-200</c> set them up, built on the wheel through the vector constructor) is
/// worth 7500.351898887035, the unrounded source's. nanoql keeps the precision, so the converted swap is worth the
/// source's, the identity QuantLib's <c>testNonstandardSwapConversionPreservesObservationConventions</c> asserts of the
/// conventions it copies.
/// </summary>
public class NonstandardSwapRoundingTests
{
    [Fact]
    public void TheConversionKeepsTheRoundingPrecision()
    {
        var today = new Date(30, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.0337, new Actual360());
        var sofr = new Sofr(curve);
        Calendar calendar = sofr.FixingCalendar;
        Date start = calendar.Advance(today, 1, TimeUnit.Years);
        var schedule = new Schedule(
            start,
            calendar.Advance(start, 2, TimeUnit.Years),
            new Period(6, TimeUnit.Months),
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            false,
            context);
        var source = new OvernightIndexedSwap(
            SwapType.Payer, 1e6, schedule, 0.03, new Actual360(), sofr, 0.0, 2, BusinessDayConvention.Following, calendar, roundingPrecision: 4);

        double npv = DiscountingSwapEngine.Calculate(new NonstandardSwap(source, context), context, curve).NPV!.Value;
        Assert.True(Math.Abs((npv / DiscountingSwapEngine.Calculate(source, context, curve).NPV!.Value) - 1.0) <= 1e-14, $"{npv}");
        Assert.True(Math.Abs((npv / 7519.320757824375) - 1.0) <= 1e-12, $"{npv}");

        // QuantLib's conversion, unrounded
        Assert.True(Math.Abs((npv / 7500.351898887035) - 1.0) > 1e-3, $"{npv}");
    }
}
