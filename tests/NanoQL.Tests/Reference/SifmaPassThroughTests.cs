// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Extensions.Coupons;
using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Reference;

/// <summary>
/// <see cref="MBSFixedRateBond"/> against SIFMA's standard formulas (The Bond Market Association, Uniform Practices,
/// Chapter SF, 02/01/99): the pass-through example's first month and cash flows (SF-4, SF-5, SF-51); a same-month
/// pool's PSA months (SF-6); the standard yield's examples (SF-48, SF-51, SF-52); and its times' two rules, the due
/// dates and SIFMA's 30/360 (SF-44).
/// </summary>
[Trait("Oracle", "Reference")]
public class SifmaPassThroughTests
{
    // The examples' security: issued 1 March, a 9.0% pass-through of 9.5% loans over 360 months, at 150% PSA.
    private static readonly Date Issue = new(1, March, 1999);

    [Fact]
    public void ThePassThroughExamplesFirstMonthIsSf5s()
    {
        MBSFixedRateBond pool = Example();
        EvaluationContext context = Context(Issue);

        // SF-5, per $1 of par: scheduled amortisation 0.00049188, prepayments 0.00025022, net interest 0.00750000,
        // cash flow 0.00824210.
        Assert.Equal(0.00049188, Math.Round(pool.ExpectedCashflows[1].Amount(context) / 100, 8));
        Assert.Equal(0.00025022, Math.Round(pool.ExpectedCashflows[2].Amount(context) / 100, 8));
        Assert.Equal(0.00750000, Math.Round(pool.ExpectedCashflows[0].Amount(context) / 100, 8));
        Assert.Equal(0.00824210, Math.Round(Flow(pool, 0, context) / 100, 8));
    }

    [Theory]
    // SF-51: CF1, CF2, CF3 and CF360 per 100 of par, to four decimals.
    [InlineData(1, 0.8242)]
    [InlineData(2, 0.8491)]
    [InlineData(3, 0.8738)]
    [InlineData(360, 0.0562)]
    public void ThePassThroughExamplesCashFlowsAreSf51s(int month, double perHundred)
    {
        Assert.Equal(perHundred, Math.Round(Flow(Example(), month - 1, Context(Issue)), 4));
    }

    [Fact]
    public void ASameMonthPoolsFirstTwoMonthsAreMonthOne()
    {
        // SF-6: an original WAM of 361 on a 360-month loan term, "MONTH would equal 1 for the first two months of the
        // pool": at 100% PSA, CPRs of 0.2%, 0.2%, 0.4% and 0.6%.
        var pool = new MBSFixedRateBond(0, new NullCalendar(), 1000000, Issue, new Period(361, TimeUnit.Months),
            new Period(360, TimeUnit.Months), Frequency.Monthly, 0.06, 0.055,
            new Thirty360(Thirty360Convention.BondBasis), new PSACurve(Issue), BusinessDayConvention.Unadjusted);
        EvaluationContext context = Context(Issue);
        double[] cprs = [0.002, 0.002, 0.004, 0.006];
        for (int i = 0; i < cprs.Length; i++)
        {
            double nominal = ((Coupon)pool.ExpectedCashflows[3 * i]).ContractNominal;
            double smm = pool.ExpectedCashflows[(3 * i) + 2].Amount(context)
                / (nominal - pool.ExpectedCashflows[(3 * i) + 1].Amount(context));
            Assert.True(Math.Abs(1 - Math.Pow(1 - smm, 12) - cprs[i]) <= 1e-12, $"month {i + 1}");
        }
    }

    [Fact]
    public void TheYieldExamplesAreSf51sAndSf52s()
    {
        MBSFixedRateBond pool = Example();
        EvaluationContext context = Context(Issue);

        // SF-51: at par on the issue date, a 14-day actual delay: 9.10675%, a mortgage yield of 8.93863%.
        double y51 = pool.Yield(100, 14, context);
        Assert.Equal(9.10675, Math.Round(y51 * 100, 5));
        Assert.Equal(8.93863, Math.Round(1200 * (Math.Pow(1 + (y51 / 2), 1.0 / 6) - 1), 5));
        Assert.Equal(100, pool.Price(y51, 14, context), 10);

        // SF-52: settled seven days later at par, its dollar price 100.1750 with seven days' accrual: 9.10644%.
        double y52 = pool.Yield(100, 14, context, Issue + 7);
        Assert.Equal(9.10644, Math.Round(y52 * 100, 5));
    }

