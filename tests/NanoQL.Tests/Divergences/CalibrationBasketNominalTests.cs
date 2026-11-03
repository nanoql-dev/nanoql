// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The calibration basket's nominal: QuantLib's match reads a negative nominal as the flipped swap type
/// (<c>basketgeneratingengine.hpp:156-157</c>), but floors the solution's nominal at 1e-6 before dropping its sign
/// (<c>basketgeneratingengine.cpp:213-214</c>, <c>:230</c>), so a flipped solution's helper gets 1e-6. On the wheel, a
/// Bermudan payer exercised in one, two and three years into a 5-year inverse floater (fixed 3%; floating 4% − 0.5 ×
/// Euribor 6M) on 1,000,000, on a GSR model (a = σ = 0.01) on a flat 3% curve, gets helpers of nominal 1e-6 at strikes
/// 0.0215898859149053, 0.02160167978480207 and 0.02162134419969126. The receiver at −3% fixed with gearing 0.5 and
/// spread −4%, worth the same in every state, solves unflipped: nominals 490917.1124356477, 491116.4435245846 and
/// 491433.33826786536, from another guess (a strike of −3%), at the same strikes within 4e-9. nanoql keeps the
/// solution's size: QuantLib's strikes, and nominals within 3.1e-6 of the unflipped solution's, the two starts' spread.
/// QuantLib's check: its match on the unflipped equivalent.
/// </summary>
public class CalibrationBasketNominalTests
{
    [Fact]
    public void AFlippedSolutionKeepsItsNominal()
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
        var swap = new NonstandardSwap(
            SwapType.Payer,
            Enumerable.Repeat(1e6, 5),
            Enumerable.Repeat(1e6, 10),
            Schedule(12),
            Enumerable.Repeat(0.03, 5),
            new Thirty360(Thirty360Convention.BondBasis),
            Schedule(6),
            new Euribor6M(curve),
            -0.5,
            0.04,
            new Actual360(),
            context,
            paymentConvention: BusinessDayConvention.ModifiedFollowing);
        var swaption = new NonstandardSwaption(swap, new BermudanExercise([.. new[] { 1, 2, 3 }.Select(i => calendar.Advance(today, i, TimeUnit.Years))]));
        var engine = new Gaussian1dNonstandardSwaptionEngine(Gaussian1dInputs.Constant(curve, 0.01, 0.01), discountCurve: curve);
        SwaptionVolatilityStructure vol = ConstantSwaptionVolatility.Create(
            0, calendar, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed(), context);

        ImmutableArray<SwaptionHelper> basket = swaption.CalibrationBasket(
            engine, new EuriborSwapIsdaFixA(new Period(10, TimeUnit.Years), curve, curve), vol, context);
        double[] strikes = [0.0215898859149053, 0.02160167978480207, 0.02162134419969126];
        double[] unflipped = [490917.1124356477, 491116.4435245846, 491433.33826786536];
        Assert.Equal(3, basket.Length);
        for (int i = 0; i < basket.Length; i++)
        {
            Assert.Equal(new Date(20, Month.January, 2031), basket[i].Underlying.MaturityDate());
            Assert.True(Math.Abs(basket[i].Underlying.FixedRate - strikes[i]) <= 1e-12, $"{i}: {basket[i].Underlying.FixedRate}");
            Assert.True(Math.Abs((basket[i].Underlying.Nominal() / unflipped[i]) - 1.0) <= 1e-5, $"{i}: {basket[i].Underlying.Nominal()}");
        }
    }
}
