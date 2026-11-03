// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="ZeroCouponInflationSwap.FairRate"/> where the legs pay on different days reads the legs' end discounts
/// from the engine's results; with no results it is a <see cref="NanoQLException"/> with <c>fixedLegBPS</c>'s text,
/// its subject the fair rate: "cannot calculate fairRate because end discount is not populated". On one payment day it
/// reads no result, QuantLib's expression.
/// </summary>
public class ZeroCouponInflationSwapRefusalTests
{
    [Fact]
    public void AFairRateWithoutEndDiscountsIsRefused()
    {
        var today = new Date(15, Month.June, 2024);
        var context = new EvaluationContext
        {
            EvaluationDate = today,
            Fixings = IndexFixings.Empty.AddFixing(new UKRPI(), new Date(1, Month.March, 2024), 310.0),
        };
        var curve = new ZeroInflationCurve(
            today,
            [new Date(1, Month.March, 2024), new Date(1, Month.March, 2034)],
            [0.03, 0.03],
            Frequency.Monthly,
            new Actual365Fixed());

        ZeroCouponInflationSwap parted = Swap(curve, BusinessDayConvention.Following, BusinessDayConvention.Preceding);
        Assert.Equal(
            "cannot calculate fairRate because end discount is not populated",
            Assert.Throws<NanoQLException>(() => parted.FairRate(context)).Message);

        ZeroCouponInflationSwap together = Swap(curve, BusinessDayConvention.Following, null);
        Assert.Equal(together.FixedLeg[0].Date, together.InflationLeg[0].Date);
        Assert.True(together.FairRate(context) > 0.0);
    }

    private static ZeroCouponInflationSwap Swap(
        ZeroInflationCurve curve, BusinessDayConvention fixedConvention, BusinessDayConvention? inflationConvention) =>
        new(
            SwapType.Payer,
            1e6,
            new Date(15, Month.June, 2024),
            new Date(31, Month.May, 2025),
            new UnitedKingdom(),
            fixedConvention,
            new Actual365Fixed(),
            0.03,
            new UKRPI(curve),
            new Period(3, TimeUnit.Months),
            CPIInterpolationType.Flat,
            false,
            null,
            inflationConvention);
}
