// SPDX-FileCopyrightText: Copyright (C) 2014 Cheng Li
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/amortizingbond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

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
    public void TestBrazilianAmortizingFixedRateBond()
    {
        // Testing Brazilian amortizing fixed rate bond...

        // Following data is based on the following Brazilian onshore corporate bond code:
        // SND Code - RISF11
        // ISIN Code - BRRISFDBS005
        // Fiduciary Agent URL -
        // https://www.pentagonotrustee.com.br/Site/DetalhesEmissor?ativo=RISF11&aba=tab-5&tipo=undefined
        Real[] notionals =
        [
            1000, 983.33300000, 966.66648898, 950.00019204,
            933.33338867, 916.66685434, 900.00001759, 883.33291726,
            866.66619177, 849.99933423, 833.33254728, 816.66589633,
            799.99937871, 783.33299165, 766.66601558, 749.99946306,
            733.33297499, 716.66651646, 699.99971995, 683.33272661,
            666.66624140, 649.99958536, 633.33294599, 616.66615618,
            599.99951997, 583.33273330, 566.66633377, 549.99954356,
            533.33290739, 516.66625403, 499.99963400, 483.33314619,
            466.66636930, 449.99984658, 433.33320226, 416.66634063,
            399.99968700, 383.33290004, 366.66635221, 349.99953317,
            333.33290539, 316.66626012, 299.99948151, 283.33271031,
            266.66594695, 249.99932526, 233.33262024, 216.66590450,
            199.99931312, 183.33277035, 166.66617153, 149.99955437,
            133.33295388, 116.66633464, 99.99973207, 83.33307672,
            66.66646137, 49.99984602, 33.33324734, 16.66662367,
        ];

        Real[] expectedAmortizations =
        [
            16.66700000, 16.66651102, 16.66629694, 16.66680337,
            16.66653432, 16.66683675, 16.66710033, 16.66672548,
            16.66685753, 16.66678695, 16.66665095, 16.66651761,
            16.66638706, 16.66697606, 16.66655251, 16.66648807,
            16.66645852, 16.66679651, 16.66699333, 16.66648520,
            16.66665604, 16.66663937, 16.66678981, 16.66663620,
            16.66678667, 16.66639952, 16.66679021, 16.66663617,
            16.66665336, 16.66662002, 16.66648780, 16.66677688,
            16.66652271, 16.66664432, 16.66686163, 16.66665363,
            16.66678696, 16.66654783, 16.66681904, 16.66662777,
            16.66664527, 16.66677860, 16.66677119, 16.66676335,
            16.66662168, 16.66670502, 16.66671573, 16.66659137,
            16.66654276, 16.66659882, 16.66661715, 16.66660049,
            16.66661924, 16.66660257, 16.66665534, 16.66661534,
            16.66661534, 16.66659867, 16.66662367, 16.66662367,
        ];

        Real[] expectedCoupons =
        [
            5.97950399, 4.85474255, 5.27619136, 5.18522454,
            5.33753111, 5.24221882, 4.91231709, 4.59116258,
            4.73037674, 4.63940686, 4.54843737, 3.81920094,
            4.78359948, 3.86733691, 4.38439657, 4.09359456,
            4.00262671, 4.28531030, 3.82068947, 3.55165259,
            3.46502778, 3.71720657, 3.62189368, 2.88388676,
            3.58769952, 2.72800044, 3.38838360, 3.00196900,
            2.91100034, 3.08940793, 2.59877059, 2.63809514,
            2.42551945, 2.45615766, 2.59111761, 1.94857222,
            2.28751141, 1.79268582, 2.19248291, 1.81913832,
            1.90625855, 1.89350716, 1.48110584, 1.62031828,
            1.38600825, 1.23425366, 1.39521333, 1.06968563,
            1.03950542, 1.00065409, 0.90968563, 0.81871706,
            0.79726493, 0.63678002, 0.57187676, 0.49829046,

            // data changed as source (pentagonotrustee.com.br) does not include newly introduced
            // "Black Awareness Day" holiday
            0.31177086, 0.27290565, 0.19062560, 0.08662552,
        ];

        var context = new EvaluationContext { EvaluationDate = new Date(16, September, 2015) };

        Natural settlementDays = 0;
        var issueDate = new Date(2, March, 2020);
        var maturityDate = new Date(2, March, 2025);

        var schedule = new Schedule(
            issueDate,
            maturityDate,
            new Period(Frequency.Monthly),
            new Brazil(BrazilMarket.Settlement),
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            context);

        InterestRate[] couponRates =
        [
            new(0.0675, new Business252(new Brazil()), Compounding.Compounded, Frequency.Annual),
        ];

        ImmutableArray<CashFlow> coupons = new FixedRateLeg(schedule)
            .WithNotionals(notionals)
            .WithCouponRates(couponRates)
            .WithPaymentAdjustment(BusinessDayConvention.Following)
            .ToLeg();

        var risf11 = new Bond(settlementDays, schedule.Calendar, issueDate, coupons);

        const Real tolerance = 1.0e-6;
        Real error;
        ImmutableArray<CashFlow> cashflows = risf11.Cashflows;
        for (Size k = 0; k < cashflows.Length / 2; ++k)
        {
            error = Math.Abs(expectedCoupons[k] - cashflows[2 * k].Amount(context));
            Assert.False(
                error > tolerance,
                $" {k}th cash flow  Failed! Expected Coupon: {expectedCoupons[k]} Calculated Coupon: "
                + $"{cashflows[2 * k].Amount(context)}");

            error = Math.Abs(expectedAmortizations[k] - cashflows[(2 * k) + 1].Amount(context));
            Assert.False(
                error > tolerance,
                $" {k}th cash flow  Failed! Expected Amortization: {expectedAmortizations[k]} Calculated Amortization: "
                + $"{cashflows[(2 * k) + 1].Amount(context)}");
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
