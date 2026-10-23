// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The float-float swap's arguments (the S-g1d plan's G1D6): a redemption flow takes the fixing date of its leg's
/// coupon paid on the same date (<c>floatfloatswap.cpp:391-525</c>), so it belongs to the exercise right exactly when
/// that coupon does, the engine's right holding the coupons fixing on or after the exercise
/// (<c>gaussian1dfloatfloatswaptionengine.cpp:128-133</c>). On the wheel, a 5-year amortizing swap from 19 January 2026
/// (Euribor 1Y + 0.5%, yearly on 1,000,000 to 200,000, 30/360, against Euribor 6M on the same nominals) with capital
/// exchanges, on a GSR model (a = 0.03, σ = 0.008) on a flat 3% curve with a 2.8% discount curve: a payer exercised on
/// 19 April 2027 holds the second leg's redemption on 19 January 2028, whose coupon fixes on 15 July 2027, but not the
/// first leg's, whose coupon fixed on 15 January 2027: 190231.52904994308, where the swap without the exchanges gives
/// 6566.591892282355; a receiver exercised on 15 January 2027, both coupons' fixing, holds both, which cancel:
/// 8184.371881337382 with the exchanges, 8184.368110793747 without.
/// </summary>
public class FloatFloatSwaptionArgumentsTests
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
        double[] nominals = [1e6, 8e5, 6e5, 4e5, 2e5];
        FloatFloatSwap Swap(SwapType type, bool exchanges) => new(
            type,
            nominals,
            nominals.SelectMany(n => new[] { n, n }),
            Schedule(12),
            new Euribor1Y(curve),
            new Thirty360(Thirty360Convention.BondBasis),
            Schedule(6),
            new Euribor6M(curve),
            new Actual360(),
            context,
            exchanges,
            exchanges,
            spread1: Enumerable.Repeat(0.005, 5),
            paymentConvention1: BusinessDayConvention.ModifiedFollowing,
            paymentConvention2: BusinessDayConvention.ModifiedFollowing);
        Gsr model = new(curve, [], [0.008], 0.03);
        YieldTermStructure discount = new FlatForward(today, 0.028, new Actual365Fixed());
        double Price(SwapType type, bool exchanges, Date exercise) => Gaussian1dFloatFloatSwaptionEngine.Calculate(
            new FloatFloatSwaption(Swap(type, exchanges), new EuropeanExercise(exercise)), context, model, discountCurve: discount).NPV!.Value;

        (SwapType Type, bool Exchanges, Date Exercise, double QuantLib)[] cases =
        [
            (SwapType.Payer, true, new Date(19, Month.April, 2027), 190231.52904994308),
            (SwapType.Payer, false, new Date(19, Month.April, 2027), 6566.591892282355),
            (SwapType.Receiver, true, new Date(15, Month.January, 2027), 8184.371881337382),
            (SwapType.Receiver, false, new Date(15, Month.January, 2027), 8184.368110793747),
        ];
        foreach ((SwapType type, bool exchanges, Date exercise, double quantLib) in cases)
        {
            double price = Price(type, exchanges, exercise);
            Assert.True(Math.Abs((price / quantLib) - 1.0) <= 1e-11, $"{type}, {exchanges} on {exercise}: {price}");
        }
    }
}
