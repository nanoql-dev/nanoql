// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Extensions.Coupons;
using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A pool's pay-off. Under SIFMA's cap (<c>PsaCapAndFloorTests</c>), a 2,000% PSA pool prepays its whole balance at
/// the 25th period, where its CPR reaches 100%, and its flows after are 0 but for one period's residue of rounding,
/// 7.45e-9; QLNet's CPR passes 1 there and its flows from the 26th prepayment and 27th coupon are NaN, 335 of them.
/// After a pay-off <see cref="MBSFixedRateBond.MonthlyYield"/>, <see cref="MBSFixedRateBond.BondEquivalentYield"/>,
/// <c>Yield</c> and <c>Price</c> refuse the pool, its balance 0, as <c>BondFunctions</c> refuses a bond not tradable at
/// its settlement: QLNet's yields throw too, on its stale notional (376,325,238.28 on a pool at CPR 1 settled 1 June
/// 2007), where a solve on the pool's balance would return 0, QLNet's <c>PVDifference</c> being exactly 0 at both ends
/// of its bracket. QLNet's 25th prepayment stands within six decimal places: GitHub's windows-2022 runner's C runtime
/// and processor give it one ulp more, 59878137.771088034 (CI run 41543305018).
/// </summary>
public class MbsPayOffTests
{
    [Fact]
    public void A2000PercentPsaPoolsFlowsAfterThePayOffAreZero()
    {
        MBSFixedRateBond pool = MbsPools.Pool(360, 360, 0.06, 0.055, new PSACurve(MbsPools.Start, 20.0));
        EvaluationContext context = MbsPools.At(MbsPools.Start);
        var coupons = pool.ExpectedCashflows.OfType<FixedRateCoupon>().ToList();
        var prepay = pool.ExpectedCashflows.OfType<VoluntaryPrepay>().ToList();

        // QLNet: prepayment 25 59878137.77108803 (its CPR 1), then NaN: 335 prepayments.
        Assert.Equal(59878137.77108803, prepay[24].Amount(context), 6);
        Assert.All(pool.ExpectedCashflows, c => Assert.True(double.IsFinite(c.Amount(context))));
        Assert.True(coupons[25].ContractNominal < 1e-6, $"the residue {coupons[25].ContractNominal:R}");
        Assert.All(coupons.Skip(26), c => Assert.Equal(0.0, c.ContractNominal));
    }

    [Fact]
    public void ThePaidOffPoolsYieldsAreRefused()
    {
        MBSFixedRateBond pool = MbsPools.Pool(60, 60, 0.05, 0.045, new ConstantCPR(1.0));
        EvaluationContext context = MbsPools.At(new Date(1, June, 2007));

        // QLNet: notional 376325238.28080237; MonthlyYield throws "root not bracketed: f[0,1] ->
        // [-376325238.28080237,-376325238.28080237]"; PVDifference on the pool's balance 0 at 0 and at 1: 0 and 0.
        Assert.Equal(0.0, pool.Notional(context));
        Assert.Throws<ArgumentException>(() => pool.MonthlyYield(context));
        Assert.Throws<ArgumentException>(() => pool.BondEquivalentYield(context));
        Assert.Throws<ArgumentException>(() => pool.Yield(100.0, 14, context));
        Assert.Throws<ArgumentException>(() => pool.Price(0.05, 14, context));
        Assert.Equal(0.0, MBSFixedRateBondFunctions.PVDifference(0, pool.ExpectedCashflows, 0, new Date(1, June, 2007),
            context));
    }
}