    [Fact]
    public void TheTimesRunToTheDueDatesWhateverThePaymentConvention()
    {
        // SF-44: "the date the payment is assumed to be received from homeowners, assuming 30-day months", plus the
        // actual delay; SF-48's yield discounts over it, so a pool paying on adjusted dates yields its due dates'.
        var start = new Date(1, February, 2007);
        var adjusted = new MBSFixedRateBond(0, new TARGET(), 100, start, new Period(358, TimeUnit.Months),
            new Period(360, TimeUnit.Months), Frequency.Monthly, 0.06, 0.055,
            new Thirty360(Thirty360Convention.BondBasis), new PSACurve(start), BusinessDayConvention.Following);
        var unadjusted = new MBSFixedRateBond(0, new NullCalendar(), 100, start, new Period(358, TimeUnit.Months),
            new Period(360, TimeUnit.Months), Frequency.Monthly, 0.06, 0.055,
            new Thirty360(Thirty360Convention.BondBasis), new PSACurve(start), BusinessDayConvention.Unadjusted);
        EvaluationContext context = Context(start);

        Assert.NotEqual(adjusted.ExpectedCashflows[3].Date, unadjusted.ExpectedCashflows[3].Date);
        Assert.Equal(unadjusted.Yield(100, 24, context), adjusted.Yield(100, 24, context));
    }

    [Fact]
    public void AFebruaryEndSettlementCountsAsTheThirtieth()
    {
        // SF-44: "If M1 is 2 and D1 is 28 in a nonleap year ... change D1 to 30": from 28 February 2007 to 1 March is
        // one day, where 30/360 Bond Basis counts three. The yield, solved here on SF-44's times by bisection.
        MBSFixedRateBond pool = Example(new Date(1, February, 2007));
        var settlement = new Date(28, February, 2007);
        EvaluationContext context = Context(settlement);
        double price = 100.5;
        double balance = ((Coupon)pool.ExpectedCashflows[0]).ContractNominal;
        double accrued = balance * 0.09 * 27 / 360;
        double dirty = (price / 100 * balance) + accrued;
        double Value(double y, Func<Date, int> days)
        {
            double v = 0;
            for (int k = 0; k < pool.ExpectedCashflows.Length / 3; k++)
            {
                double t = (days(((Coupon)pool.ExpectedCashflows[3 * k]).AccrualEndDate) + 14) / 360.0;
                v += Flow(pool, k, context) / Math.Pow(1 + (y / 2), 2 * t);
            }

            return v;
        }

        double Solve(Func<Date, int> days)
        {
            double lo = -0.5, hi = 0.5;
            for (int i = 0; i < 200; i++)
            {
                double mid = 0.5 * (lo + hi);
                (lo, hi) = Value(mid, days) > dirty ? (mid, hi) : (lo, mid);
            }

            return 0.5 * (lo + hi);
        }

        int Sf44(Date due) => (360 * (due.Year - 2007)) + (30 * ((int)due.Month - 2)) + (due.DayOfMonth - 30);
        var bondBasis = new Thirty360(Thirty360Convention.BondBasis);
        double expected = Solve(Sf44);
        double onBondBasis = Solve(due => bondBasis.DayCount(settlement, due));

        Assert.Equal(1, Sf44(new Date(1, March, 2007)));
        Assert.True(Math.Abs(pool.Yield(price, 14, context) - expected) <= 1e-10);
        Assert.True(Math.Abs(onBondBasis - expected) > 1e-6, "Bond Basis's times give another yield");
    }

    private static MBSFixedRateBond Example(Date? start = null)
    {
        Date s = start ?? Issue;
        return new MBSFixedRateBond(0, new NullCalendar(), 100, s, new Period(360, TimeUnit.Months),
            new Period(360, TimeUnit.Months), Frequency.Monthly, 0.095, 0.09,
            new Thirty360(Thirty360Convention.BondBasis), new PSACurve(s, 1.5), BusinessDayConvention.Unadjusted);
    }

    private static double Flow(MBSFixedRateBond pool, int period, EvaluationContext context) =>
        pool.ExpectedCashflows[3 * period].Amount(context) + pool.ExpectedCashflows[(3 * period) + 1].Amount(context)
        + pool.ExpectedCashflows[(3 * period) + 2].Amount(context);

    private static EvaluationContext Context(Date d) => new() { EvaluationDate = d };
}
