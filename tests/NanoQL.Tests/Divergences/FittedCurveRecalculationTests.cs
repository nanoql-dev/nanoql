// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fitted curve's recalculation path (the S-sr plan's SSD22). A fitted curve is always built afresh, fitted from the
/// given guess. QuantLib's curve is a lazy object whose fit ends by keeping its solution as its guess
/// (<c>fittedbonddiscountcurve.cpp:294-295</c>), which its L2 penalty reads (<c>:331-332</c>), so a curve recalculated
/// after a quote moves starts from, and is penalized towards, its previous fit, though its header says the penalty
/// applies "starting from the initial guess" (<c>fittedbonddiscountcurve.hpp:182-184</c>). On the wheel, a Nelson-Siegel
/// fit with L2 penalties of 0.5 over ten bonds, from 16 June 2025, recalculated after the sixth bond's price moves by 0.5,
/// discounts 0.664584692842777 at ten years after 395 evaluations, where a fresh curve on the moved prices from the same
/// guess discounts 0.6644661886683543 after 427. nanoql's curve on the moved prices is QuantLib's fresh curve.
/// </summary>
public class FittedCurveRecalculationTests
{
    private static readonly Date Today = new(16, Month.June, 2025);

    private static readonly (int Years, double Coupon)[] Bonds =
    [
        (1, 0.02), (2, 0.025), (3, 0.0275), (4, 0.03), (5, 0.03), (7, 0.0325), (9, 0.035), (12, 0.0375), (15, 0.04),
        (20, 0.0425),
    ];

    [Fact]
    public void ACurveOnMovedPricesIsTheFreshFit()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        double[] prices = [99.9, 99.6, 99.1, 98.9, 98.2, 97.6 + 0.5, 97.1, 96.3, 96.5, 96.0];
        var helpers = new List<BondHelper>();
        for (int i = 0; i < Bonds.Length; ++i)
        {
            var schedule = new Schedule(
                Today - new Period(6, TimeUnit.Months),
                Today + new Period(Bonds[i].Years, TimeUnit.Years),
                new Period(6, TimeUnit.Months),
                new TARGET(),
                BusinessDayConvention.Unadjusted,
                BusinessDayConvention.Unadjusted,
                DateGenerationRule.Backward,
                false,
                context);
            var bond = new FixedRateBond(
                2, 100.0, schedule, [Bonds[i].Coupon], new ActualActual(ActualActualConvention.ISDA));
            helpers.Add(new BondHelper(prices[i], bond, context));
        }

        var method = new NelsonSiegelFitting(l2: [0.5, 0.5, 0.5, 0.5]);
        var curve = new FittedBondDiscountCurve(
            Today, helpers, new Actual365Fixed(), method, context, 1e-10, 10000, [0.03, -0.01, 0.0, 0.5], 1.0);

        // QuantLib's fresh fit on the wheel, and its recalculated one
        Assert.True(Math.Abs((curve.Discount(10.0) / 0.6644661886683543) - 1.0) <= 1e-10, $"{curve.Discount(10.0)}");
        Assert.Equal(427, curve.FitResults.NumberOfIterations);
        Assert.True(Math.Abs(curve.Discount(10.0) - 0.664584692842777) > 1e-4, $"{curve.Discount(10.0)}");
    }
}
