// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The nonstandard swap's arguments: a redemption flow takes the reset date of its leg's coupon paid on the same date
/// (<c>nonstandardswap.cpp:303-317</c>, <c>:354-369</c>), so it belongs to the exercise right exactly when that coupon
/// does, as the nonstandard engine's header states. On the wheel, a 5-year amortizing payer at 3.5% from 19 January
/// 2026 (fixed nominals 1,000,000 to 200,000, annual; Euribor 6M on the same nominals) with capital exchanges, on a GSR
/// model (a = 0.03, σ = 0.008) on a flat 3% curve with a 2.8% discount curve: exercised on 19 April 2027, inside the
/// second fixed period, the right holds the floating redemption on 19 January 2028, whose coupon starts on 19 July
/// 2027, but not the fixed one, whose coupon starts on 19 January 2027: 190272.45330183694, where the swap without the
/// exchanges gives 10258.068968747979; exercised on 19 January 2027, the period's start, the two redemptions cancel:
/// 4383.961741083354 with the exchanges, 4383.961741083289 without.
/// </summary>
public class NonstandardSwapArgumentsTests
{
    [Fact]
    public void ARedemptionBelongsToTheRightWithItsCoupon()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var calendar = new TARGET();
        Date start = calendar.Advance(calendar.Advance(today, 1, TimeUnit.Years), 2, TimeUnit.Days);
        Schedule Schedule(int months) => new(
            start,
            calendar.Advance(start, 5, TimeUnit.Years),
            new Period(months, TimeUnit.Months),
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            false,
            context);
        double[] fixedNominals = [1e6, 8e5, 6e5, 4e5, 2e5];
        NonstandardSwap Swap(bool exchanges) => new(
            SwapType.Payer,
            fixedNominals,
            fixedNominals.SelectMany(n => new[] { n, n }),
            Schedule(12),
            Enumerable.Repeat(0.035, 5),
            new Thirty360(Thirty360Convention.BondBasis),
            Schedule(6),
            new Euribor6M(curve),
            1.0,
            0.0,
            new Actual360(),
            context,
            exchanges,
            exchanges,
            BusinessDayConvention.ModifiedFollowing);
        Gsr model = Gaussian1dInputs.Constant(curve, 0.03, 0.008);
        YieldTermStructure discount = new FlatForward(today, 0.028, new Actual365Fixed());
        double Price(bool exchanges, Date exercise) => Gaussian1dNonstandardSwaptionEngine.Calculate(
            new NonstandardSwaption(Swap(exchanges), new EuropeanExercise(exercise)), context, model, discountCurve: discount).NPV!.Value;

        (bool Exchanges, Date Exercise, double QuantLib)[] cases =
        [
            (true, new Date(19, Month.April, 2027), 190272.45330183694),
            (false, new Date(19, Month.April, 2027), 10258.068968747979),
            (true, new Date(19, Month.January, 2027), 4383.961741083354),
            (false, new Date(19, Month.January, 2027), 4383.961741083289),
        ];
        foreach ((bool exchanges, Date exercise, double quantLib) in cases)
        {
            double price = Price(exchanges, exercise);
            Assert.True(Math.Abs((price / quantLib) - 1.0) <= 1e-12, $"{exchanges} on {exercise}: {price}");
        }
    }
}
