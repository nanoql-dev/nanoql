// SPDX-FileCopyrightText: Copyright (C) 2014 Cheng Li
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/amortizingbond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: testBrazilianAmortizingFixedRateBond, which waits for batch 2's Brazil calendar and Business252 day counter

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class AmortizingBondTests
{
    // Adapted: the evaluation date the case reads (line 53) is the test runner's, the context's; the cash flows'
    // amounts take it.
    [Fact]
    public void TestAmortizingFixedRateBond()
    {
        // Testing amortizing fixed rate bond...

        // Following data is generated from Excel using function pmt with Nper = 360, PV = 100.0
        Real[] rates = [0.0, 0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.08, 0.09, 0.10, 0.11, 0.12];
        Real[] amounts =
        [
            0.277777778, 0.321639520, 0.369619473, 0.421604034,
            0.477415295, 0.536821623, 0.599550525,
            0.665302495, 0.733764574, 0.804622617,
            0.877571570, 0.952323396, 1.028612597,
        ];

        Frequency freq = Frequency.Monthly;

        var context = new EvaluationContext { EvaluationDate = new Date(16, September, 2015) };
        Date refDate = context.EvaluationDate;

        const Real tolerance = 1.0e-6;

        for (Size i = 0; i < rates.Length; ++i)
        {
            Schedule schedule = AmortizingFixedRateBondFunctions.SinkingSchedule(
                refDate, new Period(30, TimeUnit.Years), freq, new NullCalendar());
            ImmutableArray<double> notionals = AmortizingFixedRateBondFunctions.SinkingNotionals(
                new Period(30, TimeUnit.Years), freq, rates[i], 100.0);

            var myBond = new AmortizingFixedRateBond(
                0, notionals, schedule, [rates[i]], new ActualActual(ActualActualConvention.ISMA));

            ImmutableArray<CashFlow> cashflows = myBond.Cashflows;

            for (Size k = 0; k < cashflows.Length / 2; ++k)
            {
                Real coupon = cashflows[2 * k].Amount(context);
                Real principal = cashflows[(2 * k) + 1].Amount(context);
                Real totalAmount = coupon + principal;

                // Check the amount is same as pmt returned
                Real error = Math.Abs(totalAmount - amounts[i]);
                Assert.False(
                    error > tolerance,
                    $"Rate: {rates[i]} {k}th cash flow Failed! Expected Amount: {amounts[i]} Calculated Amount: "
                    + $"{totalAmount}");

                // Check the coupon result
                Real expectedCoupon = notionals[k] * rates[i] / (Integer)freq;
                error = Math.Abs(coupon - expectedCoupon);

                Assert.False(
                    error > tolerance,
                    $"Rate: {rates[i]} {k}th cash flow Failed! Expected Coupon: {expectedCoupon} Calculated Coupon: "
                    + $"{coupon}");
            }
        }
    }

    [Fact]
    public void TestAmortizingFixedRateBondWithDrawDown()
    {
        // Testing amortizing fixed rate bond with draw-down...

        var issueDate = new Date(19, May, 2012);
        var maturityDate = new Date(25, May, 2017);
        Calendar calendar = new UnitedStates(UnitedStatesMarket.GovernmentBond);
        Natural settlementDays = 3;

        var context = new EvaluationContext { EvaluationDate = new Date(16, September, 2015) };
        var schedule = new Schedule(
            issueDate, maturityDate, new Period(Frequency.Semiannual), calendar, BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, context);

        Real[] nominals = [100.0, 100.0, 100.5, 100.5, 101.5, 101.5, 90.0, 80.0, 70.0, 60.0];
        Rate[] rates = [0.042];

        ImmutableArray<CashFlow> leg = new FixedRateLeg(schedule)
            .WithNotionals(nominals)
            .WithCouponRates(rates, new Actual360())
            .WithPaymentAdjustment(BusinessDayConvention.Unadjusted)
            .WithPaymentCalendar(calendar)
            .ToLeg();

        var bond = new Bond(settlementDays, calendar, issueDate, leg);

        ImmutableArray<CashFlow> cfs = bond.Cashflows;

        // first draw-down
        Real calculated = cfs[2].Amount(context);
        Real expected = nominals[1] - nominals[2];
        Real error = Math.Abs(calculated - expected);
        Real tolerance = 1e-8;

        Assert.False(
            error > tolerance,
            $"Failed to calculate first draw down: expected {expected}, calculated {calculated}");

        // second draw-down
        calculated = cfs[5].Amount(context);
        expected = nominals[3] - nominals[4];
        error = Math.Abs(calculated - expected);

        Assert.False(
            error > tolerance,
            $"Failed to calculate second draw down: expected {expected}, calculated {calculated}");

        // first amortization
        calculated = cfs[8].Amount(context);
        expected = nominals[5] - nominals[6];
        error = Math.Abs(calculated - expected);

        Assert.False(
            error > tolerance,
            $"Failed to calculate fist amortization: expected {expected}, calculated {calculated}");
    }
}
