// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/overnightindexedfundingratehelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class OvernightIndexedFundingRateHelperTests
{
    // Adapted: the evaluation date is the context's; a helper's implied quote after the bootstrap is the helper bound to
    // the bootstrapped curve, as QuantLib's bootstrap leaves its helpers bound, and setTermStructure returns the bound
    // copy.
    private readonly record struct FundingQuote(Period Tenor, double Margin);

    [Fact]
    public void TestUsdCofStyleBootstrap()
    {
        var today = new Date(30, Month.September, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        Calendar calendar = new UnitedStates(UnitedStatesMarket.Settlement);
        YieldTermStructure projection = FlatCurve(today, 0.04);
        var sofr = new Sofr(projection);

        FundingQuote[] quotes =
        [
            new(3 * TimeUnit.Months, 0.0008750000), new(6 * TimeUnit.Months, 0.0020333333),
            new(9 * TimeUnit.Months, 0.0026166667), new(1 * TimeUnit.Years, 0.0032250000),
            new(2 * TimeUnit.Years, 0.0053769087), new(3 * TimeUnit.Years, 0.0068085774),
            new(4 * TimeUnit.Years, 0.0076010466), new(5 * TimeUnit.Years, 0.0083935157),
            new(6 * TimeUnit.Years, 0.0083935157), new(7 * TimeUnit.Years, 0.0083935157),
            new(8 * TimeUnit.Years, 0.0083935157), new(9 * TimeUnit.Years, 0.0083935157),
            new(10 * TimeUnit.Years, 0.0083935157), new(12 * TimeUnit.Years, 0.0083935157),
            new(15 * TimeUnit.Years, 0.0083935157), new(20 * TimeUnit.Years, 0.0083935157),
            new(25 * TimeUnit.Years, 0.0083935157), new(30 * TimeUnit.Years, 0.0083935157),
            new(40 * TimeUnit.Years, 0.0083935157),
        ];

        var fundingHelpers = new List<OvernightIndexedFundingRateHelper>();
        foreach (FundingQuote q in quotes)
        {
            fundingHelpers.Add(new OvernightIndexedFundingRateHelper(
                q.Margin, q.Tenor, 2, calendar, BusinessDayConvention.ModifiedFollowing, false, sofr,
                3 * TimeUnit.Months, new Actual360(), context, 2, true));
        }

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, fundingHelpers, new Actual365Fixed(), context);
        curve.Discount(fundingHelpers[^1].PillarDate!.Value);

        Date start = calendar.Advance(today, 2, TimeUnit.Days, BusinessDayConvention.Following);
        for (int i = 0; i < quotes.Length; ++i)
        {
            Date maturity = calendar.Advance(start, quotes[i].Tenor, BusinessDayConvention.ModifiedFollowing, false);
            Date expectedPillar = calendar.Advance(maturity, 2, TimeUnit.Days, BusinessDayConvention.Following);
            Assert.Equal(expectedPillar, fundingHelpers[i].PillarDate);
            double calculated = fundingHelpers[i].SetTermStructure(curve).ImpliedQuote(context);
            if (Math.Abs(calculated - quotes[i].Margin) > 1.0e-11)
            {
                Assert.Fail(
                    $"failed to reproduce the {quotes[i].Tenor} funding margin\n"
                    + $"    expected:   {quotes[i].Margin}\n"
                    + $"    calculated: {calculated}");
            }
        }
    }

    [Fact]
    public void TestLaggedCashFlowsAndDatedConstructor()
    {
        var today = new Date(30, Month.September, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        Calendar calendar = new UnitedStates(UnitedStatesMarket.Settlement);
        YieldTermStructure projection = FlatCurve(today, 0.041);
        var sofr = new Sofr(projection);

        var start = new Date(2, Month.October, 2025);
        Date end = start + (2 * TimeUnit.Years);
        var helper = new OvernightIndexedFundingRateHelper(
            0.003, start, end, calendar, BusinessDayConvention.ModifiedFollowing, false, sofr, 3 * TimeUnit.Months,
            new Actual360(), context, 2, true);

        YieldTermStructure discountCurve = FlatCurve(today, 0.047);
        helper = helper.SetTermStructure(discountCurve);

        Swap swap = helper.Swap;
        ImmutableArray<CashFlow> overnightLeg = swap.Leg(0);
        ImmutableArray<CashFlow> exchanges = swap.Leg(1);

        Date expectedInitial = calendar.Advance(start, 2, TimeUnit.Days, BusinessDayConvention.Following);
        Date expectedFinal = calendar.Advance(
            calendar.Adjust(end, BusinessDayConvention.ModifiedFollowing), 2, TimeUnit.Days, BusinessDayConvention.Following);

        Assert.Equal(2, exchanges.Length);
        Assert.Equal(expectedInitial, exchanges[0].Date);
        Assert.Equal(expectedFinal, exchanges[^1].Date);
        Assert.Equal(expectedFinal, overnightLeg[^1].Date);
        Assert.Equal(calendar.Adjust(end, BusinessDayConvention.ModifiedFollowing), helper.MaturityDate);
        Assert.Equal(expectedFinal, helper.PillarDate);

        double overnightNpv = 0.0;
        double annuity = 0.0;
        foreach (CashFlow cf in overnightLeg)
        {
            var coupon = Assert.IsAssignableFrom<Coupon>(cf);
            double discount = discountCurve.Discount(cf.Date);
            overnightNpv += cf.Amount(context) * discount;
            annuity += coupon.Nominal(context) * coupon.AccrualPeriod() * discount;
        }

        double expectedMargin = ((100.0 * discountCurve.Discount(expectedInitial))
            - (100.0 * discountCurve.Discount(expectedFinal)) - overnightNpv) / annuity;
        Assert.True(Math.Abs(helper.ImpliedQuote(context) - expectedMargin) <= 1.0e-13);
    }

    [Fact]
    public void TestSameDayOvernightPillar()
    {
        var today = new Date(15, Month.July, 2026);
        var context = new EvaluationContext { EvaluationDate = today, IncludeReferenceDateEvents = false };
        Calendar calendar = new Australia();
        YieldTermStructure projection = FlatCurve(today, 0.04);
        var aonia = new Aonia(projection);

        double margin = 0.0004;
        var helper = new OvernightIndexedFundingRateHelper(
            margin, 1 * TimeUnit.Days, 0, calendar, BusinessDayConvention.Following, false, aonia, 12 * TimeUnit.Months,
            new Actual365Fixed(), context, 0, true);

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, [helper], new Actual365Fixed(), context);

        Assert.Equal(new Date(16, Month.July, 2026), helper.PillarDate);
        curve.Discount(helper.PillarDate!.Value);
        Assert.True(Math.Abs(helper.SetTermStructure(curve).ImpliedQuote(context) - margin) <= 1.0e-11);
    }

    private static YieldTermStructure FlatCurve(Date referenceDate, double rate) =>
        new FlatForward(referenceDate, rate, new Actual365Fixed(), Compounding.Continuous);
}
