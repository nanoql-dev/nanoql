// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="MBSFixedRateBond"/>'s notional schedule and redemptions are the pool's expected ones, so its yields price
/// the pool's balance at the settlement. QLNet's MBS keeps its level-payment base bond's notionals
/// (<c>MBSFixedRateBond.cs:39</c>, <c>:96</c>), the balance with no prepayment: on the Fabozzi pool settled 1 February
/// 2012, QLNet's notional is 371,882,785.38 against the pool's 291,474,567.29, and its monthly yield 0.0017466, where a
/// pool priced at its own balance yields its pass-through rate, 0.055/12 a month. The check is QLNet's own
/// <c>PVDifference</c> and <c>Brent</c> on the pool's balance (the vectors' seasoned cases); here, that yield. The
/// yields also count the settlement date's flows as paid whatever the context's settings, as the balance does, where
/// QLNet's include them under either setting that does: on that payment date its monthly yield moves to 0.0018220.
/// </summary>
public class MbsPoolBalanceTests
{
    [Fact]
    public void TheNotionalIsThePoolsBalance()
    {
        MBSFixedRateBond pool = MbsPools.Fabozzi();
        var settlement = new Date(1, February, 2012);
        EvaluationContext context = MbsPools.At(settlement);
        double balance = pool.ExpectedCashflows.OfType<FixedRateCoupon>().First(c => c.AccrualStartDate == settlement)
            .ContractNominal;

        // QLNet: notional 371882785.3760657; the pool's balance 291474567.28517103.
        Assert.Equal(291474567.28517103, balance);
        Assert.Equal(balance, pool.Notional(context, settlement));
        Assert.Equal(balance, pool.Notional(context, settlement + 10));
    }

    [Fact]
    public void ASeasonedPoolAtItsBalanceYieldsItsPassThroughRate()
    {
        MBSFixedRateBond pool = MbsPools.Fabozzi();
        EvaluationContext context = MbsPools.At(new Date(1, February, 2012));

        // QLNet: MonthlyYield 0.001746570538116544, BondEquivalentYield 0.021050575113241266.
        double y = pool.MonthlyYield(context);
        Assert.True(Math.Abs(y - (0.055 / 12)) <= 1e-12, $"monthly yield {y:R}");
        Assert.True(Math.Abs(pool.BondEquivalentYield(context) - (2 * (Math.Pow(1 + (0.055 / 12), 6) - 1))) <= 1e-11);
    }

    // The settlement date's flows count as paid under every setting, as the balance takes them. QLNet's yields read
    // the settings: on a payment date with either setting including the date's flows, its MonthlyYield is
    // 0.0018220147653889031 and its BondEquivalentYield 0.021964011594138633, against 0.001746570538116544 and
    // 0.021050575113241266 under QuantLib's defaults.
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void TheYieldsPriceTheSettlementDatesFlowsAsPaid(bool includeReferenceDateEvents, bool? includeTodaysCashFlows)
    {
        MBSFixedRateBond pool = MbsPools.Fabozzi();
        EvaluationContext defaults = MbsPools.At(new Date(1, February, 2012));
        EvaluationContext context = defaults with
        {
            IncludeReferenceDateEvents = includeReferenceDateEvents,
            IncludeTodaysCashFlows = includeTodaysCashFlows,
        };

        double y = pool.MonthlyYield(context);
        Assert.True(Math.Abs(y - (0.055 / 12)) <= 1e-12, $"monthly yield {y:R}");
        Assert.Equal(pool.MonthlyYield(defaults), y);
        Assert.Equal(pool.BondEquivalentYield(defaults), pool.BondEquivalentYield(context));
    }
}
