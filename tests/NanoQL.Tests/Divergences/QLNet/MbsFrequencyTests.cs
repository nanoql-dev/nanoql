// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Extensions.Coupons;
using NanoQL.Extensions.Instruments;
using NanoQL.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A pool of a frequency other than monthly prepays at its model's rate: a period survives the product of its months'
/// survival, each month's SMM at its own start, and a period counted in weeks or days its start's monthly survival to
/// the power of the months it spans, each calendar month's share of it. QLNet applies one month's SMM a period
/// (<c>MBSFixedRateBond.cs:61-62</c>), so a CPR of 8% prepays at 2.74% a year quarterly and 1.38% semi-annually, and
/// its yields refuse such a pool ("root not bracketed", its bracket and formula being monthly). The checks: QLNet's
/// own MBS fed the period's SMM (the vectors' quarterly and semi-annual cases); the constant-CPR closed form, a span's
/// balance (1 − CPR)^(T/12) of the level-payment schedule's, T its months by an independent count; and the yields'
/// closed form at par, the pass-through rate per period compounded to a month and to a half year.
/// </summary>
public class MbsFrequencyTests
{
    [Theory]
    [InlineData(Frequency.Quarterly, 4)]
    [InlineData(Frequency.Semiannual, 2)]
    public void AConstantCprPrepaysAtItsRate(Frequency frequency, int perYear)
    {
        MBSFixedRateBond pool = MbsPools.Pool(360, 360, 0.06, 0.055, new ConstantCPR(0.08), frequency);
        EvaluationContext context = MbsPools.At(MbsPools.Start);
        double nominal = pool.ExpectedCashflows.OfType<FixedRateCoupon>().First().ContractNominal;
        double scheduled = pool.ExpectedCashflows.OfType<AmortizingPayment>().First().Amount(context);
        double prepay = pool.ExpectedCashflows.OfType<VoluntaryPrepay>().First().Amount(context);

        // QLNet: an annual CPR of 0.027411173781143994 quarterly, 0.013800818181815777 semi-annually.
        double cpr = 1 - Math.Pow(1 - (prepay / (nominal - scheduled)), perYear);
        Assert.True(Math.Abs(cpr - 0.08) <= 1e-12, $"CPR {cpr:R}");
    }

    [Theory]
    [InlineData(Frequency.Weekly, 520)]
    [InlineData(Frequency.Biweekly, 260)]
    [InlineData(Frequency.Daily, 3650)]
    public void AShortPeriodsPoolFollowsTheClosedForm(Frequency frequency, int length)
    {
        TimeUnit unit = frequency == Frequency.Daily ? TimeUnit.Days : TimeUnit.Weeks;
        var tenor = new Period(length, unit);
        var pool = new MBSFixedRateBond(
            0, new NullCalendar(), 400000000, MbsPools.Start, tenor, tenor, frequency, 0.06, 0.055,
            new Thirty360(Thirty360Convention.BondBasis), new ConstantCPR(0.08), BusinessDayConvention.Unadjusted);
        ImmutableArray<double> level =
            AmortizingFixedRateBondFunctions.SinkingNotionals(tenor, frequency, 0.06, 400000000);
        var coupons = pool.ExpectedCashflows.OfType<FixedRateCoupon>().ToList();
        for (int k = 1; k < coupons.Count; k++)
        {
            double expected = level[k] * Math.Pow(1 - 0.08, Months(MbsPools.Start, coupons[k].AccrualStartDate) / 12);
            Assert.True(Math.Abs(coupons[k].ContractNominal - expected) <= 1e-10 * level[k], $"period {k + 1}");
        }
    }

    [Theory]
    [InlineData(Frequency.Quarterly)]
    [InlineData(Frequency.Semiannual)]
    public void TheYieldsAtParAreThePassThroughRatesEquivalents(Frequency frequency)
    {
        MBSFixedRateBond pool = MbsPools.Pool(360, 360, 0.06, 0.055, new ConstantCPR(0.08), frequency);
        EvaluationContext context = MbsPools.At(MbsPools.Start);
        int f = (int)frequency;

        // QLNet: MonthlyYield throws "root not bracketed: f[0,1]".
        Assert.True(Math.Abs(pool.MonthlyYield(context) - (Math.Pow(1 + (0.055 / f), f / 12.0) - 1)) <= 1e-12);
        Assert.True(
            Math.Abs(pool.BondEquivalentYield(context) - (2 * (Math.Pow(1 + (0.055 / f), f / 2.0) - 1))) <= 1e-12);
    }

    // The months from one date to another, each calendar month's days over its length, counted day by day.
    private static double Months(Date from, Date to)
    {
        double months = 0;
        for (Date d = from; d < to; d++)
        {
            months += 1.0 / Date.EndOfMonth(d).DayOfMonth;
        }

        return months;
    }
}
