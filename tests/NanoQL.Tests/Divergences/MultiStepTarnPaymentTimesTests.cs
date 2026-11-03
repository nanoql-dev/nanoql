// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The TARN's floating payment times (fixed). QuantLib's <c>MultiStepTarn</c> takes the floating coupons' payment times
/// and appends the fixed ones in their place (<c>multisteptarn.cpp:50</c>), so it discounts the floating coupons at the
/// fixed times: on six semi-annual rates with the fixed coupons paid at each period's start and the floating at its
/// end, its cash-flow times are the starts twice, and on a model of 1e-8 volatility its value is −0.351659, 1.3e-3
/// above the deterministic closed form. nanoql pays the floating coupons at their own times. Checks: QuantLib's value
/// where the two schedules coincide (the vectors); the deterministic model's closed form.
/// </summary>
public class MultiStepTarnPaymentTimesTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void TheFloatingCouponsArePaidAtTheirOwnTimes()
    {
        double[] starts = Market.RateTimes[..^1];
        MultiStepTarn tarn = Tarn(starts);
        Assert.Equal([.. starts, .. Market.PaymentTimes], tarn.PossibleCashFlowTimes());

        // QuantLib's: the starts twice
        Assert.NotEqual([.. starts, .. starts], tarn.PossibleCashFlowTimes());
    }

    [Fact]
    public void ADeterministicModelGivesTheClosedForm()
    {
        double[] starts = Market.RateTimes[..^1];
        (ImmutableArray<double> values, _) = Market.TerminalPc(Tarn(starts), 3, 1023, vol: 1e-8);

        // the coupons on the forwards, each discounted at its own time
        double closedForm = 0.0, paid = 0.0;
        for (int i = 0; i < Market.N; ++i)
        {
            double floating = Market.Forwards[i] * Market.Accruals[i];
            double coupon = Math.Max(0.08 - (2.0 * Market.Forwards[i]), 0.0) * Market.Accruals[i];
            paid += coupon;
            if (i == Market.N - 1)
            {
                coupon += 0.5 - paid;
            }

            closedForm += (floating * Market.Discounts[i + 1]) - (coupon * Market.Discounts[i]);
        }

        Assert.Equal(closedForm, values[0], 1e-7);

        // QuantLib's, the floating coupons discounted at the starts
        const double quantLib = -0.35165937413210674;
        Assert.True(Math.Abs(quantLib - closedForm) > 1e-3, $"{quantLib} against {closedForm}");
    }

    // A TARN of total coupon 50%, never reached, struck at 8% on twice the rate: the fixed coupons paid at the given
    // times, the floating at the periods' ends.
    private static MultiStepTarn Tarn(double[] fixedTimes) =>
        new(
            Market.RateTimes,
            Market.Accruals,
            Market.Accruals,
            fixedTimes,
            Market.PaymentTimes,
            0.5,
            [.. Enumerable.Repeat(0.08, Market.N)],
            [.. Enumerable.Repeat(2.0, Market.N)],
            new double[Market.N]);
}
