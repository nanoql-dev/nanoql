// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Extensions.Coupons;
using NanoQL.Extensions.Instruments;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A pool whose original length is shorter than its remaining term, so that its age is negative, prepays at MONTH 1
/// until its loans' first month, as SIFMA's PSA model prices it (Chapter SF, SF-6, "MONTH equals 1 for all prior
/// months"; its same-month pools are <c>Reference/SifmaPassThroughTests</c>'). QLNet's PSA falls to 0 and below there
/// (<c>PSACurve.cs:38</c>), so an original length of 300 on a 358-month term gives 57 negative prepayments, the first
/// −3,611,165.28. The check is SIFMA's formula, and QLNet's bits wherever the age is 0 or more (the vectors).
/// </summary>
public class MbsYoungPoolTests
{
    [Fact]
    public void NoPrepaymentIsNegative()
    {
        MBSFixedRateBond pool = MbsPools.Pool(358, 300, 0.06, 0.055, new PSACurve(MbsPools.Start));
        EvaluationContext context = MbsPools.At(MbsPools.Start);
        var prepay = pool.ExpectedCashflows.OfType<VoluntaryPrepay>().Select(c => c.Amount(context)).ToList();

        // QLNet: 57 negative prepayments, the first -3611165.275755381, the last in period 57.
        Assert.All(prepay, p => Assert.True(p >= 0));
    }

    [Fact]
    public void TheMonthsBeforeTheLoansFirstPrepayAtMonthOne()
    {
        MBSFixedRateBond pool = MbsPools.Pool(358, 300, 0.06, 0.055, new PSACurve(MbsPools.Start));
        EvaluationContext context = MbsPools.At(MbsPools.Start);
        var coupons = pool.ExpectedCashflows.OfType<FixedRateCoupon>().ToList();
        var scheduled = pool.ExpectedCashflows.OfType<AmortizingPayment>().ToList();
        var prepay = pool.ExpectedCashflows.OfType<VoluntaryPrepay>().ToList();
        double monthOne = 1 - Math.Pow(1 - 0.002, 1 / 12d);
        for (int i = 0; i < 58; i++)
        {
            double smm = prepay[i].Amount(context) / (coupons[i].ContractNominal - scheduled[i].Amount(context));
            Assert.True(Math.Abs(smm - monthOne) <= 1e-12 * monthOne, $"period {i + 1}: SMM {smm:R}");
        }
    }
}
