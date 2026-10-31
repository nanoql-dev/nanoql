// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Extensions.Coupons;
using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="MBSFixedRateBond"/> reads its prepayment model at its start's anniversaries: period i's month at the
/// start plus the pool's age plus i months, so a pool on the PSA curve takes SF-6's MONTH in every period
/// (<c>PsaMonthCountTests</c>). QLNet reads a schedule date plus the age (<c>MBSFixedRateBond.cs:61</c>, <c>:84</c>), the
/// same date from a start before the 29th, and on its Bond Basis month: from a start on 31 January 2007 its pool takes
/// MONTH 1's CPR in its second period and MONTH 13's in its 14th (0.2% and 2.6% at 100% PSA), skipping MONTHs 2 and 14.
/// The check is SIFMA's formula, each period's CPR SF-6's at its MONTH; QLNet's bits hold from a start before the 29th
/// (the vectors).
/// </summary>
public class MbsPoolMonthsTests
{
    [Theory]
    [InlineData(2007, 1, 31, 360, 360)]
    [InlineData(2007, 1, 30, 360, 360)]
    [InlineData(2008, 1, 29, 360, 360)]
    [InlineData(2007, 8, 31, 360, 360)]
    // Seasoned two and seven months: a backward schedule from a maturity on the 30th falls on 30ths.
    [InlineData(2007, 1, 31, 358, 360)]
    [InlineData(2007, 3, 31, 353, 360)]
    public void APoolFromAMonthEndTakesSf6sMonthInEveryPeriod(int year, int month, int day, int tenor, int original)
    {
        var start = new Date(day, (Month)month, year);
        var pool = new MBSFixedRateBond(0, new NullCalendar(), 400000000, start, new Period(tenor, TimeUnit.Months),
            new Period(original, TimeUnit.Months), Frequency.Monthly, 0.06, 0.055,
            new Thirty360(Thirty360Convention.BondBasis), new PSACurve(start), BusinessDayConvention.Unadjusted);
        EvaluationContext context = MbsPools.At(start);
        var coupons = pool.ExpectedCashflows.OfType<FixedRateCoupon>().ToList();
        var scheduled = pool.ExpectedCashflows.OfType<AmortizingPayment>().ToList();
        var prepay = pool.ExpectedCashflows.OfType<VoluntaryPrepay>().ToList();

        // QLNet from 2007-01-31: periods 1 to 4 at CPRs of 0.2%, 0.2%, 0.6% and 0.8%; periods 13 to 15 at 2.6%, 2.6%
        // and 3.0%.
        for (int i = 0; i < 40; i++)
        {
            double smm = prepay[i].Amount(context) / (coupons[i].ContractNominal - scheduled[i].Amount(context));
            double cpr = 1 - Math.Pow(1 - smm, 12);
            double sf6 = 0.002 * Math.Min(i + (original - tenor) + 1, 30);
            Assert.True(Math.Abs(cpr - sf6) <= 1e-12, $"period {i + 1}: CPR {cpr:R}, SF-6's {sf6:R}");
        }
    }

    [Fact]
    public void ThePublicSmmIsTheModelAtADatePlusTheAge()
    {
        // Seasoned two months from 31 January 2007: the schedule falls on 30ths and February's end, and from 30 March
        // the date plus the age, 30 May, is MONTH 4 where the flows' anniversary, 31 May, is MONTH 5. QLNet's flows
        // read the model at the schedule's dates plus the age, which on this curve gives CPRs of 0.6%, 0.6%, 0.8% and
        // 1.2% in periods 1 to 4; the flows take SF-6's 0.6%, 0.8%, 1.0% and 1.2%.
        var start = new Date(31, January, 2007);
        var model = new PSACurve(start);
        var pool = new MBSFixedRateBond(0, new NullCalendar(), 400000000, start, new Period(358, TimeUnit.Months),
            new Period(360, TimeUnit.Months), Frequency.Monthly, 0.06, 0.055,
            new Thirty360(Thirty360Convention.BondBasis), model, BusinessDayConvention.Unadjusted);
        EvaluationContext context = MbsPools.At(start);
        var coupons = pool.ExpectedCashflows.OfType<FixedRateCoupon>().ToList();
        var scheduled = pool.ExpectedCashflows.OfType<AmortizingPayment>().ToList();
        var prepay = pool.ExpectedCashflows.OfType<VoluntaryPrepay>().ToList();
        double[] qlnetCpr = [0.006, 0.006, 0.008, 0.012];
        for (int i = 0; i < 4; i++)
        {
            Date d = coupons[i].AccrualStartDate;
            Assert.Equal(model.GetSMM(d + new Period(2, TimeUnit.Months)), pool.SMM(d));
            Assert.True(Math.Abs((1 - Math.Pow(1 - pool.SMM(d), 12)) - qlnetCpr[i]) <= 1e-12, $"period {i + 1}");
            double smm = prepay[i].Amount(context) / (coupons[i].ContractNominal - scheduled[i].Amount(context));
            double cpr = 1 - Math.Pow(1 - smm, 12);
            Assert.True(Math.Abs(cpr - (0.002 * (i + 3))) <= 1e-12, $"period {i + 1}: CPR {cpr:R}");
        }
    }
}
